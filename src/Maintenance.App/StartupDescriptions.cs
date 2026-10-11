namespace Maintenance.App;
static class StartupDescriptions
{
    internal static bool IsMicrosoft(StartupEntry item)=>new[]{"Microsoft","Microsoft Corporation","Microsoft Windows"}.Contains(item.Publisher.Trim(),StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string,string> Services=new(StringComparer.OrdinalIgnoreCase){
        ["wuauserv"]="Finds, downloads and installs Windows updates.",
        ["BITS"]="Transfers files in the background for Windows and apps, including updates.",
        ["DoSvc"]="Downloads Windows and Microsoft Store updates and can share update data according to your Windows settings.",
        ["WinDefend"]="Runs Microsoft Defender Antivirus protection and malware scans.",
        ["Spooler"]="Queues print jobs and sends them to printers.",
        ["Audiosrv"]="Manages sound playback and recording in Windows.",
        ["AudioEndpointBuilder"]="Finds and configures audio devices used for playback and recording.",
        ["WSearch"]="Builds a search index so Windows can find files and other content faster.",
        ["Dhcp"]="Obtains network addresses and related settings automatically from your router or network.",
        ["Dnscache"]="Remembers network name lookups so apps can find websites and other computers.",
        ["EventLog"]="Records Windows and application events used for troubleshooting and auditing.",
        ["Schedule"]="Runs automated tasks at their configured times or when specified events occur.",
        ["W32Time"]="Keeps the computer's clock synchronized with its configured time source.",
        ["bthserv"]="Helps Windows discover and use Bluetooth devices.",
        ["RpcSs"]="Lets Windows components and programs communicate with one another.",
        ["LanmanWorkstation"]="Connects this computer to shared files and printers on other computers.",
        ["LanmanServer"]="Makes this computer's shared files and printers available over the network.",
        ["Themes"]="Applies Windows visual themes and appearance settings."
    };
    internal static string Purpose(StartupEntry r){
        if(r.Kind=="Services"&&IsMicrosoft(r)&&Services.TryGetValue(r.Name,out var explanation))return explanation;
        if(r.Kind!="Startup apps"&&!string.IsNullOrWhiteSpace(r.Description)&&!r.Description.TrimStart().StartsWith('@'))return "Reported purpose: "+r.Description.Trim();
        if(!string.IsNullOrWhiteSpace(r.FileDescription))return r.Kind=="Startup apps"?"Starts at sign-in: "+r.FileDescription:r.Kind=="Services"?"Background component: "+r.FileDescription:"Runs automatically: "+r.FileDescription;
        if(r.Kind=="Scheduled tasks"&&r.ActionCommands is {Length:>0})return "Runs "+string.Join(", ",r.ActionCommands.Where(a=>!string.IsNullOrWhiteSpace(a)).Select(a=>Path.GetFileName(a.Trim('"'))).Distinct())+" according to its configured triggers. Its specific purpose is not reported.";
        return r.Kind=="Startup apps"?"Registered to start at sign-in. Its specific purpose is not reported.":"Purpose not reported. Review its executable, account and configuration below.";
    }
    internal static string Source(StartupEntry r)=>r.Kind=="Services"&&IsMicrosoft(r)&&Services.ContainsKey(r.Name)?"Built-in plain-English explanation":r.Kind!="Startup apps"&&!string.IsNullOrWhiteSpace(r.Description)&&!r.Description.TrimStart().StartsWith('@')?"Description supplied by Windows or the registering application":!string.IsNullOrWhiteSpace(r.FileDescription)?"Executable file description":"Configuration summary; application purpose unknown";
}
