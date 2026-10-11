using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
namespace Maintenance.App;
record DiskWorkload(string Name,string Block,int Queue,bool Random,int? WritePercent=null,int Duration=0,string? Title=null);
record DiskScore(double MBps,double Iops,double Microseconds,double P95Microseconds=0,double P99Microseconds=0);
record DiskMeasurement(string Workload,bool Write,int Pass,DiskScore Score);
static class DiskBenchmark
{
    internal static readonly DiskWorkload[] Workloads=[new("SEQ1M Q8T1","1M",8,false),new("SEQ1M Q1T1","1M",1,false),new("RND4K Q32T1","4K",32,true),new("RND4K Q1T1","4K",1,true)];
    internal static readonly DiskWorkload[] Extended=[new("MIX4K Q8 · 70/30","4K",8,true,30,Title:"Multitasking · mixed I/O"),new("MIX1M Q4 · 50/50","1M",4,false,50,Title:"Simultaneous transfers"),new("RND64K Q4T1","64K",4,true,Title:"Medium-block access"),new("SEQ1M Q8 · sustained","1M",8,false,Duration:60,Title:"Sustained transfers · 60 seconds")];
    internal static readonly Dictionary<string,long> Sizes=new(){["64 MiB"]=64L<<20,["128 MiB"]=128L<<20,["256 MiB"]=256L<<20,["512 MiB"]=512L<<20,["1 GiB"]=1L<<30,["2 GiB"]=2L<<30,["4 GiB"]=4L<<30,["8 GiB"]=8L<<30,["16 GiB"]=16L<<30,["32 GiB"]=32L<<30,["64 GiB"]=64L<<30};
    internal static DiskScore Parse(string xml,bool write)
    {
        var span=XDocument.Parse(xml).Root?.Element("TimeSpan")??throw new IOException("Benchmark returned no results.");
        double Number(XElement e,string name)=>double.Parse(e.Element(name)?.Value??"0",CultureInfo.InvariantCulture);
        double seconds=Number(span,"TestTimeSeconds"),bytes=0,ios=0,latency=0;string prefix=write?"Write":"Read";
        foreach(var t in span.Elements("Thread").Elements("Target")){double count=Number(t,prefix+"Count");bytes+=Number(t,prefix+"Bytes");ios+=count;latency+=Number(t,"Average"+prefix+"LatencyMilliseconds")*count;}
        if(seconds<=0||ios<=0)throw new IOException("Benchmark completed without measured I/O.");
        double Percentile(int percentile){var bucket=span.Element("Latency")?.Elements("Bucket").FirstOrDefault(b=>Number(b,"Percentile")==percentile);return bucket==null?0:Number(bucket,prefix+"Milliseconds")*1000;}
        return new(bytes/seconds/1_000_000,ios/seconds,latency/ios*1000,Percentile(95),Percentile(99));
    }
    internal static async Task Run(string folder,long size,int passes,int seconds,DiskWorkload[] workloads,IProgress<string> status,IProgress<DiskMeasurement> results,CancellationToken token)
    {
        if(!Sizes.ContainsValue(size)||passes is <1 or >9||seconds is <1 or >30||workloads.Length==0)throw new ArgumentException("Invalid benchmark settings.");
        token.ThrowIfCancellationRequested();
        string engine=Path.Combine(AppContext.BaseDirectory,"Tools","diskspd.exe");if(!File.Exists(engine))throw new IOException("Benchmark engine is missing. Use the complete app folder.");
        if(!Directory.Exists(folder))throw new IOException("Choose an existing writable folder.");
        if(!folder.StartsWith(@"\\")){var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))!);if(drive.AvailableFreeSpace<size+(256L<<20))throw new IOException("Not enough free space for the test file plus 256 MiB reserve.");}
        string file=Path.Combine(folder,".maintenance-benchmark-"+Guid.NewGuid().ToString("N")+".tmp");bool owned=false;
        try {
            status.Report("Preparing random test data… (not part of measured results)");
            using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,FileOptions.Asynchronous|FileOptions.WriteThrough)) {
                owned=true;byte[] data=new byte[1024*1024];long done=0;
                while(done<size){token.ThrowIfCancellationRequested();RandomNumberGenerator.Fill(data);int count=(int)Math.Min(data.Length,size-done);await stream.WriteAsync(data.AsMemory(0,count),token);done+=count;if(done%(16L<<20)==0)status.Report($"Preparing test file · {done*100/size}%");}stream.Flush(true);
            }
            foreach(var workload in workloads)foreach(bool write in workload.WritePercent.HasValue?new[]{false}:new[]{false,true})for(int pass=1;pass<=passes;pass++) {
                int duration=workload.Duration>0?workload.Duration:seconds;
                token.ThrowIfCancellationRequested();status.Report($"{workload.Name} · {(workload.WritePercent.HasValue?"Read + write":write?"Write":"Read")} · pass {pass}/{passes} · {duration}s test + 1s warm-up");
                var info=new ProcessStartInfo(engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                foreach(string arg in new[]{"-d"+duration,"-W1","-C0","-b"+workload.Block,"-o"+workload.Queue,"-t1",workload.Random?"-r":"-s","-w"+(workload.WritePercent??(write?100:0)),"-Sh","-L","-Rxml","-n","-Z16M",file})info.ArgumentList.Add(arg);
                using var process=Process.Start(info)??throw new IOException("Could not start benchmark engine.");
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                try {await process.WaitForExitAsync(token);}catch(OperationCanceledException){try{process.Kill(true);}catch(InvalidOperationException){}await process.WaitForExitAsync(CancellationToken.None);throw;}
                string xml=await output,error=await errors;token.ThrowIfCancellationRequested();
                if(process.ExitCode!=0)throw new IOException("Benchmark failed. This folder/device may not support unbuffered I/O. "+error[..Math.Min(error.Length,350)]);
                results.Report(new(workload.Name,write,pass,Parse(xml,write)));
                if(workload.WritePercent.HasValue)results.Report(new(workload.Name,true,pass,Parse(xml,true)));
            }
        } finally {if(owned)try{File.Delete(file);}catch(Exception e){throw new IOException("Test file could not be removed. Remove it when the folder is available: "+file,e);}}
    }
}
