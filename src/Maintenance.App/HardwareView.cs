using System.Text.RegularExpressions;
namespace Maintenance.App;
static class HardwareView
{
    internal static List<SystemFact> Prepare(IEnumerable<SystemFact> raw)
    {
        var facts=raw.ToList();var output=facts.Where(f=>f.Section is not ("Mouse" or "Keyboard")).ToList();
        var inputs=facts.Where(f=>f.Section is "Mouse" or "Keyboard").GroupBy(f=>(f.Section,f.Component)).Select(g=>g.ToList());
        string V(List<SystemFact> f,string p)=>f.FirstOrDefault(x=>x.Property==p)?.Value.Trim()??"";
        string Key(List<SystemFact> f){string c=V(f,"ContainerId");return c.Length>0&&!c.Contains("00000000-0000")?c:V(f,"ParentDeviceID") is {Length:>0} parent?parent:V(f,"PNPDeviceID");}
        foreach(var cluster in inputs.GroupBy(Key,StringComparer.OrdinalIgnoreCase)) {
            var all=cluster.SelectMany(x=>x).ToList();var names=all.Where(f=>f.Property is "BusReportedName" or "ParentReportedName").Select(f=>f.Value).Distinct().ToArray();
            bool virtualDevice=names.Any(n=>Regex.IsMatch(n,"LGVir|Virtual",RegexOptions.IgnoreCase));
            string friendly=names.FirstOrDefault(n=>Regex.IsMatch(n,"keyboard|mouse|trackball|touchpad|MX Master|MX Anywhere",RegexOptions.IgnoreCase))??names.FirstOrDefault(n=>!Regex.IsMatch(n,"Service|HID|Input Device",RegexOptions.IgnoreCase))??"";
            string section=virtualDevice?"Input interfaces":Regex.IsMatch(friendly,"keyboard",RegexOptions.IgnoreCase)?"Keyboard":cluster.Any(f=>f[0].Section=="Mouse")?"Mouse":"Keyboard";
            string title=virtualDevice?"Virtual input device":friendly.Length>0?friendly:section+" · model not reported";
            output.Add(new(section,cluster.Key,"Name",title));output.Add(new(section,cluster.Key,"Interface count",all.Count(f=>f.Property=="PNPDeviceID").ToString()));
            output.Add(new(section,cluster.Key,"Identification",friendly.Length>0?"Grouped by Windows device container / parent":"Windows interface group; physical model unresolved"));
            foreach(var p in new[]{"PNPDeviceID","ParentDeviceID","ContainerId"}){string value=V(all,p);if(value.Length>0)output.Add(new(section,cluster.Key,p,value));}
        }
        if(output.Any(f=>f.Section=="Connected displays"))output.RemoveAll(f=>f.Section=="Displays");
        var modules=facts.Where(f=>f.Section=="Memory modules"&&f.Property=="Capacity"&&long.TryParse(f.Value,out _)).ToArray();
        long Sum(string section,string prop)=>facts.Where(f=>f.Section==section&&f.Property==prop).Sum(f=>long.TryParse(f.Value,out var n)?n:0);
        if(modules.Length>0) {
            long slots=Sum("Memory arrays","MemoryDevices");
            foreach(string section in new[]{"Memory overview","Motherboard overview"}) {
                output.Add(new(section,"Installed memory","Installed",$"{modules.Sum(f=>long.Parse(f.Value))/(1024L*1024*1024)} GB"));
                output.Add(new(section,"Installed memory","DIMM occupancy",slots>0?$"{modules.Length} of {slots} populated · {Math.Max(0,slots-modules.Length)} free":"Slot count not reported"));
                long capacity=Sum("Memory arrays","MaxCapacityEx");if(capacity==0)capacity=Sum("Memory arrays","MaxCapacity");
                if(capacity>0)output.Add(new(section,"Installed memory","Firmware-reported ceiling",$"{capacity/(1024*1024)} GB · not a compatibility guarantee"));
            }
            output.Add(new("Memory overview","Installed memory","Active timings / channels","Not measured. Rated profiles below are not live settings."));
        }
        return output;
    }
    internal static string GpuTitle(string processor,string id,string fallback)
    {
        string vendor=DeviceIdentity.Lookup(id).FirstOrDefault(r=>r.Label=="Board vendor (subsystem ID)").Value??"";
        vendor=vendor.Contains("ASUSTeK",StringComparison.OrdinalIgnoreCase)?"ASUS":vendor.Contains("Micro-Star",StringComparison.OrdinalIgnoreCase)?"MSI":vendor.Contains("Gigabyte",StringComparison.OrdinalIgnoreCase)?"Gigabyte":"";
        string model=Regex.Replace(processor,@"^NVIDIA\s+","",RegexOptions.IgnoreCase);
        return model.Length>0?$"{vendor} {model}".Trim():fallback;
    }
}
