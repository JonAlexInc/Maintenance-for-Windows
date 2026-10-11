using Maintenance.Core;
using System.Runtime.InteropServices;

static class DuplicateChecks
{
    public static int Run(string parent)
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("Duplicate check failed: " + name); count++; Console.WriteLine("PASS duplicate: " + name); }
        string root = Directory.CreateDirectory(Path.Combine(parent, "duplicates")).FullName;
        string a = Path.Combine(root, "original.bin"), b = Path.Combine(root, "renamed.bin"), c = Path.Combine(root, "third.bin");
        byte[] bytes = Enumerable.Range(0, 400_000).Select(i => (byte)(i % 251)).ToArray();
        File.WriteAllBytes(a, bytes); File.WriteAllBytes(b, bytes); File.WriteAllBytes(c, bytes);
        var different = bytes.ToArray(); different[^1] ^= 1; File.WriteAllBytes(Path.Combine(root, "different.bin"), different);
        var middleDifferent = bytes.ToArray(); middleDifferent[200_000] ^= 1; File.WriteAllBytes(Path.Combine(root, "same-samples-different-middle.bin"), middleDifferent);
        File.WriteAllText(Path.Combine(root, "empty1"), ""); File.WriteAllText(Path.Combine(root, "empty2"), "");
        string sub = Directory.CreateDirectory(Path.Combine(root, "sub")).FullName;
        var scan = DuplicateCatalog.Scan([root, sub, root], [], CancellationToken.None);
        Check(scan.Groups.Count == 1 && scan.Groups[0].Files.Count == 3 && scan.Groups[0].ExtraBytes == 800_000, "exact bytes, differing names, same-size non-match and empty files");
        Check(scan.Groups.SelectMany(g => g.Files).Select(f => f.Path).Distinct().Count() == 3, "overlapping roots do not duplicate paths");
        Check(DuplicateCatalog.Scan([root], [b], CancellationToken.None).Groups.Single().Files.Count == 2, "exclusions honored before hashing");
        var group = scan.Groups[0]; var keeper = group.Files.Single(f => f.Path == a); var chosen = group.Files.Single(f => f.Path == b);
        bool refused = false; try { DuplicateCatalog.Recycle(group, keeper, [keeper], [], CancellationToken.None, _ => { }, _ => throw new Exception("must not recycle")); } catch (IOException) { refused = true; }
        Check(refused, "cannot select kept copy");
        refused = false; try { DuplicateCatalog.Recycle(group, keeper, [chosen with { Path = Path.Combine(root, "different.bin") }], [], CancellationToken.None, _ => { }, _ => throw new Exception("must not recycle")); } catch (IOException) { refused = true; }
        Check(refused, "foreign/forged selection refused");
        var excluded = DuplicateCatalog.Recycle(group, keeper, [chosen], [b], CancellationToken.None, _ => { }, _ => throw new Exception("must not recycle"));
        Check(excluded.Outcomes.Single().Status == "Skipped" && File.Exists(b), "new exclusions block recycling");
        File.WriteAllBytes(b, different);
        var changed = DuplicateCatalog.Recycle(group, keeper, [chosen], [], CancellationToken.None, _ => { }, _ => throw new Exception("must not recycle"));
        Check(changed.Outcomes.Single().Status == "Skipped", "changed selected copy rejected");
        File.WriteAllBytes(b, bytes);
        scan = DuplicateCatalog.Scan([root], [], CancellationToken.None); group = scan.Groups.Single(); keeper = group.Files.Single(f => f.Path == a); chosen = group.Files.Single(f => f.Path == b);
        File.WriteAllBytes(a, different); refused = false;
        try { DuplicateCatalog.Recycle(group, keeper, [chosen], [], CancellationToken.None, _ => { }, _ => throw new Exception("must not recycle")); } catch (IOException) { refused = true; }
        Check(refused && File.Exists(b), "changed keeper aborts removal"); File.WriteAllBytes(a, bytes);
        File.WriteAllText(b + ":distinct", "extra metadata");
        var ads = DuplicateCatalog.Scan([root], [], CancellationToken.None);
        Check(ads.Groups.Single().Files.Count == 2 && ads.Issues.Any(i => i.Path == b && i.Reason.Contains("streams")), "named stream differences cannot be discarded"); File.Delete(b + ":distinct");
        string link = Path.Combine(root, "hardlink.bin"); if (!CreateHardLinkW(link, b, IntPtr.Zero)) throw new Exception("Hardlink fixture creation failed");
        var links = DuplicateCatalog.Scan([root], [], CancellationToken.None);
        Check(links.Groups.Single().Files.Count == 2 && links.Groups.SelectMany(g => g.Files).All(f => f.Path != b && f.Path != link), "hard links not counted as separate reclaimable copies"); File.Delete(link);
        string protectedDir = Directory.CreateDirectory(Path.Combine(root, "Windows")).FullName; File.WriteAllBytes(Path.Combine(protectedDir, "do-not-touch.bin"), bytes);
        Check(DuplicateCatalog.Scan([root], [], CancellationToken.None).Groups.Single().Files.Count == 3, "protected folder subtree omitted");
        var capped = DuplicateCatalog.Scan([root], [], CancellationToken.None, entryLimit: 2);
        Check(capped.Partial && capped.Entries == 2, "entry limit clearly marks partial coverage");
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Check(DuplicateCatalog.Scan([root], [], stop.Token).Partial, "pre-cancelled scan is partial");
        using var midStop = new CancellationTokenSource();
        var cancelled = DuplicateCatalog.Scan([root], [], midStop.Token, new CallbackProgress(s => { if (s.Contains("Looking")) midStop.Cancel(); }));
        Check(cancelled.Partial, "cancellation during enumeration returns promptly");
        scan = DuplicateCatalog.Scan([root], [], CancellationToken.None); group = scan.Groups.Single(); keeper = group.Files.Single(f => f.Path == a); chosen = group.Files.Single(f => f.Path == b);
        refused = false; try { DuplicateCatalog.Recycle(group, keeper, [chosen], [], CancellationToken.None, _ => throw new IOException("audit unavailable"), _ => throw new Exception("must not recycle")); } catch (IOException) { refused = true; }
        Check(refused && File.Exists(b), "audit failure prevents mutation");
        var events = new List<Outcome>(); bool keeperLocked = false;
        var moved = DuplicateCatalog.Recycle(group, keeper, [chosen, chosen], [], CancellationToken.None, events.Add, path => {
            try { using var write = new FileStream(a, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); } catch (IOException) { keeperLocked = true; }
            File.Move(path, path + ".fixture-recovery");
        });
        Check(keeperLocked && File.Exists(a) && moved.Outcomes.Count == 1 && moved.Outcomes[0].Status == "Recycled" && File.Exists(b + ".fixture-recovery"), "keeper locked throughout recoverable adapter; duplicate requests deduplicated");
        Check(events.All(e => !e.Reason.Contains(root, StringComparison.OrdinalIgnoreCase)), "duplicate outcome prose does not leak paths into redacted reports");
        Check(events.Select(e => e.Status).SequenceEqual(new[] { "Pending", "Recycled" }), "audit intent and final outcome recorded");
        scan = DuplicateCatalog.Scan([root], [], CancellationToken.None); group = scan.Groups.Single(); keeper = group.Files.Single(f => f.Path == a); chosen = group.Files.Single(f => f.Path == c);
        var failure = DuplicateCatalog.Recycle(group, keeper, [chosen], [], CancellationToken.None, _ => { }, _ => throw new IOException("Recycle Bin unavailable"));
        Check(failure.Outcomes.Single().Status == "Skipped" && File.Exists(c), "recycle failure never falls back to permanent deletion");
        var noRemoval = DuplicateCatalog.Recycle(group, keeper, [chosen], [], stop.Token, _ => { }, _ => throw new Exception("must not recycle"));
        Check(noRemoval.Cancelled && noRemoval.Outcomes.Count == 0 && File.Exists(c), "cancelled cleanup preserves copies");
                string deep = Directory.CreateDirectory(Path.Combine(root, "ReadOnlyFolder", "Level2", "Level3")).FullName;
        File.SetAttributes(Path.Combine(root,"ReadOnlyFolder"), FileAttributes.Directory | FileAttributes.ReadOnly);
        File.WriteAllText(Path.Combine(deep,"nested-a.txt"), "unique nested duplicate fixture");
        File.WriteAllText(Path.Combine(root,"nested-b.txt"), "unique nested duplicate fixture");
        var recursive = DuplicateCatalog.Scan([root], [], CancellationToken.None);
        Check(recursive.Groups.Any(g => g.Files.Any(f => f.Path == Path.Combine(deep,"nested-a.txt"))), "recursive scan traverses read-only folder attributes");
        Check(NativeFile.CloudTag(0x9000001A) && NativeFile.CloudTag(0x9000F01A) && !NativeFile.CloudTag(0xA000000C) && !NativeFile.CloudTag(0xA0000003), "only cloud tags accepted; symlinks and junctions remain excluded");
        Check(NativeFile.NeedsRecall((FileAttributes)0x400000) && NativeFile.NeedsRecall(FileAttributes.Offline) && !NativeFile.NeedsRecall((FileAttributes)0x80410), "local cloud residency distinguishes download-required entries");
        return count;
    }
    sealed class CallbackProgress(Action<string> callback) : IProgress<string> { public void Report(string value) => callback(value); }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateHardLinkW(string name, string target, IntPtr security);
}



