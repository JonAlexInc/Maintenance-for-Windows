$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
if($p.Kind -eq 'Services'){
 $s=Get-CimInstance Win32_Service|Where-Object {$_.Name -ceq $p.Name}
 if(!$s -or $s.PathName -cne $p.Location -or $s.StartMode -ne $p.Expected -or $s.StartName -cne $p.ExpectedAccount){throw 'Service changed since review. Refresh before changing it.'}
 $svc=Get-Service|Where-Object {$_.Name -ceq $p.Name}
 if(!$svc -or @($svc.DependentServices).Count -gt 0){throw 'This service has dependent services. Manage it in Windows Services.'}
 if([Environment]::ExpandEnvironmentVariables($s.PathName).IndexOf(($env:windir+'\'),[StringComparison]::OrdinalIgnoreCase) -ge 0 -or $s.PathName -notmatch '(?i)\.exe' -or $s.StartMode -in @('Boot','System')){throw 'Windows and boot services must be managed in Windows Services.'}
 $mode=switch($p.Desired){'Auto'{'Automatic'}'Manual'{'Manual'}'Disabled'{'Disabled'}default{throw 'Unsupported startup mode'}}
 $svc|Set-Service -StartupType $mode -ErrorAction Stop
 $after=Get-CimInstance Win32_Service|Where-Object {$_.Name -ceq $p.Name}
 if($after.StartMode -ne $p.Desired){throw 'Startup mode could not be verified. Refresh and inspect the service.'}
 if($p.Desired -eq 'Auto'){
  Set-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Services\'+$p.Name) -Name DelayedAutoStart -Value ([int][bool]$p.Delayed) -Type DWord
 }
}elseif($p.Kind -eq 'Scheduled tasks'){
 $t=Get-ScheduledTask|Where-Object {$_.TaskName -ceq $p.Name -and $_.TaskPath -ceq $p.Location}
 if(!$t -or [bool]$t.Settings.Enabled -ne [bool]$p.ExpectedEnabled){throw 'Task changed since review. Refresh before changing it.'}
 if($t.TaskPath -like '\Microsoft\*'){throw 'Microsoft scheduled tasks must be managed in Task Scheduler.'}
 if(!$p.Definition){throw 'A complete task definition is required. Refresh before changing it.'}
 $xml=[xml](Export-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath);$xml.Task.Settings.Enabled='false'
 if($xml.OuterXml -cne $p.Definition){throw 'Task definition changed since review.'}
 if($p.Desired -eq 'Enabled'){$t|Enable-ScheduledTask|Out-Null}else{$t|Disable-ScheduledTask|Out-Null}
 $after=Get-ScheduledTask|Where-Object {$_.TaskName -ceq $p.Name -and $_.TaskPath -ceq $p.Location}
 if([bool]$after.Settings.Enabled -ne ($p.Desired -eq 'Enabled')){throw 'Task state could not be verified. Refresh and inspect the task.'}
}else{throw 'Unsupported item type'}
'Verified'
