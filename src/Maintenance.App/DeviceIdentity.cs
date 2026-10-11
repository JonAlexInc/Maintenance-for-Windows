using System.Text.RegularExpressions;
namespace Maintenance.App;
static class DeviceIdentity
{
    static readonly Dictionary<string,string> vendors=new(StringComparer.OrdinalIgnoreCase), devices=new(StringComparer.OrdinalIgnoreCase), subsystems=new(StringComparer.OrdinalIgnoreCase);
    static DeviceIdentity()
    {
        try {
            string vendor="",device="";
            foreach(string line in File.ReadLines(Path.Combine(AppContext.BaseDirectory,"pci.ids"))) {
                if(Regex.IsMatch(line,@"^[0-9a-f]{4}  ")) {vendor=line[..4];vendors[vendor]=line[6..];device="";}
                else if(Regex.IsMatch(line,@"^\t[0-9a-f]{4}  ")) {device=line.Substring(1,4);devices[vendor+device]=line[7..];}
                else if(Regex.IsMatch(line,@"^\t\t[0-9a-f]{4} [0-9a-f]{4}  ")) subsystems[vendor+device+line.Substring(2,4)+line.Substring(7,4)]=line[13..];
            }
        } catch(IOException) { }
    }
    internal static List<(string Label,string Value)> Lookup(string id)
    {
        var rows=new List<(string,string)>();
        var pci=Regex.Match(id,@"VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})(?:&SUBSYS_([0-9A-F]{4})([0-9A-F]{4}))?",RegexOptions.IgnoreCase);
        if(pci.Success) {
            string v=pci.Groups[1].Value,d=pci.Groups[2].Value,s=pci.Groups[3].Value,b=pci.Groups[4].Value;
            if(vendors.TryGetValue(v,out var vendor))rows.Add(("Chip vendor (PCI ID)",vendor));
            if(devices.TryGetValue(v+d,out var model))rows.Add(("Chip model (PCI ID)",model));
            if(vendors.TryGetValue(b,out var board))rows.Add(("Board vendor (subsystem ID)",board));
            if(subsystems.TryGetValue(v+d+b+s,out var sub))rows.Add(("Board model (database)",sub));
            rows.Add(("PCI identifiers",$"{v}:{d}"+(b.Length>0?$" · Board {b}:{s}":"")));
        }
        var usb=Regex.Match(id,@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})",RegexOptions.IgnoreCase);
        if(usb.Success)rows.Add(("USB vendor / product ID",$"{usb.Groups[1].Value}:{usb.Groups[2].Value}"));
        return rows;
    }
}
