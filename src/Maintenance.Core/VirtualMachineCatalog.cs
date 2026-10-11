namespace Maintenance.Core;
public sealed record VmFile(string Path,long Size,DateTime Modified,string Category,string Format);
public sealed record VmScan(List<VmFile> Files,int Inspected,int Skipped,bool Partial);
public static class VirtualMachineCatalog
{
    public static string? Kind(string path,bool includeRaw=false)
    {
        if(new[]{".vma.zst",".vma.lzo",".vma.gz"}.Any(s=>path.EndsWith(s,StringComparison.OrdinalIgnoreCase)))return "VM export / appliance";
        return System.IO.Path.GetExtension(path).ToLowerInvariant() switch{
        ".vhd" or ".vhdx" or ".vmdk" or ".vdi" or ".qcow" or ".qcow2" or ".qed" or ".hdd" or ".hds" or ".vhdsv2" or ".vhds"=>"Virtual disk",
        ".avhd" or ".avhdx" or ".vmsn" or ".vmsd"=>"Snapshot / differencing disk",
        ".vmem" or ".vmss" or ".vmrs" or ".vmgs"=>"Saved state / memory",
        ".vmx" or ".vmxf" or ".vbox" or ".vbox-prev" or ".vmcx" or ".pvs" or ".nvram"=>"Configuration / firmware",
        ".ova" or ".ovf" or ".xva" or ".vma"=>"VM export / appliance",
        ".img" or ".raw" when includeRaw=>"Possible raw disk (ambiguous)",_=>null};
    }
    public static VmScan Scan(IEnumerable<string> roots,bool includeRaw,CancellationToken token,IProgress<int>? progress=null)
    {
        var found=new List<VmFile>();var pending=new Queue<string>(roots);int seen=0,skipped=0;var clock=System.Diagnostics.Stopwatch.StartNew();long last=0;
        while(pending.Count>0&&!token.IsCancellationRequested){string folder=pending.Dequeue();try{
            if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0){skipped++;continue;}
            foreach(var info in new DirectoryInfo(folder).EnumerateFileSystemInfos()){
                if(token.IsCancellationRequested)break;seen++;if(clock.ElapsedMilliseconds-last>=500){progress?.Report(seen);last=clock.ElapsedMilliseconds;}
                try{
                    if((info.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0){skipped++;continue;}
                    if(info is DirectoryInfo){pending.Enqueue(info.FullName);continue;}
                    string? kind=Kind(info.Name,includeRaw);if(kind!=null)found.Add(new(info.FullName,((FileInfo)info).Length,info.LastWriteTime,kind,info.Extension.TrimStart('.').ToUpperInvariant()));
                }catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped++;}
            }
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped++;}}
        return new(found,seen,skipped,token.IsCancellationRequested||skipped>0);
    }
}
