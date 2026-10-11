using Maintenance.Core;
using System.Text.Json;
namespace Maintenance.App;
sealed record RemnantInventory(string[] Packages,string[] Commands,string[] Paths,bool Complete,string[] Notes);
sealed partial class MainWindow
{
    RemnantInventory remnantInventory=new([],[],[],false,["Evidence not collected"]);
    PresenceEvidence remnantPresence=new([],[],[],[],false);
    readonly Dictionary<string,RemnantAssessment> remnantAssessments=new(StringComparer.OrdinalIgnoreCase);
    readonly AppearanceChoice appDataView=new(){Width=220,Height=40};
    readonly InsightStrip appDataVisual=new();
    void ReviewMappedCache()
    {
        if(busy||appDataGrid.SelectedRows.Count==0||appDataGrid.SelectedRows[0].Tag is not AppDataFolder row)return;
        var menu=new ContextMenuStrip{BackColor=surface,ForeColor=ink,Renderer=new AccentMenuRenderer(accent,surface,ink)};
        try {
            using var guard=NativeFile.Guard(row.Path);
            foreach(string path in Directory.EnumerateDirectories(row.Path).Where(p=>DisposableFolderName(Path.GetFileName(p)))) {
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
                var assessment=remnantAssessments.GetValueOrDefault(row.Path);
                menu.Items.Add("Review "+Path.GetFileName(path),null,async(_,_)=>await OpenCacheReview(new(assessment?.Application??row.Name,path,Path.GetFileName(path),0,"Not yet measured",assessment?.Explanation??row.Evidence),3));
            }
            if(menu.Items.Count==0){menu.Dispose();MessageBox.Show(this,"No supported cache/log folder was found directly inside this folder. Settings and other data are not offered for removal.");return;}
            menu.Closed+=(_,_)=>BeginInvoke(menu.Dispose);menu.Show(Cursor.Position);
        } catch(Exception e){menu.Dispose();MessageBox.Show(this,"Could not review this folder: "+e.Message);}
    }
    static async Task<RemnantInventory> CollectRemnantInventory(CancellationToken token=default)
    {
        string json=await StartupTools.Run("""
        $notes=@();$commands=@();$paths=@();$packages=@();$complete=$true
        try {$packages=@(Get-AppxPackage -ErrorAction Stop | ForEach-Object {$_.Name})} catch {$complete=$false;$notes+='Current-user Store packages unavailable'}
        try {$commands+=@(Get-CimInstance Win32_Service -ErrorAction Stop | ForEach-Object {$_.PathName})} catch {$complete=$false;$notes+='Services unavailable'}
        try {$commands+=@(Get-ScheduledTask -ErrorAction Stop | ForEach-Object {foreach($a in $_.Actions){$a.Execute+' '+$a.Arguments}})} catch {$complete=$false;$notes+='Scheduled tasks unavailable'}
        foreach($key in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Run','HKLM:\Software\Microsoft\Windows\CurrentVersion\Run','HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run')) {
          try {if(Test-Path $key){$k=Get-Item $key -ErrorAction Stop;foreach($name in $k.GetValueNames()){$commands+=[string]$k.GetValue($name)}}}catch{$complete=$false;$notes+='Startup registry unavailable'}
        }
        foreach($key in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths','HKLM:\Software\Microsoft\Windows\CurrentVersion\App Paths','HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths')) {
          try {if(Test-Path $key){foreach($k in Get-ChildItem $key -ErrorAction Stop){$paths+=[string]$k.GetValue('')}}}catch{$complete=$false;$notes+='Registered executable paths unavailable'}
        }
        @{Packages=@($packages);Commands=@($commands);Paths=@($paths);Complete=$complete;Notes=@($notes)}|ConvertTo-Json -Depth 4 -Compress
        """,token:token);
        var value=JsonSerializer.Deserialize<RemnantInventory>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new([],[],[],false,["Inventory response unavailable"]);
        string[] Clean(string[]? input)=>(input??[]).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return value with{Packages=Clean(value.Packages),Commands=Clean(value.Commands),Paths=Clean(value.Paths),Notes=Clean(value.Notes)};
    }
    static (bool Checked,bool Found) CheckExpectedBinaries(RemnantRule rule)
    {
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] roots=rule.Folder=="Notepad++"?[Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Notepad++"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Notepad++")]:rule.Folder=="Code"?[Path.Combine(local,"Programs","Microsoft VS Code"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft VS Code"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft VS Code")]:[Path.Combine(local,rule.Folder)];
        bool complete=true,found=false;
        foreach(string root in roots)try {
            // GetAttributes distinguishes missing from inaccessible; do not follow redirected installs.
            var attrs=File.GetAttributes(root);if((attrs&FileAttributes.ReparsePoint)!=0){complete=false;continue;}
            foreach(string file in Directory.EnumerateFiles(root,"*.exe"))if(rule.ExecutableNames.Contains(Path.GetFileName(file),StringComparer.OrdinalIgnoreCase))found=true;
            foreach(string child in Directory.EnumerateDirectories(root)){
                if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0){complete=false;continue;}
                foreach(string file in Directory.EnumerateFiles(child,"*.exe"))if(rule.ExecutableNames.Contains(Path.GetFileName(file),StringComparer.OrdinalIgnoreCase))found=true;
            }
        }catch(DirectoryNotFoundException){}catch(FileNotFoundException){}catch{complete=false;}
        return(complete,found);
    }
    void AssessAppData()
    {
        remnantAssessments.Clear();if(appDataReport==null)return;
        foreach(var row in appDataReport.Folders) {
            var rule=AppRemnants.Rules.FirstOrDefault(r=>r.Folder.Equals(row.Name,StringComparison.OrdinalIgnoreCase));
            var markers=new List<string>();bool checkedPaths=true;
            if(rule!=null)foreach(string marker in rule.Markers)try {var attributes=File.GetAttributes(Path.Combine(row.Path,marker));if((attributes&FileAttributes.ReparsePoint)==0)markers.Add(marker);}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}catch{checkedPaths=false;}
            var binary=rule==null?(Checked:false,Found:false):CheckExpectedBinaries(rule);
            remnantAssessments[row.Path]=AppRemnants.Assess(row,remnantPresence,markers,checkedPaths&&binary.Checked,binary.Found);
        }
    }
    void ShowAppDataVisual(AppDataFolder? row=null)
    {
        if(row!=null){
            var colors=new[]{Color.MediumTurquoise,Color.Goldenrod,Color.CornflowerBlue,Color.MediumPurple,Color.SlateGray};
            appDataVisual.ShowData("Measured composition · "+row.Name+(row.Partial?" · partial":""),row.Categories.Select((p,i)=>($"{p.Key}: {FormatBytes(p.Value)}",p.Value,colors[i%colors.Length])).ToArray());
        }else if(appDataReport!=null){
            var rows=appDataReport.Folders;long likely=rows.Count(r=>remnantAssessments.GetValueOrDefault(r.Path)?.State=="Likely leftovers");
            long present=rows.Count(r=>(remnantAssessments.GetValueOrDefault(r.Path)?.State??r.Association).Contains("present",StringComparison.OrdinalIgnoreCase));
            appDataVisual.ShowData("Evidence overview · folder counts, not reclaimable space",($"App evidence: {present}",present,Color.MediumTurquoise),($"Likely leftovers: {likely}",likely,Color.Goldenrod),($"Uncertain / shared: {rows.Count-likely-present}",rows.Count-likely-present,Color.SlateGray));
        }
    }
}



