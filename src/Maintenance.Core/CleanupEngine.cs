using System.ComponentModel;

namespace Maintenance.Core;

public record Candidate(string Path, long Size, long Created, long Written, long Changed, uint Volume, uint IndexHigh, uint IndexLow);
public record Finding(string Path, string Reason);
public record ScanResult(DateTimeOffset At, List<Candidate> Candidates, List<Finding> Skipped, bool Cancelled, bool Limited);
public record Outcome(string Path, string Status, string Reason, long Bytes);
public record RunResult(DateTimeOffset At, List<Outcome> Outcomes, long? FreeBefore, long? FreeAfter, bool Cancelled);

public sealed class CleanupEngine
{
    public const int AgeDays = 7, MaxEntries = int.MaxValue;
    public string Root { get; }
    public CleanupEngine(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (Root.Length <= 3 || Root.StartsWith(@"\\")) throw new ArgumentException("A local temporary subfolder is required.");
        if (new DriveInfo(Path.GetPathRoot(Root)!).DriveType != DriveType.Fixed) throw new IOException("Only fixed local drives are supported.");
    }
    public static CleanupEngine ForCurrentUser()
    {
        // Do not trust a redirected TEMP environment variable in the initial preview.
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
        if (!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.TrimEndingDirectorySeparator(expected), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Custom temporary-folder locations are not supported in 0.01. No files will be scanned or deleted.");
        return new(expected);
    }
    public bool Contains(string path) => Path.GetFullPath(path).StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static bool IsExcluded(string path, IEnumerable<string> exclusions) => exclusions.Any(e =>
        path.Equals(Path.TrimEndingDirectorySeparator(e), StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(e) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    static bool Unsafe(NativeFile.Info i) => ((FileAttributes)i.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.System | FileAttributes.ReadOnly | FileAttributes.Offline)) != 0 || i.Links != 1;
    static bool Old(NativeFile.Info i) => i.Creation <= DateTime.UtcNow.AddDays(-AgeDays).ToFileTimeUtc() && i.Write <= DateTime.UtcNow.AddDays(-AgeDays).ToFileTimeUtc();
    static string Error(Exception e) => e is Win32Exception w ? $"Unavailable, locked, or denied (Windows error {w.NativeErrorCode})" : "Unavailable or unsafe path";
    public ScanResult Scan(IReadOnlyList<string> exclusions, CancellationToken token, IProgress<int>? progress = null, int maxEntries = MaxEntries, TimeSpan? maxDuration = null)
    {
        if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
        if (maxDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxDuration));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var good = new List<Candidate>(); var skipped = new List<Finding>(); var visited = 0;
        var dirs = new Stack<string>(); dirs.Push(Root); bool limited = false;
        while (dirs.Count > 0 && !token.IsCancellationRequested && !limited)
        {
            if (maxDuration.HasValue && clock.Elapsed >= maxDuration.Value) { limited = true; break; }
            var dir = dirs.Pop();
            if (IsExcluded(dir, exclusions)) { skipped.Add(new(dir, "Excluded")); continue; }
            try
            {
                using var guard = NativeFile.Guard(dir);
                foreach (var path in Directory.EnumerateFileSystemEntries(dir))
                {
                    if (token.IsCancellationRequested) break;
                    if (maxDuration.HasValue && clock.Elapsed >= maxDuration.Value) { limited = true; break; }
                    if (++visited > maxEntries) { limited = true; break; }
                    if (visited % 100 == 0) progress?.Report(visited);
                    if (!Contains(path) || IsExcluded(path, exclusions)) { skipped.Add(new(path, "Excluded or outside allowed location")); continue; }
                    try
                    {
                        using var h = NativeFile.Open(path); var info = NativeFile.Read(h);
                        if (((FileAttributes)info.Attributes).HasFlag(FileAttributes.ReparsePoint)) { skipped.Add(new(path, "Link or redirected item")); continue; }
                        if (((FileAttributes)info.Attributes).HasFlag(FileAttributes.Directory)) { dirs.Push(path); continue; }
                        if (Unsafe(info)) { skipped.Add(new(path, "Protected, offline, or multiply linked file")); continue; }
                        if (!Old(info)) { skipped.Add(new(path, "Created or modified within seven days")); continue; }
                        good.Add(new(path, info.Size, info.Creation, info.Write, NativeFile.Change(h), info.Volume, info.IndexHigh, info.IndexLow));
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception) { skipped.Add(new(path, Error(e))); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception) { skipped.Add(new(dir, Error(e))); }
        }
        return new(DateTimeOffset.Now, good, skipped, token.IsCancellationRequested, limited);
    }
    public RunResult Clean(IEnumerable<Candidate> selected, IReadOnlyList<string> exclusions, CancellationToken token,
        Action<Outcome> journal, IProgress<int>? progress = null)
    {
        var outcomes = new List<Outcome>(); var before = Free(); var count = 0;
        foreach (var item in selected.DistinctBy(c => c.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (token.IsCancellationRequested) break;
            Outcome outcome;
            // Persist intent before any destructive operation. Journal failure stops the run.
            journal(new(item.Path, "Pending", "Requested; outcome unknown until recorded", item.Size));
            try
            {
                if (!Contains(item.Path) || IsExcluded(item.Path, exclusions)) outcome = new(item.Path, "Skipped", "Excluded or outside allowed location", 0);
                else
                {
                    using var guard = NativeFile.Guard(Path.GetDirectoryName(item.Path)!);
                    using (var h = NativeFile.Open(item.Path, delete: true))
                    {
                        var i = NativeFile.Read(h);
                        if (Unsafe(i) || !Old(i) || i.Size != item.Size || i.Creation != item.Created || i.Write != item.Written ||
                            NativeFile.Change(h) != item.Changed || i.Volume != item.Volume || i.IndexHigh != item.IndexHigh || i.IndexLow != item.IndexLow)
                            outcome = new(item.Path, "Skipped", "Changed since scan or no longer eligible", 0);
                        else if (token.IsCancellationRequested) outcome = new(item.Path, "Skipped", "Cancelled before deletion", 0);
                        else { NativeFile.DeleteOpened(h); outcome = new(item.Path, "Deleted", "Deleted selected temporary file", item.Size); }
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception)
            { outcome = new(item.Path, "Skipped", Error(e), 0); }
            outcomes.Add(outcome); journal(outcome); progress?.Report(++count);
        }
        return new(DateTimeOffset.Now, outcomes, before, Free(), token.IsCancellationRequested);
    }
    public long? Free() { try { return new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace; } catch { return null; } }
}

