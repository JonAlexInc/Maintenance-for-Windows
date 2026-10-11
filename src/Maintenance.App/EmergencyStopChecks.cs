using System.Diagnostics;
namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyEmergencyStop()
    {
        try {
            SetBusy(false);SelectPage(1);scanLocation.SelectedItem="Downloads";
            string root=Directory.CreateDirectory(Path.GetFullPath(Path.Combine(".test-data","stop-"+Guid.NewGuid().ToString("N")))).FullName;
            File.WriteAllText(Path.Combine(root,"retained.txt"),"Stop all fixture");
            await ScanLocalDrive(root);await renderTask;int rows=files.Items.Count;
            if(rows!=1)throw new Exception("Result fixture missing");
            string marker=Path.Combine(root,"helper-pid.txt");
            var helper=StartupTools.Run("[IO.File]::WriteAllText($p.Path,[string]$PID);Start-Sleep -Seconds 30",new{Path=marker});
            var start=Stopwatch.StartNew();while(!File.Exists(marker)&&start.ElapsedMilliseconds<6000)await Task.Delay(30);
            if(!File.Exists(marker))throw new Exception("Owned helper did not start");
            int pid=int.Parse(File.ReadAllText(marker));
            cancel=new();speedCancel=new();storageRun=new();systemSearchCancel=new();renderCancel=new();SetBusy(true);
            UpdateStatusStrip();if(!emergencyStop.Enabled)throw new Exception("Universal stop unavailable while working");
            Size=new(1280,900);themeChoice.SelectedItem="Dark";SelectPage(8);await Task.Delay(100);UpdateStatusStrip();
            if(!emergencyStop.Visible||emergencyStop.Parent!=liveStatus||emergencyStop.Right>liveStatus.ClientSize.Width)throw new Exception("Universal stop is clipped or page-specific");
            start.Restart();emergencyStop.PerformClick();
            if(new[]{cancel,speedCancel,storageRun,systemSearchCancel,renderCancel}.Any(s=>!s!.IsCancellationRequested))throw new Exception("A job was not cancelled");
            try{await helper.WaitAsync(TimeSpan.FromSeconds(5));throw new Exception("Helper ignored Stop all");}catch(OperationCanceledException){}
            if(AppOperations.Count!=0)throw new Exception("Owned helper registration leaked");
            try{using var process=Process.GetProcessById(pid);if(!process.HasExited)throw new Exception("Owned helper still running");}catch(ArgumentException){}
            if(files.Items.Count!=rows||!File.Exists(Path.Combine(root,"retained.txt")))throw new Exception("Stopping discarded results or fixture file");
            foreach(var source in new[]{cancel,speedCancel,storageRun,systemSearchCancel,renderCancel})source!.Dispose();
            cancel=speedCancel=storageRun=systemSearchCancel=renderCancel=null;SetBusy(false);UpdateStatusStrip();
            if(emergencyStopping||emergencyStop.Enabled||liveStatus.Running)throw new Exception("Stop state failed to settle");
            foreach(string theme in new[]{"Dark","Light"}) {
                themeChoice.SelectedItem=theme;SelectPage(0);UpdateStatusStrip();await Task.Delay(80);using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save("artifacts/stop-all-"+theme+".png");
            }
            File.WriteAllText("artifacts/stop-all-check-success.txt",DateTimeOffset.Now.ToString("o")+$" · Universal status-bar stop at 1280x900; five job tokens cancelled; owned PowerShell helper exited in {start.ElapsedMilliseconds} ms including fixture checks; existing results/files retained; UI settled; light/dark screenshots. No personal files or system settings changed.");
        }catch(Exception e){File.WriteAllText("artifacts/stop-all-check-failure.txt",e.ToString());throw;}
    }
}
