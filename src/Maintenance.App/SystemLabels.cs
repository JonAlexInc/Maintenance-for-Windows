using System.Globalization;
using System.Text.RegularExpressions;

namespace Maintenance.App;
sealed partial class MainWindow
{
    static string ReadableProperty(string property) => property switch
    {
        "L2CacheSize"=>"L2 cache", "L3CacheSize"=>"L3 cache", "VirtualizationFirmwareEnabled"=>"Hardware virtualization", "SecondLevelAddressTranslationExtensions"=>"Extended memory virtualization", "AddressWidth"=>"Architecture (bits)", "ConfiguredVoltage"=>"Configured voltage (mV)", "DataWidth"=>"Data width (bits)", "TotalWidth"=>"Total width (bits)", "SMBIOSMemoryType"=>"Memory generation",
        "Manufacturer"=>"Brand", "Product" or "Model" or "PartNumber"=>"Model", "Name" or "DeviceName" or "Caption"=>"Name",
        "SMBIOSBIOSVersion"=>"BIOS version", "ReleaseDate"=>"Released", "Enabled"=>"Secure Boot",
        "NumberOfCores"=>"CPU cores", "NumberOfLogicalProcessors"=>"Logical processors (threads)", "MaxClockSpeed"=>"Reported maximum clock",
        "SocketDesignation"=>"CPU socket", "Capacity"=>"Memory size", "ConfiguredClockSpeed"=>"Configured memory speed",
        "Speed"=>"Reported speed", "DeviceLocator"=>"Memory slot", "BankLabel"=>"Memory bank",
        "Size"=>"Total capacity", "FreeSpace"=>"Free space", "InterfaceType"=>"Windows storage interface", "MediaType"=>"Drive type", "FileSystem"=>"File system",
        "VideoProcessor"=>"Graphics processor", "DriverVersion"=>"Driver version", "CurrentHorizontalResolution"=>"Screen width", "CurrentVerticalResolution"=>"Screen height", "CurrentRefreshRate"=>"Refresh rate",
        "NetConnectionID"=>"Connection name", "IPAddress"=>"IP address", "MACAddress"=>"Hardware address",
        "BuildNumber"=>"Windows build", "OSArchitecture"=>"System type", "LastBootUpTime"=>"Last started", "HotFixID"=>"Update", "EstimatedChargeRemaining"=>"Battery charge",
        "Active channel mode"=>"Memory channels", "Active timings (CL/tRCD/tRP/tRAS)"=>"Memory timings",
        _=>Regex.Replace(property,"(?<=[a-z])(?=[A-Z])"," ")
    };
    static bool UsefulValue(string value) => !string.IsNullOrWhiteSpace(value) && !new[]{"Not reported","Unknown","System Product Name","System manufacturer","To be filled by O.E.M.","Default string"}.Contains(value.Trim(),StringComparer.OrdinalIgnoreCase);
    string Sibling(SystemFact fact,string property) => systemFacts.FirstOrDefault(f=>f.Section==fact.Section && f.Component==fact.Component && f.Property==property)?.Value.Trim() ?? "";
    string ReadableComponent(SystemFact fact)
    {
        if(fact.Section=="Memory modules") { var number=Regex.Match(fact.Component,@"\[(\d+)\]$");return number.Success?"Memory module "+number.Groups[1].Value:"Memory module"; }
        if(fact.Section=="BIOS and firmware")return fact.Component=="Secure Boot"?"Secure Boot":"BIOS / firmware";
        if(fact.Section=="Motherboard")return "Motherboard";
        if(fact.Section=="Computer")return "Computer";
        if(fact.Section=="Operating system")return "Windows";
        if(fact.Section=="Physical disks") {string model=Sibling(fact,"Model");if(UsefulValue(model))return model;}
        if(fact.Section=="Volumes")return "Drive "+Sibling(fact,"Name");
        return Regex.Replace(fact.Component,@"\s*\[\d+\]$","");
    }
    bool UsefulFact(SystemFact f)
    {
        if(!UsefulValue(f.Value))return false;
        if(f.Section is "Identity" or "Enclosure" or "Memory arrays" or "Memory mappings" or "Partitions" or "Disk partition mapping" or "Volume mapping" or "Storage controllers" or "Network configuration" or "Windows updates" or "Signed drivers" or "Plug and Play devices")return false;
        if(f.Section=="Network adapters" && !Sibling(f,"PhysicalAdapter").Equals("True",StringComparison.OrdinalIgnoreCase))return false;
        if(f.Section=="Memory modules" && f.Property is "Speed" or "BankLabel")return false;
        if(f.Section=="Graphics" && f.Property=="VideoProcessor")return false;
        return true;
    }
    string FriendlyValue(SystemFact f)
    {
        if (f.Property == "Manufacturer" && f.Value is "GenuineIntel" or "AuthenticAMD") return f.Value == "GenuineIntel" ? "Intel" : "AMD";
        if (f.Property is "L2CacheSize" or "L3CacheSize" && long.TryParse(f.Value, out long cacheKb)) return FormatBytes(cacheKb * 1024);
        if(f.Property=="Panel size")return PanelDimensions(f.Value,preferences.Measurements=="Metric / centimeters");
        if(f.Property=="SMBIOSMemoryType")return f.Value switch {"26"=>"DDR4","34"=>"DDR5","24"=>"DDR3","30"=>"LPDDR4","35"=>"LPDDR5",_=>"Not identified by firmware"};
        if(!UsefulValue(f.Value))return "Not available";
        if((f.Property is "Capacity" or "Size" or "FreeSpace") && long.TryParse(f.Value,out var bytes))return FormatBytes(bytes);
        if(f.Property=="MaxClockSpeed" && double.TryParse(f.Value,out var clock))return $"{clock/1000:N2} GHz";
        if(f.Section=="Memory modules" && f.Property is "ConfiguredClockSpeed" or "Speed")return $"{f.Value} MT/s (SMBIOS-reported)";
        if(f.Section=="Network adapters" && f.Property=="Speed" && double.TryParse(f.Value,out var rate))return rate>=1e9?$"{rate/1e9:N1} Gbps":$"{rate/1e6:N0} Mbps";
        if(f.Property=="CurrentRefreshRate")return f.Value+" Hz";
        if(f.Property is "CurrentHorizontalResolution" or "CurrentVerticalResolution")return f.Value+" pixels";
        if(f.Property=="EstimatedChargeRemaining")return f.Value+"%";
        if(f.Property is "ReleaseDate" or "LastBootUpTime" && DateTime.TryParse(f.Value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))return date.ToString(f.Property=="ReleaseDate"?"MMM d, yyyy":"MMM d, yyyy · h:mm tt");
        if(f.Property is "Active channel mode" or "Active timings (CL/tRCD/tRP/tRAS)")return "Not available from Windows. Requires a hardware-specific tool.";
        if(bool.TryParse(f.Value,out var enabled))return enabled?"On":"Off";
        return f.Value.Trim();
    }
}





