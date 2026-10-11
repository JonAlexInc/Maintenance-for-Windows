using Maintenance.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

if (args.Length == 2 && args[0] == "--inspect-duplicates") {
    using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var observed = DuplicateCatalog.Scan([args[1]], [], limit.Token);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { observed.Entries, observed.Skipped, observed.Partial, Groups = observed.Groups.Count, Copies = observed.Groups.Sum(g => g.Files.Count), Reasons = observed.Issues.GroupBy(f => f.Reason).Select(g => new { Reason = g.Key, Count = g.Count() }) }));
    return;
}
var basePath = Path.GetFullPath(Path.Combine(".test-data", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(basePath);
int passed = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); passed++; }
string OldFile(string name)
{
    var p = Path.GetFullPath(Path.Combine(basePath, name)); Directory.CreateDirectory(Path.GetDirectoryName(p)!);
    File.WriteAllText(p, "fixture data"); File.SetCreationTimeUtc(p, DateTime.UtcNow.AddDays(-10)); File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddDays(-10)); return p;
}
var root = Path.Combine(basePath, "allowed"); Directory.CreateDirectory(root);
var engine = new CleanupEngine(root);
var old = OldFile("allowed/old.tmp"); var excluded = OldFile("allowed/keep/old.tmp");
var recent = Path.Combine(root, "recent.tmp"); File.WriteAllText(recent, "new");
var outside = OldFile("outside/valuable.txt");
var outcomes = new List<Outcome>();
var scan = engine.Scan([Path.GetDirectoryName(excluded)!], CancellationToken.None);

Check(scan.Candidates.Count == 1 && scan.Candidates[0].Path == old, "only old non-excluded file eligible");
Check(File.Exists(old), "scan makes no deletions");
Check(scan.Skipped.Any(x => x.Path == recent), "recent file skipped");
Check(!engine.Contains(Path.Combine(basePath, "allowed-sibling", "x")), "sibling prefix is outside allowed root");
var cleaned = engine.Clean(scan.Candidates, [], CancellationToken.None, outcomes.Add);
Check(!File.Exists(old) && File.Exists(excluded) && File.Exists(outside), "selected cleanup preserves excluded and outside files");
Check(cleaned.Outcomes.Single().Status == "Deleted" && outcomes[0].Status == "Pending", "intent journal precedes confirmed deletion");

var changed = OldFile("allowed/changed.tmp"); scan = engine.Scan([], CancellationToken.None); var candidate = scan.Candidates.Single(c => c.Path == changed);
File.AppendAllText(changed, "modified");
cleaned = engine.Clean([candidate], [], CancellationToken.None, outcomes.Add);
Check(File.Exists(changed) && cleaned.Outcomes.Single().Status == "Skipped", "changed file preserved");
var replaced = OldFile("allowed/replaced.tmp"); candidate = engine.Scan([], CancellationToken.None).Candidates.Single(c => c.Path == replaced);
File.Move(replaced, replaced + ".original"); OldFile("allowed/replaced.tmp");
cleaned = engine.Clean([candidate], [], CancellationToken.None, outcomes.Add);
Check(File.Exists(replaced) && cleaned.Outcomes.Single().Status == "Skipped", "replacement at reviewed path preserved");
var locked = OldFile("allowed/locked.tmp"); candidate = engine.Scan([], CancellationToken.None).Candidates.Single(c => c.Path == locked);
using (var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
{
    cleaned = engine.Clean([candidate], [], CancellationToken.None, outcomes.Add);
    Check(cleaned.Outcomes.Single().Status == "Skipped", "locked file preserved");
}
candidate = engine.Scan([], CancellationToken.None).Candidates.Single(c => c.Path == locked);
cleaned = engine.Clean([candidate], [locked], CancellationToken.None, outcomes.Add);
Check(File.Exists(locked) && cleaned.Outcomes.Single().Status == "Skipped", "new execution-time exclusion honored");
using (var stop = new CancellationTokenSource())
{
    stop.Cancel(); cleaned = engine.Clean([candidate], [], stop.Token, outcomes.Add);
    Check(cleaned.Cancelled && cleaned.Outcomes.Count == 0 && File.Exists(locked), "cancellation prevents remaining work");
}
try { engine.Clean([candidate], [], CancellationToken.None, _ => throw new IOException("journal unavailable")); } catch (IOException) { }
Check(File.Exists(locked), "journal failure blocks deletion");
var outsideCandidate = new CleanupEngine(Path.GetDirectoryName(outside)!).Scan([], CancellationToken.None).Candidates.Single();
cleaned = engine.Clean([outsideCandidate], [], CancellationToken.None, outcomes.Add);
Check(File.Exists(outside) && cleaned.Outcomes.Single().Status == "Skipped", "forged selection outside root rejected");

// Junction creation does not require symbolic-link privileges. Fixtures remain for inspection.
var link = Path.Combine(root, "redirect");
var proc = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{Path.GetDirectoryName(outside)}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
proc!.WaitForExit(); if (proc.ExitCode != 0) throw new Exception("Cannot create junction fixture");
scan = engine.Scan([], CancellationToken.None);
Check(scan.Skipped.Any(s => s.Path == link) && scan.Candidates.All(c => !c.Path.StartsWith(link)), "junction not traversed during scan");
var swapFile = OldFile("allowed/swap/file.tmp"); candidate = engine.Scan([], CancellationToken.None).Candidates.Single(c => c.Path == swapFile);
Directory.Move(Path.GetDirectoryName(swapFile)!, Path.Combine(root, "swap-original"));
proc = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{Path.GetDirectoryName(swapFile)}\" \"{Path.GetDirectoryName(outside)}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }); proc!.WaitForExit();
if (proc.ExitCode != 0) throw new Exception("Cannot create swapped junction fixture");
cleaned = engine.Clean([candidate], [], CancellationToken.None, outcomes.Add);
Check(cleaned.Outcomes.Single().Status == "Skipped" && File.Exists(outside), "parent swapped to junction rejected");
var multi = OldFile("allowed/multiple.tmp");
if (!CreateHardLinkW(Path.Combine(basePath, "outside", "linked.txt"), multi, IntPtr.Zero)) throw new Exception("Cannot create hardlink fixture");
scan = engine.Scan([], CancellationToken.None);
Check(scan.Candidates.All(c => c.Path != multi), "multiply linked files skipped");
var store = new LocalStore(Path.Combine(basePath, "records"));
store.SaveExclusions([excluded]); Check(store.Exclusions().SequenceEqual([excluded]), "exclusions persist");
string reportFile;
using (var journal = store.StartJournal()) { reportFile = journal.Name; LocalStore.Append(journal, new Outcome(outside, "Skipped", "Excluded", 0)); }
Check(!LocalStore.ReadReport(reportFile, false).Contains(basePath) && LocalStore.ReadReport(reportFile, true).Contains(outside), "report paths omitted by default and opt-in works");
File.AppendAllText(reportFile, "{truncated\n"); Check(LocalStore.ReadReport(reportFile, false).Contains("Incomplete record"), "truncated journal reports unknown outcome");
File.WriteAllText(Path.Combine(store.Folder, "exclusions.json"), "invalid");
bool rejected = false; try { store.Exclusions(); } catch { rejected = true; }
Check(rejected, "corrupt exclusions fail closed");
var largeRoot = Path.Combine(basePath, "large-scan"); Directory.CreateDirectory(largeRoot);
for (int n = 0; n < 20001; n++) File.WriteAllText(Path.Combine(largeRoot, $"{n}.tmp"), "");
var largeEngine = new CleanupEngine(largeRoot);
var bounded = largeEngine.Scan([], CancellationToken.None, maxEntries: 25);
Check(bounded.Limited && bounded.Skipped.Count == 25, "configured scan limit reports partial coverage");
var complete = largeEngine.Scan([], CancellationToken.None);
Check(!complete.Limited && complete.Skipped.Count == 20001, "default scan continues past former 20000 entry cutoff");
var timed = largeEngine.Scan([], CancellationToken.None, maxDuration: TimeSpan.FromTicks(1));
Check(timed.Limited && !timed.Cancelled, "max wait reports a partial scan distinctly from cancellation");
var cancelledScan = largeEngine.Scan([], new CancellationToken(true));
Check(cancelledScan.Cancelled && !cancelledScan.Limited, "manual scan cancellation remains available without a time limit");
var sourceA=OldFile("sources/a/old.tmp");var sourceB=OldFile("sources/b/old.tmp");var keepSource=OldFile("sources/unchecked/keep.tmp");
var multiEngine=new MultiCleanupEngine([Path.GetDirectoryName(sourceA)!,Path.GetDirectoryName(sourceB)!,Path.GetDirectoryName(sourceA)!]);
var multiScan=multiEngine.Scan([],CancellationToken.None);
Check(multiScan.Candidates.Count==2&&multiScan.Candidates.All(c=>c.Path!=keepSource),"multiple checked sources only; duplicate roots deduplicated");
var singleSource=new MultiCleanupEngine([Path.GetDirectoryName(sourceA)!]);
var rejectedCross=singleSource.Clean(multiScan.Candidates.Where(c=>c.Path==sourceB),[],CancellationToken.None,_=>{});
Check(rejectedCross.Outcomes.Single().Status=="Skipped"&&File.Exists(sourceB),"candidate from unchecked root cannot be deleted");
File.AppendAllText(sourceB,"changed");var multiDeleted=multiEngine.Clean(multiScan.Candidates,[],CancellationToken.None,_=>{});
Check(!File.Exists(sourceA)&&File.Exists(sourceB)&&File.Exists(keepSource),"multi-source cleanup revalidates changes and preserves unchecked source");
var nested=OldFile("sources/b/nested/data.tmp");var overlap=new MultiCleanupEngine([Path.GetDirectoryName(sourceB)!,Path.GetDirectoryName(nested)!]);
Check(overlap.Scan([],CancellationToken.None).Candidates.Count(c=>c.Path==nested)==1,"overlapping cleanup roots do not duplicate files");
Check(multiEngine.Scan([],CancellationToken.None,maxDuration:TimeSpan.FromTicks(1)).Limited,"multi-source max wait is shared across sources");
Check(multiEngine.Scan([],new CancellationToken(true)).Cancelled,"multi-source cancellation reports partial coverage");
bool emptyRejected=false;try{_ = new MultiCleanupEngine([]);}catch(ArgumentException){emptyRejected=true;}Check(emptyRejected,"no checked sources fails closed");
var adsRoot=Path.Combine(basePath,"streams");Directory.CreateDirectory(adsRoot);
var adsFile=Path.Combine(adsRoot,"document.txt");File.WriteAllText(adsFile,"original");
// PowerShell supports named streams directly; avoid FileStream's colon validation.
var adsScript=Path.Combine(basePath,"create-streams.ps1");
File.WriteAllText(adsScript,"param($File,$Folder)\nSet-Content -LiteralPath $File -Stream Zone.Identifier -Value '[ZoneTransfer]'\nSet-Content -LiteralPath $File -Stream custom -Value 'test metadata'\nSet-Content -LiteralPath $Folder -Stream foldernote -Value 'folder metadata'\n");
var adsStart=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};
foreach(var arg in new[]{"-NoProfile","-File",adsScript,adsFile,adsRoot})adsStart.ArgumentList.Add(arg);
using(var adsProcess=Process.Start(adsStart)!){adsProcess.WaitForExit();Check(adsProcess.ExitCode==0,"ADS fixtures created");}
Check(StreamCatalog.IsNtfs(adsRoot,out _),"NTFS capability verified");
var ads=StreamCatalog.Read(adsRoot,CancellationToken.None);
Check(ads.Candidates.Count==3&&ads.Skipped.Count==0,"named file and directory streams enumerated; default data omitted");
Check(ads.Candidates.Any(c=>StreamCatalog.StreamName(c.Path)=="Zone.Identifier"&&StreamCatalog.OwnerPath(c.Path)==adsFile&&c.Size>0),"stream name, owner, and size reported");
Check(File.ReadAllText(adsFile)=="original"&&StreamCatalog.Read(adsRoot,CancellationToken.None).Candidates.Count==3,"stream scan preserves files and metadata");
Check(StreamCatalog.Read(adsRoot,new CancellationToken(true)).Cancelled,"stream cancellation keeps partial state");
Check(StreamCatalog.Read(adsRoot,CancellationToken.None,TimeSpan.FromTicks(1)).Limited,"stream max wait marks partial state");
Check(!StreamCatalog.IsNtfs(@"Z:\missing-volume-that-does-not-exist",out _)||Directory.Exists(@"Z:\"),"unavailable volumes do not silently pass validation");
var reviewRoot=Path.Combine(basePath,"personal");Directory.CreateDirectory(reviewRoot);
var reviewFile=Path.Combine(reviewRoot,"test.txt");File.WriteAllText(reviewFile,"keep");var reviewItem=PersonalReview.Snapshot(reviewFile);int recycleCalls=0;
var reviewResult=PersonalReview.Recycle(reviewRoot,[reviewItem],[],CancellationToken.None,_=>{},_=>recycleCalls++);
Check(recycleCalls==1&&reviewResult.Outcomes.Single().Status=="Recycled","reviewed personal file reaches recycle adapter");
File.AppendAllText(reviewFile,"changed");reviewResult=PersonalReview.Recycle(reviewRoot,[reviewItem],[],CancellationToken.None,_=>{},_=>recycleCalls++);
Check(recycleCalls==1&&reviewResult.Outcomes.Single().Status=="Skipped","changed personal file cannot be recycled");
reviewItem=PersonalReview.Snapshot(reviewFile);reviewResult=PersonalReview.Recycle(reviewRoot,[reviewItem],[reviewFile],CancellationToken.None,_=>{},_=>recycleCalls++);
Check(recycleCalls==1&&reviewResult.Outcomes.Single().Status=="Skipped","excluded personal file cannot be recycled");
reviewResult=PersonalReview.Recycle(reviewRoot,[outsideCandidate],[],CancellationToken.None,_=>{},_=>recycleCalls++);
Check(recycleCalls==1&&reviewResult.Outcomes.Single().Status=="Skipped","outside personal scan scope cannot be recycled");
reviewResult=PersonalReview.Recycle(reviewRoot,[reviewItem],[],new CancellationToken(true),_=>{},_=>recycleCalls++);
Check(recycleCalls==1&&reviewResult.Cancelled,"cancelled personal recycle does not invoke shell");
try{PersonalReview.Recycle(reviewRoot,[reviewItem],[],CancellationToken.None,_=>throw new IOException("journal failed"),_=>recycleCalls++);}catch(IOException){}
Check(recycleCalls==1&&File.Exists(reviewFile),"personal recycle journal failure preserves file");
var auditStore=new LocalStore(Path.Combine(basePath,"audit"));
using(var audit=auditStore.StartJournal()) {
 LocalStore.Append(audit,new{Summary="Scan fixture",Kind="Temporary files",Location=root});
 LocalStore.Append(audit,new Outcome("deleted.tmp","Pending","Requested",123));
 LocalStore.Append(audit,new Outcome("deleted.tmp","Deleted","Completed",123));
 LocalStore.Append(audit,new Outcome("recycled.txt","Recycled","Completed",456));
 LocalStore.Append(audit,new Outcome("unknown.tmp","Pending","Requested",78));
 LocalStore.Append(audit,new Outcome("skipped.tmp","Pending","Requested",99));
 LocalStore.Append(audit,new Outcome("skipped.tmp","Skipped","Changed",0));
}
var auditRead=HistoryCatalog.Read(auditStore.History());
Check(auditRead.Files.Count==4&&auditRead.Files.Count(f=>f.Status=="Deleted")==1,"history collapses pending/final pairs without double counting");
Check(auditRead.Files.Single(f=>f.Path=="unknown.tmp").Status=="Unknown","unmatched pending remains unknown, never deleted");
Check(auditRead.Files.Single(f=>f.Path=="skipped.tmp").Size==99,"skipped history preserves known requested size");
Check(auditRead.Scans.Single().Location==root&&auditRead.Scans.Single().At.Year==DateTime.Now.Year,"scan source and journal timestamp preserved");
File.AppendAllText(auditStore.History().First(),"{broken\n");
Check(HistoryCatalog.Read(auditStore.History()).Unreadable==1,"truncated audit records report incomplete coverage");
for(int n=0;n<31;n++){using var retained=auditStore.StartJournal();LocalStore.Append(retained,new{Summary="Scan retention fixture"});}
Check(auditStore.History().Count()==32,"history no longer silently purges after 30 runs");
var streamText=StreamPreview.Read(adsFile+":Zone.Identifier:$DATA");
Check(streamText.Text.Contains("ZoneTransfer")&&!streamText.Truncated&&!streamText.Binary,"named stream text preview reads without execution");
File.WriteAllBytes(adsFile+":large-preview",new byte[100000]);
var binaryPreview=StreamPreview.Read(adsFile+":large-preview:$DATA");
Check(binaryPreview.Bytes.Length==65536&&binaryPreview.Size==100000&&binaryPreview.Truncated&&binaryPreview.Binary,"binary stream preview is capped at 64 KiB");
Check(binaryPreview.Hex().StartsWith("00000000")&&binaryPreview.Hex().Contains("0000FFF0"),"hex preview offsets cover displayed bytes");
bool normalRefused=false;try{StreamPreview.Read(adsFile);}catch(IOException){normalRefused=true;}Check(normalRefused,"stream viewer rejects ordinary-file targets");
var olderPoint=new RestoreEntry(4,"Before driver","2026-10-01T00:00:00.0000000Z",10);
Check(RestoreReview.NormalizeDate("20261010073123.123456-300")=="2026-10-10T12:31:23.1234560Z","DMTF negative offset and microseconds");
Check(RestoreReview.NormalizeDate("20261010073123.000000+330")=="2026-10-10T02:01:23.0000000Z","DMTF positive offset");
Check(RestoreReview.NormalizeDate(olderPoint.Created)==olderPoint.Created,"ISO restore time retained");
Check(RestoreReview.Refusal(olderPoint,[olderPoint,new RestoreEntry(6,"Unknown","invalid",0)])!=null,"unknown creation time blocks removal");
var newerPoint=new RestoreEntry(5,"Before update","2026-10-09T00:00:00.0000000Z",12);
Check(RestoreReview.Refusal(newerPoint,[olderPoint,newerPoint])!=null,"newest restore point cannot be removed");
Check(RestoreReview.Refusal(olderPoint,[olderPoint])!=null,"last restore point cannot be removed");
Check(RestoreReview.Refusal(olderPoint,[olderPoint,newerPoint])==null,"reviewed older restore point can be eligible");
Check(RestoreReview.Refusal(olderPoint,[olderPoint with{Description="Changed"},newerPoint])!=null,"changed restore-point identity is rejected");
Check(RestoreReview.Refusal(olderPoint,[newerPoint])!=null,"missing restore point is rejected");
foreach(string extension in new[]{"vhd","vhdx","vmdk","vdi","qcow","qcow2","qed","avhd","avhdx","ova","ovf","vmx","vmcx","vmrs","vmsn"})Check(VirtualMachineCatalog.Kind("test."+extension)!=null,"VM format recognized: "+extension);
Check(VirtualMachineCatalog.Kind("user.img")==null&&VirtualMachineCatalog.Kind("user.img",true)!=null,"ambiguous raw disks require explicit opt-in");
var vmRoot=Path.Combine(basePath,"vm-scan");Directory.CreateDirectory(vmRoot);File.WriteAllText(Path.Combine(vmRoot,"disk.vhdx"),"fixture virtual disk metadata only");File.WriteAllText(Path.Combine(vmRoot,"photo.jpg"),"personal");
var vmScan=VirtualMachineCatalog.Scan([vmRoot],false,CancellationToken.None);
Check(vmScan.Files.Count==1&&vmScan.Files[0].Category=="Virtual disk"&&File.Exists(Path.Combine(vmRoot,"photo.jpg")),"VM scan finds format without modifying files");
using(var vmCancel=new CancellationTokenSource()){vmCancel.Cancel();Check(VirtualMachineCatalog.Scan([vmRoot],false,vmCancel.Token).Partial,"cancelled VM scan reports partial coverage");}
passed += DuplicateChecks.Run(basePath);
passed += AppDataChecks.Run(basePath);
Console.WriteLine($"\n{passed} safety checks passed. Fixtures retained at {basePath}");

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern bool CreateHardLinkW(string name, string target, IntPtr security);










