using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
namespace Maintenance.App;
record InternetProgress(bool Upload,long Bytes,long Total,double Mbps,bool Complete=false,string? Status=null);
record InternetResult(bool Upload,long Bytes,double Seconds){public double Mbps=>Bytes*8/Seconds/1_000_000;}
static class InternetBenchmark
{
    internal const string Cloudflare="Cloudflare · Automatic location";
    internal static readonly Dictionary<string,long> Sizes=new(){["10 MB"]=10_000_000,["25 MB"]=25_000_000,["50 MB"]=50_000_000,["100 MB"]=100_000_000,["250 MB"]=250_000_000,["500 MB"]=500_000_000,["1 GB"]=1_000_000_000};
    internal static async Task<InternetResult> Run(bool upload,long bytes,string downloadUrl,IProgress<InternetProgress> progress,CancellationToken token,string? uploadUrl=null)
    {
        using var handler=new SocketsHttpHandler{ConnectTimeout=TimeSpan.FromSeconds(15),AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None};
        using var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
        var watch=Stopwatch.StartNew();long done=0,lastBytes=0;double lastTime=0;
        void Report(long count){done+=count;double now=watch.Elapsed.TotalSeconds;if(now-lastTime<.2)return;progress.Report(new(upload,done,bytes,(done-lastBytes)*8/(now-lastTime)/1_000_000));lastBytes=done;lastTime=now;}
        async Task<HttpResponseMessage> Send(Func<HttpRequestMessage> create){long start=done;
            for(int attempt=0;;attempt++){
                using var request=create();var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
                if((int)response.StatusCode!=429||attempt>=2)return response;
                var retry=response.Headers.RetryAfter;var wait=retry?.Delta??(retry?.Date-DateTimeOffset.UtcNow)??TimeSpan.FromSeconds(60);
                if(wait>TimeSpan.FromMinutes(2))return response;
                response.Dispose();done=start;lastBytes=done;lastTime=watch.Elapsed.TotalSeconds;
                progress.Report(new(upload,done,bytes,0,false,$"{(upload?"Sending":"Receiving")} · Server cooldown {Math.Max(1,wait.TotalSeconds):N0}s"));
                watch.Stop();await Task.Delay(wait>TimeSpan.Zero?wait:TimeSpan.FromSeconds(1),token);watch.Start();
            }
        }
        progress.Report(new(upload,0,bytes,0));
        while(done<bytes){token.ThrowIfCancellationRequested();long chunk=Math.Min(bytes-done,upload?(downloadUrl.Contains("speed.cloudflare.com",StringComparison.Ordinal)?10_000_000:512_000):25_000_000);
            // Space public-server requests without counting intentional pauses as transfer time.
            if(done>0&&downloadUrl.Contains("speed.cloudflare.com",StringComparison.Ordinal)){watch.Stop();await Task.Delay(2100,token);watch.Start();}
            if(upload){using var response=await Send(()=>new HttpRequestMessage(HttpMethod.Post,uploadUrl??"https://speed.cloudflare.com/__up"){Content=new GeneratedContent(chunk,Report,token)});CheckResponse(response);}
            else{
                string url=downloadUrl.Contains("speed.cloudflare.com/__down",StringComparison.Ordinal)?downloadUrl+"?bytes="+chunk:downloadUrl.EndsWith("garbage.php",StringComparison.Ordinal)?downloadUrl+"?ckSize="+(int)Math.Ceiling(chunk/1048576d)+"&r="+Guid.NewGuid().ToString("N"):downloadUrl;
                using var response=await Send(()=>{var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.CacheControl=new(){NoCache=true,NoStore=true};return request;});CheckResponse(response);
                if(response.Content.Headers.ContentType?.MediaType?.Contains("text",StringComparison.OrdinalIgnoreCase)==true)throw new IOException("Test server returned a page instead of test data.");
                using var stream=await response.Content.ReadAsStreamAsync(token);byte[] buffer=new byte[128*1024];long received=0;
                while(received<chunk){int n=await stream.ReadAsync(buffer.AsMemory(0,(int)Math.Min(buffer.Length,chunk-received)),token);if(n==0)throw new IOException("Download ended early; result discarded.");received+=n;Report(n);}
            }
        }
        token.ThrowIfCancellationRequested();var result=new InternetResult(upload,done,watch.Elapsed.TotalSeconds);progress.Report(new(upload,done,bytes,result.Mbps,true));return result;
    }
    static void CheckResponse(HttpResponseMessage response){if((int)response.StatusCode==429)throw new IOException("This test server is rate-limiting requests (429). Wait before trying again, or select another provider. No average is recorded for this direction.");response.EnsureSuccessStatusCode();}
    sealed class GeneratedContent(long length,Action<long> report,CancellationToken token):HttpContent
    {
        protected override bool TryComputeLength(out long value){value=length;return true;}
        protected override async Task SerializeToStreamAsync(Stream stream,TransportContext? context){byte[] buffer=new byte[128*1024];RandomNumberGenerator.Fill(buffer);long done=0;while(done<length){int n=(int)Math.Min(buffer.Length,length-done);await stream.WriteAsync(buffer.AsMemory(0,n),token);done+=n;report(n);}}
    }
}

