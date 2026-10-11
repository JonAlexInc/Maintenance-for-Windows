using System.Text;
using System.Text.Json;

namespace Maintenance.Core;

public sealed class LocalStore
{
    public string Folder { get; }
    public LocalStore(string folder) { Folder = folder; Directory.CreateDirectory(folder); }
    public List<string> Exclusions()
    {
        var path = Path.Combine(Folder, "exclusions.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? throw new IOException("Invalid exclusions") : [];
    }
    public void SaveExclusions(IEnumerable<string> paths)
    {
        var temp = Path.Combine(Folder, "exclusions.new");
        File.WriteAllText(temp, JsonSerializer.Serialize(paths));
        File.Move(temp, Path.Combine(Folder, "exclusions.json"), true);
    }
    public FileStream StartJournal()
    {
        var path = Path.Combine(Folder, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".jsonl");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        return stream;
    }
    public static void Append(FileStream stream, object entry)
    {
        var record=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(entry))!;
        record["RecordedAt"]=JsonSerializer.SerializeToElement(DateTimeOffset.Now);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record) + "\n");
        stream.Write(bytes); stream.Flush(true);
    }
    public IEnumerable<string> History() => Directory.GetFiles(Folder, "*.jsonl").OrderDescending();
    public static string ReadReport(string file, bool includePaths)
    {
        var b = new StringBuilder("Maintenance for Windows — 0.01 Preview\r\nLocal activity record\r\n\r\n");
        foreach (var line in File.ReadLines(file))
        {
            try
            {
                using var doc = JsonDocument.Parse(line); var r = doc.RootElement;
                if (r.TryGetProperty("Status", out var status))
                {
                    b.Append(status.GetString()).Append(": ").Append(r.GetProperty("Reason").GetString());
                    if (includePaths) b.Append(" | ").Append(r.GetProperty("Path").GetString());
                    if (r.TryGetProperty("Bytes", out var bytes) && bytes.GetInt64() > 0) b.Append(" | logical bytes: ").Append(bytes.GetInt64());
                    b.AppendLine();
                }
                else if (r.TryGetProperty("Summary", out var summary)) b.AppendLine(summary.GetString());
            }
            catch (JsonException) { b.AppendLine("Incomplete record: an operation's outcome may be unknown."); }
        }
        b.AppendLine("\r\nPending entries without a later outcome are unknown; they are not proof of deletion.");
        b.AppendLine("File sizes are logical sizes, not a guarantee of reclaimed space. Other processes can change free space.");
        if (!includePaths) b.AppendLine("File paths, machine name, and user name are omitted.");
        return b.ToString();
    }
}
