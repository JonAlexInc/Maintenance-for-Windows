# All Windows mutators and inventory commands are mocked. Never changes host configuration.
$ErrorActionPreference='Stop'
$change=Join-Path $PSScriptRoot '..\src\Maintenance.App\StartupChange.ps1'
$script:service=[pscustomobject]@{Name='Fixture';PathName='C:\Fixture\app.exe';StartMode='Auto';StartName='LocalSystem'}
$script:controller=[pscustomobject]@{Name='Fixture';DependentServices=@()}
$script:task=[pscustomobject]@{TaskName='Fixture';TaskPath='\Fixture\';Settings=[pscustomobject]@{Enabled=$true}}
$script:delayed=-1
function Get-CimInstance {$script:service}
function Get-Service {$script:controller}
function Set-Service {param([Parameter(ValueFromPipeline=$true)]$InputObject,$StartupType) process {$script:service.StartMode=switch($StartupType){'Automatic'{'Auto'}'Manual'{'Manual'}'Disabled'{'Disabled'}default{throw 'Bad mode'}}}}
function Stop-Service {throw 'Must not stop a service'}
function Set-ItemProperty {param($LiteralPath,$Name,$Value,$Type) $script:delayed=$Value}
function Get-ScheduledTask {$script:task}
function Export-ScheduledTask {param($TaskName,$TaskPath) '<Task><Settings><Enabled>'+([string]$script:task.Settings.Enabled).ToLower()+'</Enabled></Settings><Actions><Exec>fixture.exe</Exec></Actions></Task>'}
function Disable-ScheduledTask {param([Parameter(ValueFromPipeline=$true)]$InputObject) process{$script:task.Settings.Enabled=$false}}
function Enable-ScheduledTask {param([Parameter(ValueFromPipeline=$true)]$InputObject) process{$script:task.Settings.Enabled=$true}}
function Expect-Fail {param([scriptblock]$Action) $failed=$false;try{&$Action}catch{$failed=$true};if(!$failed){throw 'Expected rejection'}}
$p=[pscustomobject]@{Kind='Services';Name='Fixture';Location=$script:service.PathName;Expected='Auto';ExpectedAccount='LocalSystem';Desired='Disabled';Delayed=$false}
. $change|Out-Null;if($script:service.StartMode -ne 'Disabled'){throw 'Disable failed'}
Expect-Fail {. $change|Out-Null}
$p.Expected='Disabled';$p.Desired='Auto';$p.Delayed=$true;. $change|Out-Null
if($script:service.StartMode -ne 'Auto' -or $script:delayed -ne 1){throw 'Restore failed'}
$p.Expected='Auto';$p.Desired='Disabled';$script:controller.DependentServices=@('Dependency');Expect-Fail {. $change|Out-Null};$script:controller.DependentServices=@()
$script:service.StartName='ChangedAccount';Expect-Fail {. $change|Out-Null};$script:service.StartName='LocalSystem'
$script:service.PathName=$env:windir+'\system32\svchost.exe';$p.Location=$script:service.PathName;Expect-Fail {. $change|Out-Null}
$p=[pscustomobject]@{Kind='Scheduled tasks';Name='Fixture';Location='\Fixture\';ExpectedEnabled=$true;Desired='Disabled';Definition='<Task><Settings><Enabled>false</Enabled></Settings><Actions><Exec>fixture.exe</Exec></Actions></Task>'}
. $change|Out-Null;if($script:task.Settings.Enabled){throw 'Task disable failed'}
Expect-Fail {. $change|Out-Null}
$p.ExpectedEnabled=$false;$p.Desired='Enabled';. $change|Out-Null;if(!$script:task.Settings.Enabled){throw 'Task restore failed'}
$p.ExpectedEnabled=$true;$p.Desired='Disabled';$p.Definition='Changed';Expect-Fail {. $change|Out-Null}
$p.Location='\Microsoft\Windows\';$script:task.TaskPath=$p.Location;Expect-Fail {. $change|Out-Null}
'PASS: mock service disable/restore, delayed start, stale state, changed account, dependent/Windows guards, task disable/restore, changed definition and Microsoft task guards. No host mutations.'


