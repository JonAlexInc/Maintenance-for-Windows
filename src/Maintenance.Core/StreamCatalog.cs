using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Maintenance.Core;

public sealed record StreamProgress(int Entries,int Streams,TimeSpan Elapsed,string Folder);

// Read-only enumeration: never opens a stream for writing or removes metadata.
public static class StreamCatalog
{
    public static bool HasNamedStreams(string path)
    {
        var handle = FindFirstStreamW(path, 0, out var data, 0);
        if (handle == new IntPtr(-1)) {
            int error = Marshal.GetLastWin32Error();
            if (error == 38) return false;
            throw new Win32Exception(error);
        }
        try {
            do { if (!data.Name.Equals("::$DATA", StringComparison.OrdinalIgnoreCase)) return true; }
            while (FindNextStreamW(handle, out data));
            int error = Marshal.GetLastWin32Error();
            if (error != 38) throw new Win32Exception(error);
            return false;
        } finally { FindClose(handle); }
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=296)] public string Name; }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr FindFirstStreamW(string path,int level,out StreamData data,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool FindNextStreamW(IntPtr handle,out StreamData data);
    [DllImport("kernel32.dll")] static extern bool FindClose(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool GetVolumeInformationW(string root,StringBuilder? name,int size,out uint serial,out uint maxComponent,out uint flags,StringBuilder fs,int fsSize);
    public static bool IsNtfs(string root,out string reason)
    {
        var fs=new StringBuilder(64);
        string volume=Path.GetPathRoot(Path.GetFullPath(root))!.TrimEnd('\\')+"\\";
        if(!GetVolumeInformationW(volume,null,0,out _,out _,out uint flags,fs,fs.Capacity)) {reason=new Win32Exception(Marshal.GetLastWin32Error()).Message;return false;}
        bool supported=fs.ToString().Equals("NTFS",StringComparison.OrdinalIgnoreCase)&&(flags&0x40000)!=0;
        reason=supported?"NTFS · named streams supported":$"{fs} · NTFS with named-stream support required";return supported;
    }
    public static string OwnerPath(string streamPath) {int index=streamPath.IndexOf(':',2);return index<0?streamPath:streamPath[..index];}
    public static string StreamName(string streamPath) {int index=streamPath.IndexOf(':',2);return index<0?"":streamPath[(index+1)..].Replace(":$DATA","",StringComparison.OrdinalIgnoreCase);}
    public static ScanResult Read(string root,CancellationToken token,TimeSpan? maxWait=null,IProgress<StreamProgress>? progress=null)
    {
        root=Path.GetFullPath(root);
        if(!IsNtfs(root,out string reason))throw new IOException("Cannot scan this location: "+reason);
        var found=new List<Candidate>();var skipped=new List<Finding>();var dirs=new Queue<string>();dirs.Enqueue(root);
        var watch=System.Diagnostics.Stopwatch.StartNew();int count=0;long last=-300;bool limited=false;
        bool Stop(){if(token.IsCancellationRequested)return true;if(maxWait.HasValue&&watch.Elapsed>=maxWait.Value){limited=true;return true;}return false;}
        void Inspect(string path,FileSystemInfo info)
        {
            string native=path.StartsWith(@"\\?\")?path:path.StartsWith(@"\\")?@"\\?\UNC\"+path[2..]:@"\\?\"+path;
            var handle=FindFirstStreamW(native,0,out var data,0);
            if(handle==new IntPtr(-1)){int error=Marshal.GetLastWin32Error();if(error!=38)skipped.Add(new(path,"Stream enumeration: "+new Win32Exception(error).Message));return;}
            try {
                do {
                    if(Stop())break;
                    if(!data.Name.Equals("::$DATA",StringComparison.OrdinalIgnoreCase))found.Add(new(path+data.Name,data.Size,info.CreationTimeUtc.ToFileTimeUtc(),info.LastWriteTimeUtc.ToFileTimeUtc(),0,0,0,0));
                }while(FindNextStreamW(handle,out data));
                if(!Stop()){int error=Marshal.GetLastWin32Error();if(error!=38)skipped.Add(new(path,"Stream enumeration incomplete: "+new Win32Exception(error).Message));}
            }finally{FindClose(handle);}
        }
        while(dirs.Count>0&&!Stop()) {
            string dir=dirs.Dequeue();
            try {
                var folder=new DirectoryInfo(dir);
                if((folder.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0){skipped.Add(new(dir,"Redirected or offline folder omitted"));continue;}
                Inspect(dir,folder);
                foreach(var info in folder.EnumerateFileSystemInfos()) {
                    if(Stop())break;count++;
                    if(watch.ElapsedMilliseconds-last>=250){progress?.Report(new(count,found.Count,watch.Elapsed,dir));last=watch.ElapsedMilliseconds;}
                    try {
                        if((info.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0){skipped.Add(new(info.FullName,"Redirected or offline item omitted"));continue;}
                        if((info.Attributes&FileAttributes.Directory)!=0)dirs.Enqueue(info.FullName);else Inspect(info.FullName,info);
                    }catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped.Add(new(info.FullName,e.Message));}
                }
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped.Add(new(dir,e.Message));}
        }
        return new(DateTimeOffset.Now,found,skipped,token.IsCancellationRequested,limited);
    }
}
