namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyInternetTests()
    {
        SetBusy(false);SelectPage(3);speedMode.SelectedItem="Internet speed";themeChoice.SelectedItem="Dark";
        if(internetDirection.Text!="Download + Upload"||speedTarget.Text!=InternetBenchmark.Cloudflare)throw new InvalidOperationException("Internet defaults incorrect");
        internetDirection.SelectedItem="Download only";if(speedTarget.Items.Count<2)throw new InvalidOperationException("Regional downloads missing");
        internetDirection.SelectedItem="Upload only";if(speedTarget.Items.Count!=3)throw new InvalidOperationException("Upload offered on download-only target");
        internetDirection.SelectedItem="Download + Upload";internetSize.SelectedItem="10 MB";
        bool large=Environment.GetCommandLineArgs().Contains("--internet-large-check");long testBytes=large?1_000_000_000:10_000_000;internetSize.SelectedItem=large?"1 GB":"10 MB";
        string provider=Environment.GetCommandLineArgs().Contains("--alternative-check")?"Clouvider · Amsterdam":Environment.GetCommandLineArgs().Contains("--garr-check")?"GARR · Rome":InternetBenchmark.Cloudflare;speedTarget.SelectedItem=provider;
        speedRunId=Guid.NewGuid().ToString("N");speedRunMode="Internet speed";speedRunTarget=provider;SaveSpeedRun("Started","Validation run");
        using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        foreach(bool upload in new[]{false,true}){
            var gauge=upload?uploadGauge:downloadGauge;
            var result=await InternetBenchmark.Run(upload,testBytes,internetTargets[provider],new Progress<InternetProgress>(p=>gauge.Update(p)),limit.Token,internetUploads[provider]);
            SaveSpeedSample("Internet transfer",upload?"Upload":"Download",1,result.Bytes,result.Mbps,"Mbps",detail:$"Live validation · {result.Seconds:N2}s");
            if(result.Bytes!=testBytes||!double.IsFinite(result.Mbps)||result.Mbps<=0)throw new InvalidOperationException("Invalid live internet result");
        }
        SaveSpeedRun("Completed","Live verification complete");await Task.Delay(400);RefreshHistory();if(savedSpeeds.Count(r=>r.Run==speedRunId)!=3)throw new InvalidOperationException("Saved speed results did not survive reload");
        Refresh();using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save("artifacts/internet-gauges-preview.png");
        SelectPage(4);historyTabs.SelectedIndex=4;Refresh();using(var historyImage=new Bitmap(Width,Height)){DrawToBitmap(historyImage,new Rectangle(0,0,Width,Height));historyImage.Save("artifacts/speed-history-preview.png");}
        File.WriteAllText("artifacts/internet-check-success.txt",DateTimeOffset.Now.ToString("o")+" · "+provider+" download + generated upload completed; direction, capability and history reload passed. Bytes per direction: "+testBytes+"");
    }
}



