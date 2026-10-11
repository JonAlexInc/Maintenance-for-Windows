using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Maintenance.Core;

public sealed record DuplicateGroup(string Hash, IReadOnlyList<Candidate> Files)
{
    public long Size => Files[0].Size;
    public long ExtraBytes => Size * (Files.Count - 1);
}
public sealed record DuplicateScan(IReadOnlyList<DuplicateGroup> Groups, IReadOnlyList<Finding> Issues, int Entries, int Skipped, bool Partial) { public int FilesExamined { get; init; } public int FoldersExamined { get; init; } }

// Exact primary-content comparisons. Named streams and hard links are omitted rather than
// treating different metadata or shared physical storage as disposable duplicate copies.
public static class DuplicateCatalog
{
    const FileAttributes Unsafe = FileAttributes.Directory | FileAttributes.Offline | FileAttributes.System | FileAttributes.ReadOnly;
    static bool Expected(Exception e) => e is IOException or UnauthorizedAccessException or Win32Exception;
    static readonly string[] ProtectedRoots = new[] {
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
    }.Where(p => !string.IsNullOrEmpty(p)).ToArray();
    public static bool Protected(string path) => CleanupEngine.IsExcluded(path, ProtectedRoots) ||
        path.Split(Path.DirectorySeparatorChar).Any(s => new[] { "$Recycle.Bin", "System Volume Information", "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData", "WindowsApps" }.Contains(s, StringComparer.OrdinalIgnoreCase));
    public static string LocalPath(string path)
    {
        path = Path.GetFullPath(path);
        if (path.StartsWith(@"\\") || path.Length < 3 || path[1] != ':' || path.IndexOf(':', 2) >= 0) throw new IOException("Choose a local or USB drive folder.");
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) throw new IOException("Choose a ready local or USB drive.");
        // Fail closed on filesystems where stream and file-identity checks aren't supported.
        if (!drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase)) throw new IOException("This first duplicate finder supports NTFS volumes.");
        return path;
    }
    public static bool ReviewOnly(string path)
    {
        try {
            if (new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Fixed) return true;
            for (string? current = path; current != null; current = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current)))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            return false;
        } catch { return true; }
    }
    static Candidate Snapshot(string path, FileStream file)
    {
        var i = NativeFile.Read(file.SafeFileHandle);
        if (((FileAttributes)i.Attributes & Unsafe) != 0 || NativeFile.NeedsRecall((FileAttributes)i.Attributes) || (((FileAttributes)i.Attributes & FileAttributes.ReparsePoint) != 0 && !NativeFile.ResidentCloud(file.SafeFileHandle)) || i.Links != 1) throw new IOException("Protected, offline, linked or read-only file omitted.");
        if (StreamCatalog.HasNamedStreams(path)) throw new IOException("Extra data streams present; use the data-stream review instead.");
        return new(path, i.Size, i.Creation, i.Write, NativeFile.Change(file.SafeFileHandle), i.Volume, i.IndexHigh, i.IndexLow);
    }
    static FileStream Open(string path) => new(NativeFile.OpenData(path), FileAccess.Read, 128 * 1024, false);
    static void Check(Candidate expected, FileStream file)
    {
        if (Snapshot(expected.Path, file) != expected) throw new IOException("File changed since scan; scan again.");
    }
    static bool Equal(FileStream a, FileStream b, CancellationToken token, Action<long, long>? progress = null)
    {
        if (a.Length != b.Length) return false;
        a.Position = b.Position = 0;
        byte[] x = new byte[128 * 1024], y = new byte[x.Length];
        while (a.Position < a.Length) {
            token.ThrowIfCancellationRequested();
            int count = (int)Math.Min(x.Length, a.Length - a.Position);
            a.ReadExactly(x.AsSpan(0, count)); b.ReadExactly(y.AsSpan(0, count));
            if (!x.AsSpan(0, count).SequenceEqual(y.AsSpan(0, count))) return false;
            progress?.Invoke(a.Position, a.Length);
        }
        return true;
    }
    public static DuplicateScan Scan(IEnumerable<string> roots, IReadOnlyList<string> exclusions, CancellationToken token, IProgress<string>? progress = null, int entryLimit = 250_000)
    {
        if (entryLimit < 1) throw new ArgumentOutOfRangeException(nameof(entryLimit));
        var paths = roots.Select(LocalPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0) throw new IOException("Add a folder to scan.");
        var queue = new Queue<string>(paths.Where(p => !paths.Any(other => other != p && CleanupEngine.IsExcluded(p, [other]))));
        var issues = new List<Finding>(); var sizes = new Dictionary<long, List<string>>();
        var groups = new List<DuplicateGroup>(); int entries = 0, skipped = 0, filesExamined = 0, foldersExamined = 0; bool partial = false;
        var clock = Stopwatch.StartNew(); long last = -250;
        void Report(string message) { if (clock.ElapsedMilliseconds - last >= 200) { progress?.Report(message); last = clock.ElapsedMilliseconds; } }
        void Skip(string path, string reason) { skipped++; if (issues.Count < 1000) issues.Add(new(path, reason)); }
        try {
            while (queue.Count > 0) {
                token.ThrowIfCancellationRequested();
                string dir = queue.Dequeue();
                if (Protected(dir) || CleanupEngine.IsExcluded(dir, exclusions)) { Skip(dir, "Protected or excluded folder"); continue; }
                try {
                    using var guard = NativeFile.Guard(dir, true); foldersExamined++;
                    foreach (var item in new DirectoryInfo(dir).EnumerateFileSystemInfos()) {
                        token.ThrowIfCancellationRequested();
                        if (entries >= entryLimit) { partial = true; queue.Clear(); break; }
                        entries++; if (item is FileInfo) filesExamined++; Report($"Duplicates · Looking through {entries:N0} entries · {dir}");
                        try {
                            if (Protected(item.FullName) || CleanupEngine.IsExcluded(item.FullName, exclusions)) { Skip(item.FullName, "Protected or excluded location"); continue; }
                            var attributes = item.Attributes;
                            if (NativeFile.NeedsRecall(attributes)) { Skip(item.FullName, "Cloud-only / offline: make available locally before scanning"); continue; }
                            if ((attributes & FileAttributes.ReparsePoint) != 0 && !NativeFile.ResidentCloud(item.FullName)) { Skip(item.FullName, "Folder link, junction or unsupported reparse point: not followed"); continue; }
                            if ((attributes & FileAttributes.System) != 0 || (item is FileInfo && (attributes & FileAttributes.ReadOnly) != 0)) { Skip(item.FullName, "Protected or read-only file"); continue; }
                            if (item is DirectoryInfo) queue.Enqueue(item.FullName);
                            else if (item is FileInfo f && f.Length > 0) {
                                if (!sizes.TryGetValue(f.Length, out var bucket)) sizes.Add(f.Length, bucket = []);
                                bucket.Add(f.FullName);
                            } else Skip(item.FullName, "Empty file; no duplicate space to review");
                        } catch (Exception e) when (Expected(e)) { Skip(item.FullName, e.Message); }
                    }
                } catch (Exception e) when (Expected(e)) { Skip(dir, e.Message); }
            }
            int hashed = 0;
            foreach (var bucket in sizes.Values.Where(b => b.Count > 1)) {
                // Cheap first/last samples rule out different files before reading whole large
                // disks, archives or media. Samples are never accepted as proof of a match.
                var samples = new Dictionary<string, List<string>>();
                foreach (string path in bucket) {
                    token.ThrowIfCancellationRequested();
                    try {
                        using var guard = NativeFile.Guard(Path.GetDirectoryName(path)!, true);
                        using var file = Open(path); var candidate = Snapshot(path, file);
                        using var sample = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        byte[] buffer = new byte[(int)Math.Min(64 * 1024, file.Length)];
                        file.ReadExactly(buffer); sample.AppendData(buffer);
                        if (file.Length > buffer.Length) { file.Position = file.Length - buffer.Length; file.ReadExactly(buffer); sample.AppendData(buffer); }
                        Check(candidate, file);
                        string key = candidate.Size + ":" + Convert.ToHexString(sample.GetHashAndReset());
                        if (!samples.TryGetValue(key, out var possible)) samples.Add(key, possible = []);
                        possible.Add(path); Report("Duplicates · Checking file samples · " + path);
                    } catch (Exception e) when (Expected(e)) { Skip(path, e.Message); }
                }
                var hashes = new Dictionary<string, List<Candidate>>();
                foreach (string path in samples.Values.Where(s => s.Count > 1).SelectMany(s => s)) {
                    token.ThrowIfCancellationRequested();
                    try {
                        using var guard = NativeFile.Guard(Path.GetDirectoryName(path)!, true);
                        using var file = Open(path); var candidate = Snapshot(path, file);
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        byte[] buffer = new byte[128 * 1024]; int read;
                        while ((read = file.Read(buffer)) > 0) {
                            token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read);
                            Report($"Duplicates · Comparing file {hashed + 1:N0} · {Path.GetFileName(path)} · {file.Position:N0} / {file.Length:N0} bytes");
                        }
                        Check(candidate, file); string digest = Convert.ToHexString(hash.GetHashAndReset()); hashed++;
                        if (!hashes.TryGetValue(digest, out var matches)) hashes.Add(digest, matches = []);
                        matches.Add(candidate);
                    } catch (Exception e) when (Expected(e)) { Skip(path, e.Message); }
                }
                foreach (var pair in hashes.Where(h => h.Value.Count > 1)) {
                    // SHA-256 narrows the candidates; a byte-for-byte comparison proves equality.
                    var remaining = pair.Value.ToList();
                    while (remaining.Count > 1) {
                        var keeper = remaining[0]; remaining.RemoveAt(0);
                        try {
                            using var guard = NativeFile.Guard(Path.GetDirectoryName(keeper.Path)!, true);
                            using var keep = Open(keeper.Path); Check(keeper, keep);
                            var verified = new List<Candidate> { keeper };
                            foreach (var other in remaining.ToArray()) {
                                token.ThrowIfCancellationRequested(); Report("Duplicates · Verifying exact match · " + other.Path);
                                try {
                                    using var otherGuard = NativeFile.Guard(Path.GetDirectoryName(other.Path)!, true);
                                    using var copy = Open(other.Path); Check(other, copy);
                                    if (Equal(keep, copy, token, (done, total) => Report($"Duplicates · Verifying {Path.GetFileName(other.Path)} · {done:N0} / {total:N0} bytes"))) { verified.Add(other); remaining.Remove(other); }
                                } catch (Exception e) when (Expected(e)) { Skip(other.Path, e.Message); remaining.Remove(other); }
                            }
                            if (verified.Count > 1) groups.Add(new(pair.Key, verified.ToArray()));
                        } catch (Exception e) when (Expected(e)) { Skip(keeper.Path, e.Message); }
                    }
                }
            }
        } catch (OperationCanceledException) { partial = true; }
        return new(groups.OrderByDescending(g => g.ExtraBytes).ToArray(), issues, entries, skipped, partial) { FilesExamined = filesExamined, FoldersExamined = foldersExamined };
    }
    public static RunResult Recycle(DuplicateGroup group, Candidate keeper, IReadOnlyList<Candidate> selected, IReadOnlyList<string> exclusions, CancellationToken token, Action<Outcome> journal, Action<string> recycle, IProgress<string>? progress = null)
    {
        var outcomes = new List<Outcome>();
        if (!group.Files.Contains(keeper) || selected.Count == 0 || selected.Any(c => c.Path.Equals(keeper.Path, StringComparison.OrdinalIgnoreCase) || !group.Files.Contains(c)))
            throw new IOException("Keep one verified copy and select only other files from this group.");
        using var keepGuard = NativeFile.Guard(Path.GetDirectoryName(LocalPath(keeper.Path))!);
        using var keep = Open(keeper.Path); Check(keeper, keep);
        if (Protected(keeper.Path) || CleanupEngine.IsExcluded(keeper.Path, exclusions)) throw new IOException("Kept copy is now protected or excluded; scan again.");
        var clock = Stopwatch.StartNew(); long last = -250;
        foreach (var item in selected.DistinctBy(c => c.Path, StringComparer.OrdinalIgnoreCase)) {
            if (token.IsCancellationRequested) break;
            journal(new(item.Path, "Pending", "Duplicate recycle requested; retained copy will stay in place", item.Size));
            Outcome result;
            try {
                string path = LocalPath(item.Path);
                if (Protected(path) || CleanupEngine.IsExcluded(path, exclusions)) throw new IOException("Protected or excluded file");
                if (new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Fixed) throw new IOException("USB/removable files are review-only; no permanent-delete fallback.");
                using var guard = NativeFile.Guard(Path.GetDirectoryName(path)!);
                using (var copy = Open(path)) {
                    Check(item, copy); Check(keeper, keep);
                    if (!Equal(keep, copy, token, (done, total) => {
                        if (clock.ElapsedMilliseconds - last < 200) return;
                        last = clock.ElapsedMilliseconds; progress?.Report($"Duplicates · Rechecking {Path.GetFileName(path)} · {done:N0} / {total:N0} bytes");
                    })) throw new IOException("Contents no longer match the kept copy.");
                }
                // Existing shell recycling is path-based; parent and keeper handles remain locked.
                // Revalidate once more immediately before the shell handoff. No permanent fallback.
                var run = PersonalReview.Recycle(Path.GetDirectoryName(path)!, [item], exclusions, token, _ => { }, recycle);
                result = run.Outcomes.SingleOrDefault() ?? new(path, "Skipped", "Cancelled before recycling", 0);
                result = result with { Reason = result.Reason.Replace(item.Path, "selected file", StringComparison.OrdinalIgnoreCase).Replace(keeper.Path, "kept copy", StringComparison.OrdinalIgnoreCase) + " · Kept copy preserved" };
            } catch (OperationCanceledException) { result = new(item.Path, "Skipped", "Cancelled before recycling", 0); }
            catch (Exception e) when (Expected(e)) { result = new(item.Path, "Skipped", e.Message.Replace(item.Path, "selected file", StringComparison.OrdinalIgnoreCase).Replace(keeper.Path, "kept copy", StringComparison.OrdinalIgnoreCase), 0); }
            outcomes.Add(result); journal(result);
        }
        return new(DateTimeOffset.Now, outcomes, null, null, token.IsCancellationRequested);
    }
}



