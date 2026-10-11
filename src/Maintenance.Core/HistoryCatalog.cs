using System.Globalization;
using System.Text.Json;
namespace Maintenance.Core;
public sealed record HistoryFile(DateTimeOffset At,string Path,string Status,long? Size,string Reason,string Journal);
public sealed record HistoryScan(DateTimeOffset At,string Kind,string Location,string Summary,string Journal);
public sealed record HistoryCatalog(List<HistoryScan> Scans,List<HistoryFile> Files,int Unreadable)
{
    public static HistoryCatalog Read(IEnumerable<string> paths)
    {
        var scans=new List<HistoryScan>();var files=new List<HistoryFile>();int unreadable=0;
        foreach(string path in paths) {
            string name=System.IO.Path.GetFileName(path);DateTimeOffset fallback=DateTimeOffset.MinValue;
            if(name.Length>=18)DateTimeOffset.TryParseExact(name[..18],"yyyyMMdd'T'HHmmssfff",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out fallback);
            var outcomes=new Dictionary<string,HistoryFile>(StringComparer.OrdinalIgnoreCase);
            try {
                foreach(string line in File.ReadLines(path)) {
                    try {
                        using var doc=JsonDocument.Parse(line);var r=doc.RootElement;
                        string Text(string field)=>r.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
                        DateTimeOffset at=DateTimeOffset.TryParse(Text("RecordedAt"),out var timestamp)?timestamp:fallback;
                        if(r.TryGetProperty("Status",out _)) {
                            string file=Text("Path"),status=Text("Status");if(file.Length==0)continue;
                            long? size=r.TryGetProperty("Bytes",out var bytes)&&bytes.TryGetInt64(out var n)&&(n>0||status is "Deleted" or "Recycled")?n:null;
                            if(size is null&&outcomes.TryGetValue(file,out var earlier))size=earlier.Size;
                            outcomes[file]=new(at,file,status=="Pending"?"Unknown":status,size,status=="Pending"?"Requested, but no final outcome was recorded":Text("Reason"),path);
                        }else {
                            string summary=Text("Summary");
                            if(summary.StartsWith("Scan",StringComparison.OrdinalIgnoreCase)||summary.StartsWith("Read-only NTFS",StringComparison.OrdinalIgnoreCase)) {
                                string kind=Text("Kind");if(kind.Length==0)kind=summary.StartsWith("Read-only NTFS")?"Alternate data streams":summary.StartsWith("Scan local drive")?"Personal folder":"Temporary files";
                                scans.Add(new(at,kind,Text("Location"),summary,path));
                            }
                        }
                    }catch(JsonException){unreadable++;}
                }
            }catch(IOException){unreadable++;}catch(UnauthorizedAccessException){unreadable++;}
            files.AddRange(outcomes.Values);
        }
        return new(scans.OrderByDescending(s=>s.At).ToList(),files.OrderByDescending(f=>f.At).ToList(),unreadable);
    }
}
