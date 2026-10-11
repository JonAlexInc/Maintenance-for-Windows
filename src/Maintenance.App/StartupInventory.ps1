$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$filePublishers=@{}
function Read-Publisher([string]$command) {
 $unknown=@{Publisher='Unknown';PublisherEvidence='Executable metadata unavailable';FileDescription=''}
 $expanded=[Environment]::ExpandEnvironmentVariables($command)
 if($expanded -notmatch '^\s*"?(.+?\.(?:exe|dll))(?:"|\s|$)'){return $unknown}
 $path=$Matches[1]
 if($path -notmatch '^[a-zA-Z]:\\'){return $unknown}
 if([IO.Path]::GetFileName($path) -in @('svchost.exe','rundll32.exe','dllhost.exe','powershell.exe','pwsh.exe','cmd.exe','wscript.exe','cscript.exe','msiexec.exe')){
  return @{Publisher='Unknown';PublisherEvidence='Shared Windows host; payload publisher is not established';FileDescription=''}
 }
 if($filePublishers.ContainsKey($path)){return $filePublishers[$path]}
 $result=$unknown
 try {$v=[Diagnostics.FileVersionInfo]::GetVersionInfo($path);if($v.CompanyName){$result=@{Publisher=[string]$v.CompanyName;PublisherEvidence=('File company metadata (not signature verification): '+$path);FileDescription=[string]$v.FileDescription}}}catch{}
 $filePublishers[$path]=$result;return $result
}
$rows=@();$notes=@()
try {
 $controllers=@{};Get-Service | ForEach-Object {$controllers[$_.Name]=$_}
 $rows+=@(Get-CimInstance Win32_Service | ForEach-Object {
  $s=$_;$d=$controllers[$s.Name];$delayed=$false
  try {$delayed=[bool](Get-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Services\'+$s.Name) -Name DelayedAutoStart -ErrorAction Stop).DelayedAutoStart}catch{}
  [ordered]@{Kind='Services';Name=[string]$s.Name;Title=[string]$s.DisplayName;State=[string]$s.State;Mode=[string]$s.StartMode;Location=[string]$s.PathName;Description=[string]$s.Description;Account=[string]$s.StartName;Author='';Extra=('Depends on: '+(@($d.ServicesDependedOn.Name)-join ', ')+'; dependents: '+(@($d.DependentServices.Name)-join ', ')+'; process ID: '+$s.ProcessId+'; service type: '+$s.ServiceType);Enabled=($s.StartMode -ne 'Disabled');Delayed=$delayed;Dependents=@($d.DependentServices).Count}
 })
}catch{$notes+='Services: '+$_.Exception.Message}
try {
 $rows+=@(Get-ScheduledTask | ForEach-Object {
  $t=$_;$history='History unavailable'
  $definition=''
  try {$xml=[xml](Export-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath);$xml.Task.Settings.Enabled='false';$definition=$xml.OuterXml}catch{}
  try {$info=$t|Get-ScheduledTaskInfo;$history=('Last run: '+$info.LastRunTime+'; next run: '+$info.NextRunTime+'; last result code: '+$info.LastTaskResult+'; missed runs: '+$info.NumberOfMissedRuns)}catch{}
  $triggers=@($t.Triggers|ForEach-Object {($_.CimClass.CimClassName -replace '^MSFT_Task','')+' '+$_.StartBoundary+'; enabled='+$_.Enabled})-join '; '
  [ordered]@{Kind='Scheduled tasks';Name=[string]$t.TaskName;Title=[string]$t.TaskName;State=[string]$t.State;Mode=$(if($t.Settings.Enabled){'Enabled'}else{'Disabled'});Location=[string]$t.TaskPath;Description=[string]$t.Description;Account=[string]$t.Principal.UserId;Author=[string]$t.Author;Extra=('Actions: '+(@($t.Actions|ForEach-Object {$_.Execute+' '+$_.Arguments+' (working folder: '+$_.WorkingDirectory+')'})-join '; ')+' | Triggers: '+$triggers+' | '+$history+' | Run level: '+$t.Principal.RunLevel);Enabled=[bool]$t.Settings.Enabled;Delayed=$false;Dependents=0;Definition=$definition;ActionCommands=@($t.Actions|ForEach-Object {[string]$_.Execute})}
 })
}catch{$notes+='Scheduled tasks: '+$_.Exception.Message}
try {
 $rows+=@(Get-CimInstance Win32_StartupCommand | ForEach-Object {
  [ordered]@{Kind='Startup apps';Name=[string]$_.Name;Title=[string]$_.Caption;State='Registered';Mode='Manage in Windows';Location=[string]$_.Location;Description=[string]$_.Command;Account=[string]$_.User;Author='';Extra='Run/Startup-folder registration; disabled and packaged startup tasks may not appear. Registration does not prove enabled state. Use Windows Startup settings for authoritative controls.';Enabled=$false;Delayed=$false;Dependents=0}
 })
}catch{$notes+='Startup apps: '+$_.Exception.Message}
foreach($r in $rows){
 $command=if($r.Kind -eq 'Services'){$r.Location}elseif($r.Kind -eq 'Startup apps'){$r.Description}else{''}
 if($r.Kind -eq 'Services' -and $command -match '(?i)svchost\.exe'){
  try {$command=[string](Get-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Services\'+$r.Name+'\Parameters') -Name ServiceDll -ErrorAction Stop).ServiceDll}catch{
   $command='';$template=$r.Name -replace '_[0-9a-fA-F]+$',''
   if($template -ne $r.Name){try {$command=[string](Get-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Services\'+$template+'\Parameters') -Name ServiceDll -ErrorAction Stop).ServiceDll}catch{}}
  }
 }
 $meta=Read-Publisher $command
 if($r.Kind -eq 'Scheduled tasks'){
  $metadata=@($r.ActionCommands|ForEach-Object {Read-Publisher $_})
  $known=@($metadata|Where-Object {$_.Publisher -ne 'Unknown'}|ForEach-Object {$_.Publisher}|Select-Object -Unique)
  if($known.Count -eq 1 -and $metadata.Count -gt 0 -and @($metadata|Where-Object {$_.Publisher -eq 'Unknown'}).Count -eq 0){$meta=$metadata[0]}
  elseif($known.Count -gt 1){$meta=@{Publisher=($known -join ' / ');PublisherEvidence='Multiple executable publishers; see actions';FileDescription=''}}
  elseif($known.Count -eq 0 -and $r.Location.StartsWith('\Microsoft\',[StringComparison]::OrdinalIgnoreCase)){$meta=@{Publisher='Microsoft';PublisherEvidence='Microsoft task folder classification; not signature verification';FileDescription=''}}
 }
 $r['Publisher']=$meta.Publisher;$r['PublisherEvidence']=$meta.PublisherEvidence;$r['FileDescription']=$meta.FileDescription
}
[ordered]@{Rows=@($rows);Notes=($notes -join ' | ')}|ConvertTo-Json -Depth 5 -Compress

