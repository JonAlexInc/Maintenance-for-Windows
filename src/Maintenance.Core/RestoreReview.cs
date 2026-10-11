using System.Globalization;
namespace Maintenance.Core;
public sealed record RestoreEntry(uint Id,string Description,string Created,int Type);
public static class RestoreReview
{
    public static string NormalizeDate(string value)
    {
        // WMI SystemRestore exposes a DMTF string, including its UTC offset in minutes.
        if(value.Length==25 && value[14]=='.' && (value[21]=='+'||value[21]=='-') &&
           DateTime.TryParseExact(value[..21],"yyyyMMddHHmmss.ffffff",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date) &&
           int.TryParse(value[22..],NumberStyles.None,CultureInfo.InvariantCulture,out int minutes) && minutes<=840)
            return new DateTimeOffset(date,TimeSpan.FromMinutes(value[21]=='-'?-minutes:minutes)).UtcDateTime.ToString("o",CultureInfo.InvariantCulture);
        if(DateTimeOffset.TryParseExact(value,"o",CultureInfo.InvariantCulture,DateTimeStyles.None,out var iso))
            return iso.UtcDateTime.ToString("o",CultureInfo.InvariantCulture);
        throw new FormatException("Windows returned an unrecognized restore-point creation time. No restore points were changed.");
    }
    // Re-check against a freshly queried inventory. Never remove the newest recovery option.
    public static string? Refusal(RestoreEntry selected,IReadOnlyList<RestoreEntry> current)
    {
        var match=current.FirstOrDefault(p=>p.Id==selected.Id);
        if(match is null)return "Restore point no longer exists";
        if(match!=selected)return "Restore point changed since review; scan again";
        RestoreEntry newest;
        try { newest=current.OrderByDescending(p=>NormalizeDate(p.Created),StringComparer.Ordinal).ThenByDescending(p=>p.Id).First(); }
        catch(FormatException) { return "Creation time unavailable; removal is disabled until all restore points can be dated."; }
        return newest.Id==selected.Id?"Newest restore point is protected":null;
    }
    public static string TypeName(int type)=>type switch{0=>"Application installed",1=>"Application removed",10=>"Driver installed",12=>"Settings changed",13=>"Cancelled operation",_=>"System checkpoint ("+type+")"};
}
