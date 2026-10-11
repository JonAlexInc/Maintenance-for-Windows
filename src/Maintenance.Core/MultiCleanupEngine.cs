namespace Maintenance.Core;
public sealed class MultiCleanupEngine
{
    readonly CleanupEngine[] engines;
    public string Root=>string.Join(Environment.NewLine,engines.Select(e=>e.Root));
    public MultiCleanupEngine(IEnumerable<string> roots)
    {
        var all=roots.Select(r=>new CleanupEngine(r)).DistinctBy(e=>e.Root,StringComparer.OrdinalIgnoreCase).ToArray();
        engines=all.Where(e=>!all.Any(other=>other!=e&&other.Contains(e.Root))).ToArray();
        if(engines.Length==0)throw new ArgumentException("Choose at least one cleanup source.");
    }
    public ScanResult Scan(IReadOnlyList<string> exclusions,CancellationToken token,IProgress<int>? progress=null,TimeSpan? maxDuration=null)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();var good=new List<Candidate>();var skipped=new List<Finding>();bool limited=false;
        foreach(var engine in engines){if(token.IsCancellationRequested)break;var remaining=maxDuration-clock.Elapsed;if(remaining<=TimeSpan.Zero){limited=true;break;}
            var result=engine.Scan(exclusions,token,progress,maxDuration:remaining);good.AddRange(result.Candidates);skipped.AddRange(result.Skipped);limited|=result.Limited;if(limited)break;}
        return new(DateTimeOffset.Now,good,skipped,token.IsCancellationRequested,limited);
    }
    public RunResult Clean(IEnumerable<Candidate> selected,IReadOnlyList<string> exclusions,CancellationToken token,Action<Outcome> journal,IProgress<int>? progress=null)
    {
        var results=new List<Outcome>();
        foreach(var group in selected.DistinctBy(c=>c.Path,StringComparer.OrdinalIgnoreCase).GroupBy(c=>engines.FirstOrDefault(e=>e.Contains(c.Path)))) {
            if(token.IsCancellationRequested)break;
            if(group.Key==null){foreach(var item in group){var rejected=new Outcome(item.Path,"Skipped","Outside the checked cleanup sources",0);journal(rejected);results.Add(rejected);}continue;}
            var result=group.Key.Clean(group,exclusions,token,journal);results.AddRange(result.Outcomes);progress?.Report(results.Count);
        }
        return new(DateTimeOffset.Now,results,null,null,token.IsCancellationRequested);
    }
}
