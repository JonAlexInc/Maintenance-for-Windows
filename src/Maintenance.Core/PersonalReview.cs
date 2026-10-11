using System.ComponentModel;
namespace Maintenance.Core;
public static class PersonalReview
{
    public static Candidate Snapshot(string path)
    {
        using var guard=NativeFile.Guard(Path.GetDirectoryName(path)!);
        using var h=NativeFile.Open(path);var i=NativeFile.Read(h);
        return new(path,i.Size,i.Creation,i.Write,NativeFile.Change(h),i.Volume,i.IndexHigh,i.IndexLow);
    }
    public static RunResult Recycle(string root,IEnumerable<Candidate> selected,IReadOnlyList<string> exclusions,CancellationToken token,Action<Outcome> journal,Action<string> recycle)
    {
        string boundary=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+Path.DirectorySeparatorChar;
        var outcomes=new List<Outcome>();
        foreach(var item in selected.DistinctBy(c=>c.Path,StringComparer.OrdinalIgnoreCase)) {
            if(token.IsCancellationRequested)break;
            journal(new(item.Path,"Pending","Requested recycle; outcome unknown until recorded",item.Size));Outcome result;
            try {
                string path=Path.GetFullPath(item.Path);
                if(!path.StartsWith(boundary,StringComparison.OrdinalIgnoreCase)||path.IndexOf(':',2)>=0||CleanupEngine.IsExcluded(path,exclusions))throw new IOException("Outside scanned folder, named stream, or excluded file");
                if(new DriveInfo(Path.GetPathRoot(path)!).DriveType!=DriveType.Fixed)throw new IOException("Recycling is limited to fixed local drives");
                using var guard=NativeFile.Guard(Path.GetDirectoryName(path)!);
                using(var h=NativeFile.Open(path)) {
                    var info=NativeFile.Read(h);
                    if(((FileAttributes)info.Attributes&(FileAttributes.Directory|FileAttributes.ReparsePoint|FileAttributes.Offline|FileAttributes.System|FileAttributes.ReadOnly))!=0||info.Links!=1)throw new IOException("Protected, linked, offline, or non-file item");
                    if(info.Size!=item.Size||info.Creation!=item.Created||info.Write!=item.Written||NativeFile.Change(h)!=item.Changed||info.Volume!=item.Volume||info.IndexHigh!=item.IndexHigh||info.IndexLow!=item.IndexLow)throw new IOException("File changed since scan; scan again");
                }
                if(token.IsCancellationRequested)break;
                // Shell recycling is path-based. Parent guards prevent directory redirection;
                // metadata is revalidated immediately before handing the path to the shell.
                recycle(path);result=new(path,"Recycled","Sent selected file to the Windows Recycle Bin",item.Size);
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException or Win32Exception or System.Runtime.InteropServices.COMException){result=new(item.Path,"Skipped",e.Message,0);}
            outcomes.Add(result);journal(result);
        }
        return new(DateTimeOffset.Now,outcomes,null,null,token.IsCancellationRequested);
    }
}

