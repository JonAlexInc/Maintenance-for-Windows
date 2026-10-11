namespace Maintenance.Core;

public sealed record AppDataEvidence(string Name, string Location);
public sealed record AppDataFolder(string Name, string Scope, string Path, long Bytes, int Files, int Skipped, bool Partial, string Association, string Evidence)
{
    public Dictionary<string,long> Categories {get;init;} = new();
    public int Executables {get;init;}
}
public sealed record AppDataReport(IReadOnlyList<AppDataFolder> Folders, IReadOnlyList<string> Issues, int Entries, bool Partial);

public static class AppDataMap
{
    public static string Category(string relative) {
        var parts=relative.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        if(parts.Any(p=>new[]{"Cache","Code Cache","GPUCache","CacheStorage","CachedData"}.Contains(p,StringComparer.OrdinalIgnoreCase)))return "Cache candidates";
        if(parts.Any(p=>new[]{"Logs","Log","CrashDumps","Crashpad"}.Contains(p,StringComparer.OrdinalIgnoreCase)))return "Logs / diagnostics";
        if(parts.Any(p=>new[]{"Temp","tmp"}.Contains(p,StringComparer.OrdinalIgnoreCase)))return "Temporary candidates";
        if(new[]{".exe",".dll",".msi",".msix"}.Contains(System.IO.Path.GetExtension(relative),StringComparer.OrdinalIgnoreCase))return "Program files";
        return "Settings / other data";
    }
    public static bool Within(string path, string root) => !string.IsNullOrWhiteSpace(root) &&
        (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    public static (string State, string Evidence) Associate(string folder, IEnumerable<AppDataEvidence> apps, IEnumerable<string> processes)
    {
        var running = processes.Where(p => Within(p, folder)).ToArray();
        var registered = apps.Where(a => Within(a.Location, folder)).ToArray();
        if (running.Length > 0) return ("Running app present", "Running executable inside this folder: " + string.Join("; ", running) + ". This does not establish ownership of every file.");
        if (registered.Length > 0) return ("Install location present", "Registered install location inside this folder: " + string.Join("; ", registered.Select(a => a.Name + " — " + a.Location)) + ". Data may be shared.");
        var names = apps.Where(a => a.Name.Equals(System.IO.Path.GetFileName(folder), StringComparison.OrdinalIgnoreCase)).Select(a => a.Name).Distinct().ToArray();
        return names.Length > 0 ? ("Name match only", "Exact folder/app name match: " + string.Join(", ", names) + ". Ownership is unverified; name alone is not proof.")
            : ("Ownership unknown", "No association in accessible process paths or uninstall registrations. Store, portable, shared or custom apps may still use this data. This is not proof of abandonment.");
    }

    public static AppDataReport Scan(IEnumerable<(string Scope, string Path)> roots, IReadOnlyList<AppDataEvidence> apps, IReadOnlyList<string> processes, CancellationToken token, IProgress<string>? progress = null, int entryLimit = 250_000)
    {
        if (entryLimit < 1) throw new ArgumentOutOfRangeException(nameof(entryLimit));
        var rows = new List<AppDataFolder>(); var issues = new List<string>(); int entries = 0; bool partial = false;
        void Issue(string message) { if (issues.Count < 200) issues.Add(message); }
        bool Stop() => token.IsCancellationRequested || entries >= entryLimit;
        foreach (var root in roots) {
            if (Stop()) { partial = true; break; }
            try {
                using var rootGuard = NativeFile.Guard(root.Path);
                foreach (var top in new DirectoryInfo(root.Path).EnumerateFileSystemInfos()) {
                    if (Stop()) { partial = true; break; }
                    entries++;
                    if (top is not DirectoryInfo) continue;
                    long bytes = 0; int files = 0, skipped = 0, executables=0; bool unfinished = false;
                    var categories=new Dictionary<string,long>();int folderStart=entries;
                    var queue = new Queue<string>(); queue.Enqueue(top.FullName);
                    progress?.Report("App data map · " + root.Scope + " · " + top.Name);
                    while (queue.Count > 0 && !Stop() && entries-folderStart<25_000) {
                        string dir = queue.Dequeue();
                        try {
                            using var guard = NativeFile.Guard(dir);
                            foreach (var item in new DirectoryInfo(dir).EnumerateFileSystemInfos()) {
                                if (Stop() || entries-folderStart>=25_000) { unfinished = true; break; }
                                entries++;
                                try {
                                    var attr = item.Attributes;
                                    if ((attr & FileAttributes.ReparsePoint) != 0 || NativeFile.NeedsRecall(attr)) { skipped++; continue; }
                                    if (item is DirectoryInfo) queue.Enqueue(item.FullName);
                                    else { long size=((FileInfo)item).Length;bytes += size; files++;string category=Category(System.IO.Path.GetRelativePath(top.FullName,item.FullName));categories[category]=categories.GetValueOrDefault(category)+size;if(item.Extension.Equals(".exe",StringComparison.OrdinalIgnoreCase))executables++; }
                                } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped++; }
                            }
                        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { skipped++; }
                    }
                    unfinished |= queue.Count > 0 || Stop();
                    var association = Associate(top.FullName, apps, processes);
                    rows.Add(new(top.Name, root.Scope, top.FullName, bytes, files, skipped, unfinished || skipped > 0, association.State, association.Evidence){Categories=categories,Executables=executables});
                    partial |= unfinished;
                }
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { partial = true; Issue(root.Scope + ": " + e.Message); }
        }
        if (token.IsCancellationRequested) { partial = true; Issue("Cancelled; some folders were not measured."); }
        if (entries >= entryLimit) { partial = true; Issue($"Reached {entryLimit:N0} entries; some folders were not measured."); }
        return new(rows.OrderByDescending(r => r.Bytes).ToArray(), issues, entries, partial);
    }
}
