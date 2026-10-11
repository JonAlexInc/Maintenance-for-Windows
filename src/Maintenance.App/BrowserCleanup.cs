using Maintenance.Core;
using Microsoft.Win32;
using System.Diagnostics;
namespace Maintenance.App;

sealed record BrowserSource(string Name,string Process,string[] Roots,bool Supported);
static class BrowserCatalog
{
    internal static List<BrowserSource> Read()
    {
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var result=new List<BrowserSource>();
        var known=new[]{
            ("Microsoft Edge","msedge",Path.Combine(local,"Microsoft","Edge","User Data"),false),
            ("Google Chrome","chrome",Path.Combine(local,"Google","Chrome","User Data"),false),
            ("Brave","brave",Path.Combine(local,"BraveSoftware","Brave-Browser","User Data"),false),
            ("Vivaldi","vivaldi",Path.Combine(local,"Vivaldi","User Data"),false),
            ("Chromium","chromium",Path.Combine(local,"Chromium","User Data"),false),
            ("Opera","opera",Path.Combine(roaming,"Opera Software","Opera Stable"),false),
            ("Opera GX","opera",Path.Combine(roaming,"Opera Software","Opera GX Stable"),false),
            ("Firefox","firefox",Path.Combine(local,"Mozilla","Firefox","Profiles"),true),
            ("LibreWolf","librewolf",Path.Combine(local,"librewolf","Profiles"),true),
            ("Waterfox","waterfox",Path.Combine(local,"Waterfox","Profiles"),true)};
        var registered=new List<(string Name,string Process)>();
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string keyPath in new[]{@"SOFTWARE\Clients\StartMenuInternet",@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet"})
            try{using var parent=hive.OpenSubKey(keyPath);if(parent==null)continue;foreach(string child in parent.GetSubKeyNames()){
                using var key=parent.OpenSubKey(child);using var command=key?.OpenSubKey(@"shell\open\command");string cmd=command?.GetValue(null)?.ToString()??"";
                var match=System.Text.RegularExpressions.Regex.Match(cmd,"^\\s*(?:\"([^\"]+\\.exe)\"|(.+?\\.exe))(?:\\s|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                string exe=match.Success?Path.GetFileNameWithoutExtension(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value):"";
                registered.Add((key?.GetValue(null)?.ToString()??child,exe));
            }}catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){}
        foreach(var (name,process,root,firefox) in known){
            bool data=Directory.Exists(root),installed=registered.Any(r=>r.Process.Equals(process,StringComparison.OrdinalIgnoreCase));
            if(!data&&!installed)continue;
            var roots=new List<string>();
            try{if(data){
                var profiles=Directory.EnumerateDirectories(root).Where(p=>firefox||Path.GetFileName(p)=="Default"||Path.GetFileName(p).StartsWith("Profile ",StringComparison.OrdinalIgnoreCase)).ToList();
                if(name.StartsWith("Opera"))profiles.Add(root);
                foreach(var profile in profiles)roots.AddRange(CacheRoots(profile,firefox).Where(Directory.Exists));
                // Opera's disk cache can be stored separately from its roaming profile.
                if(name.StartsWith("Opera"))roots.AddRange(CacheRoots(root.Replace(roaming,local),false).Where(Directory.Exists));
            }}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
            result.Add(new(name+(installed?"":" · profile data found"),process,roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),true));
        }
        foreach(var r in registered.DistinctBy(r=>r.Name))if(!known.Any(k=>k.Item2.Equals(r.Process,StringComparison.OrdinalIgnoreCase)))result.Add(new(r.Name,r.Process,[],false));
        return result;
    }
    internal static string[] CacheRoots(string profile,bool firefox)=>firefox?[Path.Combine(profile,"cache2","entries")]:[Path.Combine(profile,"Cache","Cache_Data"),Path.Combine(profile,"Code Cache"),Path.Combine(profile,"GPUCache")];
}
sealed partial class MainWindow
{
    const string BrowserMode="Browser caches";
    readonly FlowLayoutPanel browserSources=new(){Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Padding=new(4),Visible=false};
    readonly List<(CheckBox Check,BrowserSource Source)> browserChecks=[];
    Control BuildBrowserSources()
    {
        foreach(var source in BrowserCatalog.Read()){
            var check=new AppearanceCheckBox{Text=source.Name+(source.Supported?(source.Roots.Length==0?" · no supported cache found":" · cache only"):" · cleanup not supported"),AutoSize=false,Width=420,Height=42,Margin=new(4),AutoCheck=source.Supported&&source.Roots.Length>0,Tag=source.Supported?null:MutedRole};
            sourceTips.SetToolTip(check,source.Roots.Length==0?"No supported cache location was found for this browser. Custom and portable profiles may not be detected.":string.Join("\r\n",source.Roots));
            check.CheckedChanged+=(_,_)=>{if(!busy)ClearResults();};browserChecks.Add((check,source));browserSources.Controls.Add(check);
        }
        if(browserChecks.Count>0)browserSources.SetFlowBreak(browserChecks.Last().Check,true);
        var note=new Label{AutoSize=true,Text="Select browsers to review cached files at least 7 days old. Close selected browsers first, including background processes.\nKeeps passwords, cookies, history, bookmarks, extensions, sessions and offline site storage. Current account only; portable/custom profiles may be missed.",Margin=new(4,8,4,8)};
        if(browserChecks.Count==0)note.Text="No installed browser registrations or known browser profiles were found.\nPortable browsers and custom profile paths may not be detected.";
        browserSources.Controls.Add(note);browserSources.SizeChanged+=(_,_)=>{note.MaximumSize=new(Math.Max(250,browserSources.Width-20),0);int width=Math.Max(260,(browserSources.Width-20)/Math.Max(1,browserSources.Width/440)-8);foreach(var b in browserChecks)b.Check.Width=width;};return browserSources;
    }
    bool BrowsersClosed()
    {
        foreach(var item in browserChecks.Where(b=>b.Check.Checked)){
            Process[] running=Process.GetProcessesByName(item.Source.Process);bool open=running.Length>0;foreach(var p in running)p.Dispose();
            if(open){MessageBox.Show(this,"Close "+item.Source.Name+" and its background processes before scanning or cleaning its caches.","Browser is running");return false;}
        }return true;
    }
    bool ConfigureBrowserEngine()
    {
        var roots=browserChecks.Where(b=>b.Check.Checked&&b.Source.Supported).SelectMany(b=>b.Source.Roots).ToArray();
        if(roots.Length==0){MessageBox.Show(this,"Check at least one browser with a supported cache location.");return false;}
        if(!BrowsersClosed())return false;engine=new MultiCleanupEngine(roots);return true;
    }
}


