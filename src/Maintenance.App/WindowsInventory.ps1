function Add-WindowsFact($card,$key,$value) {
 if($null -eq $value -or [string]$value -eq ''){$value='Unavailable'}
 $rows.Add(@{Section='Windows overview';Component=$card;Property=$key;Value=[string]$value})
}
function Read-Settings($path){try{Get-ItemProperty -LiteralPath $path -ErrorAction Stop}catch{$null}}
try {
 $version=Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
 $editions=@{Core='Home';CoreSingleLanguage='Home Single Language';Professional='Pro';ProfessionalWorkstation='Pro for Workstations';Enterprise='Enterprise';Education='Education'}
 Add-WindowsFact 'Windows release' 'Edition' $(if($editions.ContainsKey([string]$version.EditionID)){$editions[[string]$version.EditionID]}else{$version.EditionID})
 Add-WindowsFact 'Windows release' 'Release (Windows-reported)' $version.DisplayVersion
 Add-WindowsFact 'Windows release' 'Full OS build' ($version.CurrentBuild+'.'+$version.UBR)
 Add-WindowsFact 'Windows release' 'Installation type' $version.InstallationType
 Add-WindowsFact 'Windows release' 'Build branch' $version.BuildBranch
 Add-WindowsFact 'Windows release' 'Build lab' $version.BuildLabEx
 if($version.InstallDate){Add-WindowsFact 'Windows release' 'Current installation date' ([DateTimeOffset]::FromUnixTimeSeconds([long]$version.InstallDate).ToLocalTime().ToString('yyyy-MM-dd HH:mm:ss'))}
 Add-WindowsFact 'Windows release' 'Install-date note' 'Feature upgrades can reset the reported installation date.'
}catch{Add-WindowsFact 'Windows release' 'Collection status' 'Release information unavailable'}
try {
 $licenses=@(Get-CimInstance SoftwareLicensingProduct -Filter "ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL" -OperationTimeoutSec 5)
 $states=@{0='Unlicensed';1='Licensed / activated';2='Initial grace period';3='Additional grace period';4='Non-genuine grace period';5='Notification mode';6='Extended grace period'}
 if(!$licenses.Count){Add-WindowsFact 'Windows activation' 'Status' 'Windows did not report a matching license'}
 foreach($license in $licenses){
  $card=if($licenses.Count -eq 1){'Windows activation'}else{'Activation - '+$license.Name}
  Add-WindowsFact $card 'License status' $states[[int]$license.LicenseStatus]
  Add-WindowsFact $card 'License channel' $license.Description
  if($license.GracePeriodRemaining -gt 0){Add-WindowsFact $card 'Grace remaining' ([string]$license.GracePeriodRemaining+' minutes')}
 }
}catch{Add-WindowsFact 'Windows activation' 'Status' 'Activation information unavailable'}
$au=Read-Settings 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU'
$wu=Read-Settings 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
$ux=Read-Settings 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings'
try {
 $service=Get-CimInstance Win32_Service -Filter "Name='wuauserv'" -OperationTimeoutSec 5
 Add-WindowsFact 'Windows Update' 'Update service' ($service.State+' / '+$service.StartMode)
 $automatic=if($service.StartMode -eq 'Disabled'){'Disabled service'}elseif($au.NoAutoUpdate -eq 1){'Disabled by automatic-update policy'}else{'No disable policy detected; service may start on demand'}
 Add-WindowsFact 'Windows Update' 'Automatic update availability' $automatic
}catch{Add-WindowsFact 'Windows Update' 'Update service' 'Unavailable'}
Add-WindowsFact 'Windows Update' 'Update source policy' $(if($au.UseWUServer -eq 1){'Organization WSUS server'}else{'No WSUS policy detected; other management may apply'})
if($au.AUOptions){$options=@{2='Notify before download';3='Download automatically, notify for install';4='Automatic download and scheduled install';5='Local administrator chooses';7='Notify for install and restart'};Add-WindowsFact 'Windows Update' 'Automatic-update policy' $options[[int]$au.AUOptions]}
foreach($key in @('PauseUpdatesExpiryTime','PauseFeatureUpdatesEndTime','PauseQualityUpdatesEndTime')){if($ux.$key){Add-WindowsFact 'Windows Update' ($key -replace '([a-z])([A-Z])','$1 $2') $ux.$key}}
if(!$ux.PauseUpdatesExpiryTime -and !$ux.PauseFeatureUpdatesEndTime -and !$ux.PauseQualityUpdatesEndTime){Add-WindowsFact 'Windows Update' 'Pause state' 'No pause dates recorded'}
if($wu.TargetReleaseVersionInfo){Add-WindowsFact 'Windows Update' 'Target release policy' $wu.TargetReleaseVersionInfo}
foreach($key in @('DeferFeatureUpdatesPeriodInDays','DeferQualityUpdatesPeriodInDays')){if($null -ne $wu.$key){Add-WindowsFact 'Windows Update' ($key -replace '([a-z])([A-Z])','$1 $2') $wu.$key}}
$reboot=(Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') -or (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending')
Add-WindowsFact 'Windows Update' 'Restart pending (registry)' $(if($reboot){'Yes'}else{'No Windows Update / servicing marker found'})
Add-WindowsFact 'Windows Update' 'Coverage' 'Local configuration only. No online update check was run; service state alone does not establish update health.'
$insider=Read-Settings 'HKLM:\SOFTWARE\Microsoft\WindowsSelfHost\UI\Selection'
$flight=Read-Settings 'HKLM:\SOFTWARE\Microsoft\WindowsSelfHost\Applicability'
Add-WindowsFact 'Insider / servicing channel' 'Configured channel (registry)' $(if($insider.UIBranch){$insider.UIBranch}elseif($flight.BranchName){$flight.BranchName}else{'No Insider channel recorded'})
if($insider.UIRing){Add-WindowsFact 'Insider / servicing channel' 'Configured ring' $insider.UIRing}
if($flight.Ring){Add-WindowsFact 'Insider / servicing channel' 'Applicability ring' $flight.Ring}
if($flight.ContentType){Add-WindowsFact 'Insider / servicing channel' 'Content type' $flight.ContentType}
Add-WindowsFact 'Insider / servicing channel' 'Coverage' 'Saved channel settings may be stale; enrollment and delivery are not verified online.'
Add-WindowsFact 'Session & environment' 'Time zone' ([TimeZoneInfo]::Local.DisplayName)
Add-WindowsFact 'Session & environment' 'Display language' ([Globalization.CultureInfo]::CurrentUICulture.DisplayName)
Add-WindowsFact 'Session & environment' 'Windows directory' $env:windir
Add-WindowsFact 'Session & environment' 'Secure Boot' $secure
try{$plan=Get-CimInstance -Namespace root/cimv2/power -ClassName Win32_PowerPlan -Filter 'IsActive=True' -OperationTimeoutSec 5;Add-WindowsFact 'Session & environment' 'Active power plan' $plan.ElementName}catch{}
