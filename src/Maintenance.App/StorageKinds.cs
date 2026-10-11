using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace Maintenance.App;
static class StorageKinds
{
    internal static readonly string[] Labels=["Windows / system","Applications","App data","Personal / other","Temporary","Other files","Games","Media","Documents","Development","Archives / images","Game extras / cache","Virtual machines / WSL"];
    internal static readonly Color[] DefaultColors=[Color.MediumPurple,Color.CornflowerBlue,Color.Teal,Color.SeaGreen,Color.Goldenrod,Color.SlateGray,Color.HotPink,Color.DarkTurquoise,Color.OliveDrab,Color.DarkOrange,Color.SteelBlue,Color.IndianRed,Color.MediumOrchid];
    internal static Color[] Colors=(Color[])DefaultColors.Clone();
    internal static List<string> GameRoots()
    {
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path){if(!string.IsNullOrWhiteSpace(path)&&Path.IsPathFullyQualified(path)&&!path.StartsWith(@"\\")&&new DriveInfo(Path.GetPathRoot(path)!).DriveType!=DriveType.Network)roots.Add(Path.GetFullPath(path).TrimEnd('\\')+'\\');}
        try {
            using var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            using var machine=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            string? steam=key?.GetValue("SteamPath") as string??machine?.GetValue("InstallPath") as string;
            if(steam!=null){Add(Path.Combine(steam,"steamapps","common"));string file=Path.Combine(steam,"steamapps","libraryfolders.vdf");
                if(File.Exists(file))foreach(Match match in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))Add(Path.Combine(match.Groups[1].Value.Replace(@"\\",@"\"),"steamapps","common"));}
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){}
        foreach(string registryPath in new[]{@"SOFTWARE\WOW6432Node\GOG.com\Games",@"SOFTWARE\GOG.com\Games",@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"}) {
            try {using var catalog=Registry.LocalMachine.OpenSubKey(registryPath);if(catalog is null)continue;
                foreach(string name in catalog.GetSubKeyNames()){using var game=catalog.OpenSubKey(name);Add(game?.GetValue("path") as string??game?.GetValue("InstallDir") as string);}
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException){}
        }
        try {
            string manifests=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic","EpicGamesLauncher","Data","Manifests");
            if(Directory.Exists(manifests))foreach(string file in Directory.EnumerateFiles(manifests,"*.item")){try{using var json=JsonDocument.Parse(File.ReadAllText(file));if(json.RootElement.TryGetProperty("InstallLocation",out var location))Add(location.GetString());}catch(JsonException){}}
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){}
        return roots.ToList();
    }
    internal static int Special(string path,IReadOnlyList<string>? roots=null)
    {
        string p=path.Replace('/','\\');string ext=Path.GetExtension(path).ToLowerInvariant();
        bool Has(string part)=>p.Contains("\\"+part+"\\",StringComparison.OrdinalIgnoreCase);
        if(Has("steamapps\\shadercache")||Has("steamapps\\downloading")||Has("steamapps\\workshop"))return 11;
        if((roots!=null&&roots.Any(root=>p.StartsWith(root,StringComparison.OrdinalIgnoreCase)))||Has("steamapps\\common")||Has("XboxGames")||Has("GOG Games")||Has("EA Games")||Has("Origin Games")||Has("Ubisoft Game Launcher\\games"))return 6;
        return -1;
    }
    internal static int Personal(string path)
    {
        string p=path.Replace('/','\\');string ext=Path.GetExtension(path).ToLowerInvariant();
        if(new[]{".vhd",".vhdx",".vmdk",".vdi",".qcow",".qcow2",".vmx",".vmem",".vmsn",".vmss",".vmrs"}.Contains(ext)||new[]{"\\VMWare\\","\\Virtual Machines\\","\\WSL\\"}.Any(v=>p.Contains(v,StringComparison.OrdinalIgnoreCase)))return 12;
        if(new[]{"\\.git\\","\\node_modules\\","\\.venv\\","\\.nuget\\"}.Any(v=>p.Contains(v,StringComparison.OrdinalIgnoreCase))||new[]{".cs",".cpp",".h",".py",".rs",".ts",".tsx",".go",".sln",".csproj"}.Contains(ext))return 9;
        if(new[]{".mp4",".mkv",".mov",".avi",".mp3",".flac",".wav",".jpg",".jpeg",".png",".heic",".raw",".webp"}.Contains(ext))return 7;
        if(new[]{".pdf",".docx",".xlsx",".pptx",".odt",".txt",".rtf"}.Contains(ext))return 8;
        if(new[]{".zip",".7z",".rar",".iso",".vhd",".vhdx",".vmdk",".bak"}.Contains(ext))return 10;
        return -1;
    }
    internal static bool ProjectDirectory(string directory,IEnumerable<string> names)
    {
        if(new[]{"Development","Projects","Repos","Repositories"}.Contains(Path.GetFileName(directory.TrimEnd('\\')),StringComparer.OrdinalIgnoreCase))return true;
        return names.Any(name=>new[]{".git",".hg",".svn","package.json","Cargo.toml","go.mod","pyproject.toml","CMakeLists.txt","composer.json","pom.xml","build.gradle","build.gradle.kts"}.Contains(name,StringComparer.OrdinalIgnoreCase)||new[]{".sln",".slnx",".csproj",".fsproj",".vcxproj"}.Contains(Path.GetExtension(name),StringComparer.OrdinalIgnoreCase));
    }
}

