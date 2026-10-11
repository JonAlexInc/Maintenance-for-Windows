namespace Maintenance.Core;
public sealed record RemnantRule(string Name,string Folder,string[] AppNames,string[] ExecutableNames,string[] Markers,string Source);
public sealed record RemnantAssessment(string Application,string State,string Explanation,string Source);
public sealed record PresenceEvidence(string[] InstalledNames,string[] PackageNames,string[] ExecutablePaths,string[] StartupCommands,bool CatalogsComplete);
public static class AppRemnants
{
    public static readonly RemnantRule[] Rules = [
        new("Notepad++","Notepad++",["Notepad++"],["notepad++.exe"],["config.xml","session.xml"],"https://npp-user-manual.org/docs/config-files/"),
        new("Visual Studio Code","Code",["Microsoft Visual Studio Code","Visual Studio Code"],["Code.exe"],["User","logs"],"https://code.visualstudio.com/docs/setup/uninstall"),
        new("Discord","discord",["Discord"],["Discord.exe"],["Local Storage","Cache"],"https://support.discord.com/hc/en-us/articles/115004307527--Windows-Corrupt-Installation"),
        new("Discord PTB","discordptb",["Discord PTB"],["DiscordPTB.exe"],["Local Storage","Cache"],"https://support.discord.com/hc/en-us/articles/115004307527--Windows-Corrupt-Installation")
    ];
    static bool MatchesName(string value,string expected)=>value.Equals(expected,StringComparison.OrdinalIgnoreCase)||value.StartsWith(expected+" (",StringComparison.OrdinalIgnoreCase)||value.StartsWith(expected+" ",StringComparison.OrdinalIgnoreCase);
    public static RemnantAssessment Assess(AppDataFolder folder,PresenceEvidence presence,IReadOnlyList<string> markers,bool binariesChecked,bool binaryFound)
    {
        var rule=Rules.FirstOrDefault(r=>r.Folder.Equals(folder.Name,StringComparison.OrdinalIgnoreCase));
        if(folder.Name.Equals("Packages",StringComparison.OrdinalIgnoreCase)||new[]{"Microsoft","Google","Adobe","Mozilla","Programs"}.Contains(folder.Name,StringComparer.OrdinalIgnoreCase))
            return new(folder.Name,"Shared / review separately","Publisher or package container can serve multiple products. No whole-folder abandonment assessment.","");
        if(rule is null){
            var packageMatches=presence.PackageNames.Where(p=>p.Split('.').Any(part=>part.Equals(folder.Name,StringComparison.OrdinalIgnoreCase))).ToArray();
            if(packageMatches.Length>0)return new(folder.Name,"Package name match", "Current-user Store package name matches this folder: "+string.Join(", ",packageMatches)+". This supports investigating an installed package; data ownership remains unverified.","");
            return new(folder.Name,folder.Executables>0?"App files present":folder.Association,folder.Evidence+(folder.Executables>0?" Executable files were measured; portable or retained installations may be present.":""),"");
        }
        var found=new List<string>();
        foreach(string name in presence.InstalledNames.Where(n=>rule.AppNames.Any(a=>MatchesName(n,a))))found.Add("Registered app: "+name);
        foreach(string name in presence.PackageNames.Where(n=>rule.AppNames.Any(a=>n.Contains(a.Replace(" ",""),StringComparison.OrdinalIgnoreCase))))found.Add("Store package: "+name);
        foreach(string path in presence.ExecutablePaths.Where(p=>rule.ExecutableNames.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase)))found.Add("Accessible executable: "+path);
        foreach(string command in presence.StartupCommands.Where(c=>rule.ExecutableNames.Any(n=>c.Contains(n,StringComparison.OrdinalIgnoreCase))))found.Add("Startup / service / task reference: "+command);
        if(binaryFound||folder.Executables>0)found.Add("Executable found in app data or expected installation location");
        if(found.Count>0)return new(rule.Name,"App present / referenced",string.Join("\n",found)+"\nNo abandonment recommendation; references can themselves be stale.",rule.Source);
        bool recognized=rule.Markers.All(m=>markers.Contains(m,StringComparer.OrdinalIgnoreCase));
        if(!recognized)return new(rule.Name,"Ownership needs review","Known folder name, but the expected data layout was not fully verified. Name alone cannot establish ownership.",rule.Source);
        if(!presence.CatalogsComplete||!binariesChecked||folder.Partial)return new(rule.Name,"Assessment incomplete","Recognized data layout; some inventory, installation paths or folder contents could not be checked. Absence is unconfirmed.",rule.Source);
        return new(rule.Name,"Likely leftovers","Recognized app-data layout. No matching uninstall record, current-user Store package, accessible running executable or startup/service/task reference; expected binary locations checked without a match. Portable/custom installations elsewhere and other accounts are not ruled out. Review cache/log data separately; preserve settings and personal data.",rule.Source);
    }
}
