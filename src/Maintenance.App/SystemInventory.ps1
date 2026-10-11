[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$rows = [Collections.Generic.List[object]]::new()
$classes = [ordered]@{
'Mouse'='Win32_PointingDevice'; 'Keyboard'='Win32_Keyboard'; 'Computer'='Win32_ComputerSystem'; 'Identity'='Win32_ComputerSystemProduct'; 'Motherboard'='Win32_BaseBoard'; 'BIOS and firmware'='Win32_BIOS'; 'Enclosure'='Win32_SystemEnclosure'; 'Processors'='Win32_Processor'; 'Memory modules'='Win32_PhysicalMemory'; 'Memory arrays'='Win32_PhysicalMemoryArray'; 'Memory mappings'='Win32_MemoryArray'; 'Operating system'='Win32_OperatingSystem'; 'Graphics'='Win32_VideoController'; 'Displays'='Win32_DesktopMonitor'; 'Physical disks'='Win32_DiskDrive'; 'Partitions'='Win32_DiskPartition'; 'Volumes'='Win32_LogicalDisk'; 'Disk partition mapping'='Win32_DiskDriveToDiskPartition'; 'Volume mapping'='Win32_LogicalDiskToPartition'; 'Network adapters'='Win32_NetworkAdapter'; 'Network configuration'='Win32_NetworkAdapterConfiguration'; 'Audio'='Win32_SoundDevice'; 'Battery'='Win32_Battery'; 'Cooling'='Win32_Fan'; 'Storage controllers'='Win32_SCSIController'; 'USB controllers'='Win32_USBController'; 'Plug and Play devices'='Win32_PnPEntity'; 'Signed drivers'='Win32_PnPSignedDriver'; 'Windows updates'='Win32_QuickFixEngineering'; 'Expansion slots'='Win32_SystemSlot'; 'Board connectors'='Win32_PortConnector'
}
foreach ($section in $classes.Keys) {
 try {
  $instances = @(Get-CimInstance -ClassName $classes[$section] -OperationTimeoutSec 5)
  if (!$instances.Count) { $rows.Add(@{Section=$section;Component='Availability';Property='Status';Value='Not reported by Windows'}) }
  $index = 0
  foreach ($instance in $instances) {
   $index++
   $label = [string]$instance.Name
   if (!$label) { $label = [string]$instance.DeviceID }
   if (!$label) { $label = "Instance $index" }
   $label = "$label [$index]"
   if ($section -in @('Mouse','Keyboard','Graphics')) {
    $deviceId = [string]$instance.PNPDeviceID
    if ($deviceId) {
     try {
      $props = @(Get-PnpDeviceProperty -InstanceId $deviceId -ErrorAction Stop)
      $container = ($props | Where-Object KeyName -eq 'DEVPKEY_Device_ContainerId').Data
      if ($container) { $rows.Add(@{Section=$section;Component=$label;Property='ContainerId';Value=[string]$container}) }
      $bus = ($props | Where-Object KeyName -eq 'DEVPKEY_Device_BusReportedDeviceDesc').Data
      if ($bus) { $rows.Add(@{Section=$section;Component=$label;Property='BusReportedName';Value=[string]$bus}) }
      $parent = ($props | Where-Object KeyName -eq 'DEVPKEY_Device_Parent').Data
      for ($depth=0; $depth -lt 6 -and $parent -and $section -ne 'Graphics'; $depth++) {
       $parentProps = @(Get-PnpDeviceProperty -InstanceId $parent -ErrorAction Stop)
       $parentName = ($parentProps | Where-Object KeyName -eq 'DEVPKEY_Device_BusReportedDeviceDesc').Data
       $friendlyParent = ($parentProps | Where-Object KeyName -eq 'DEVPKEY_Device_FriendlyName').Data
       if ($friendlyParent -and $friendlyParent -notmatch 'Service|Enumerator|Root Hub|USB Input|USB Composite') { $parentName = $friendlyParent }
       if ($parentName -and $parentName -notmatch 'Bluetooth LE Service|USB Input Device|USB Composite Device') {
        $rows.Add(@{Section=$section;Component=$label;Property='ParentReportedName';Value=[string]$parentName})
        $rows.Add(@{Section=$section;Component=$label;Property='ParentDeviceID';Value=[string]$parent})
        break
       }
       $parent = ($parentProps | Where-Object KeyName -eq 'DEVPKEY_Device_Parent').Data
      }
     } catch { }
    }
   }
   foreach ($property in $instance.CimInstanceProperties) {
    $v = $property.Value
    if ($null -eq $v -or [string]$v -eq '') { $value = 'Not reported' }
    elseif ($v -is [Array]) { $value = ($v | ForEach-Object { [string]$_ }) -join '; ' }
    elseif ($v -is [DateTime]) { $value = $v.ToString('yyyy-MM-dd HH:mm:ss') }
    else { $value = [string]$v }
    $rows.Add(@{Section=$section;Component=$label;Property=$property.Name;Value=$value})
   }
  }
 } catch { $rows.Add(@{Section=$section;Component='Availability';Property='Status';Value=$_.Exception.Message}) }
}
foreach ($entry in @(@('Memory details','Active channel mode','Not exposed reliably by standard Windows inventory. Module slots/bank labels are listed; these do not prove active channel mode.'), @('Memory details','Active timings (CL/tRCD/tRP/tRAS)','Not exposed by standard Windows inventory; requires vendor or SPD support.'), @('Memory details','Speed units','Speed and ConfiguredClockSpeed are raw SMBIOS values as reported by Windows; do not infer active timings from them.'))) {
 $rows.Add(@{Section=$entry[0];Component='Coverage';Property=$entry[1];Value=$entry[2]})
}
try { $secure = [string](Confirm-SecureBootUEFI) } catch { $secure = 'Unavailable: ' + $_.Exception.Message }
$rows.Add(@{Section='BIOS and firmware';Component='Secure Boot';Property='Enabled';Value=$secure})
function Decode-Monitor($value) { if ($value) { return [Text.Encoding]::ASCII.GetString([byte[]]$value).Trim([char]0) }; return '' }
try {
 $basics=@(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorBasicDisplayParams -OperationTimeoutSec 5)
 $connections=@(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorConnectionParams -OperationTimeoutSec 5)
 foreach($monitor in @(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorID -OperationTimeoutSec 5)) {
  if(!$monitor.Active){continue}
  $label=$monitor.InstanceName
  $data=[ordered]@{Name=(Decode-Monitor $monitor.UserFriendlyName);Manufacturer=(Decode-Monitor $monitor.ManufacturerName);Serial=(Decode-Monitor $monitor.SerialNumberID);Manufactured=([string]$monitor.YearOfManufacture+' / week '+$monitor.WeekOfManufacture);Active='Yes';PNPDeviceID=($label -replace '_\d+$','')}
  $basic=$basics | Where-Object InstanceName -eq $label | Select-Object -First 1
  if($basic.MaxHorizontalImageSize -and $basic.MaxVerticalImageSize){$data['Panel size']=[string]$basic.MaxHorizontalImageSize+' x '+$basic.MaxVerticalImageSize+' cm'}
  $connection=$connections | Where-Object InstanceName -eq $label | Select-Object -First 1
  $technology=@{0='VGA';4='DVI';5='HDMI';10='DisplayPort';11='Embedded DisplayPort';15='Miracast';16='Indirect wired'}
  if($null -ne $connection){$n=[int]$connection.VideoOutputTechnology;$data['Connection']=if($technology.ContainsKey($n)){$technology[$n]}else{'Windows output type '+$n}}
  foreach($key in $data.Keys){if($data[$key]){$rows.Add(@{Section='Connected displays';Component=$label;Property=$key;Value=[string]$data[$key]})}}
 }
} catch {$rows.Add(@{Section='Inventory coverage';Component='Monitor EDID';Property='Status';Value=$_.Exception.Message})}
. (Join-Path $PSScriptRoot "WindowsInventory.ps1")
ConvertTo-Json -InputObject @($rows.ToArray()) -Depth 5 -Compress



