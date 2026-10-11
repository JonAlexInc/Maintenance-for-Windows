using System.Text.Json;
namespace Maintenance.App;
static class HardwareSpecs
{
    record Entry(string Section,string Property,string Match,string Target,string Source,Dictionary<string,Dictionary<string,string>> Groups);
    static readonly List<Entry> catalog=Read();
    static List<Entry> Read(){try{return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"hardware-specs.json")))??[];}catch(IOException){return [];}}
    internal static void Add(List<SystemFact> facts)
    {
        foreach(var entry in catalog)if(facts.Any(f=>f.Section==entry.Section&&f.Property==entry.Property&&f.Value.Trim().Equals(entry.Match,StringComparison.OrdinalIgnoreCase)))
            foreach(var group in entry.Groups){foreach(var row in group.Value)facts.Add(new(entry.Target,group.Key,row.Key,row.Value));facts.Add(new(entry.Target,group.Key,"Source",entry.Source));}
    }
}
