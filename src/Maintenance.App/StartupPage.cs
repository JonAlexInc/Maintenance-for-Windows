using Maintenance.Core;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace Maintenance.App;
sealed record StartupEntry(string Kind,string Name,string Title,string State,string Mode,string Location,string Description,string Account,string Author,string Extra,bool Enabled,bool Delayed,int Dependents,string Definition="",string Publisher="Unknown",string PublisherEvidence="Not available",string FileDescription="",string[]? ActionCommands=null);
sealed record StartupSnapshot(StartupEntry[] Rows,string Notes);
sealed record StartupBackup(string Id,StartupEntry Item,string Outcome,DateTimeOffset At);
static class StartupTools
{
    internal static async Task<string> Run(string script,object? payload=null,CancellationToken token=default){
        using var operation=AppOperations.Begin(token);token=operation.Token;token.ThrowIfCancellationRequested();
        string code="$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $p=ConvertFrom-Json ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))+"')));\n"+script;
        using var process=new Process{StartInfo=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8}};
        foreach(string arg in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(code))})process.StartInfo.ArgumentList.Add(arg);
        process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(limit.Token,token);try{await process.WaitForExitAsync(linked.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);if(token.IsCancellationRequested)throw new OperationCanceledException(token);throw new IOException("Windows operation timed out. Refresh to verify the current state.");}
        string result=await output,reason=await error;if(process.ExitCode!=0)throw new IOException(reason.Trim());return result;
    }
    internal static string Script(string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,name));
    internal static bool Protected(StartupEntry r)=>r.Kind=="Startup apps"||r.Kind=="Services"&&(r.Dependents>0||r.Mode is "Boot" or "System"||!r.Location.Contains(".exe",StringComparison.OrdinalIgnoreCase)||Environment.ExpandEnvironmentVariables(r.Location).Contains(Environment.GetFolderPath(Environment.SpecialFolder.Windows)+"\\",StringComparison.OrdinalIgnoreCase))||r.Kind=="Scheduled tasks"&&(r.Definition.Length==0||r.Location.StartsWith(@"\Microsoft\",StringComparison.OrdinalIgnoreCase));
}
sealed partial class MainWindow
{
    readonly HardwarePages startupTabs=new(){Dock=DockStyle.Fill};
    readonly TextBox startupFind=new(){Width=380,PlaceholderText="Search name, description, account or path"};
    readonly TextBox startupDetail=new(){Dock=DockStyle.Bottom,Height=190,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    readonly Label startupSummary=new(){Dock=DockStyle.Top,Height=54,Text="Read the inventory to review startup apps, services and scheduled tasks. No changes happen during scanning."};
    readonly List<DataGridView> startupGrids=[];
    readonly AppearanceCheckBox hideMicrosoftStartup=new(){Text="Hide Microsoft items",AutoSize=true};
    readonly Label startupFilterCount=new(){AutoSize=true,Margin=new(12,12,0,0)};
    List<StartupEntry> startupEntries=[];
    Button startupDisable=null!,startupRestore=null!;bool startupWorking;
    string StartupBackupFile=>Path.Combine(store.Folder,"startup-changes.jsonl");
    void BuildStartup(){var body=Body(Page("Startup & automation","Understand what starts with Windows, runs in the background, and executes on a schedule."));
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(0,6,0,6)};
        actions.Controls.Add(StartSearch(ReadStartup));actions.Controls.Add(ScanCancel());
        startupDisable=Button("Disable selected…",()=>ChangeStartup(false));startupRestore=Button("Restore previous setting…",()=>ChangeStartup(true));actions.Controls.Add(startupDisable);actions.Controls.Add(startupRestore);
        actions.Controls.Add(Button("Windows controls…",()=>{string target=startupTabs.SelectedIndex switch{0=>"ms-settings:startupapps",1=>"services.msc",_=>"taskschd.msc"};Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}));
        actions.Controls.Add(Button("Inspect executable…",InspectStartupFile));
        var filters=ResultFilters(startupFind,hideMicrosoftStartup,startupFilterCount);hideMicrosoftStartup.CheckedChanged+=(_,_)=>RenderStartup();
        foreach(string title in new[]{"Startup apps","Services","Scheduled tasks"}){var tab=new Panel{Text=title,Dock=DockStyle.Fill};var grid=HistoryGrid();startupGrids.Add(grid);foreach(string name in new[]{"Name","Publisher","What it does","Status","Startup / enabled","Account / author","Location"})grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,HeaderText=name,Width=name is "What it does" or "Location"?460:220,SortMode=DataGridViewColumnSortMode.Automatic});grid.SelectionChanged+=(_,_)=>ShowStartupSelection();tab.Controls.Add(grid);startupTabs.TabPages.Add(tab);startupTabs.Controls.Add(tab);}
        body.Controls.Add(startupTabs);body.Controls.Add(startupVisual);body.Controls.Add(ModernTabs(startupTabs));body.Controls.Add(filters);body.Controls.Add(startupSummary);body.Controls.Add(actions);body.Controls.Add(startupDetail);
        startupFind.TextChanged+=(_,_)=>RenderStartup();startupTabs.SelectedIndexChanged+=(_,_)=>ShowStartupSelection();startupDisable.Enabled=startupRestore.Enabled=false;
    }
    async Task ReadStartup(){if(startupWorking||busy)return;startupWorking=true;SetBusy(true);cancel=new();status.Text="Reading services, scheduled tasks and startup registrations…";ShowStartupSelection();
        try{var snapshot=JsonSerializer.Deserialize<StartupSnapshot>(await StartupTools.Run(StartupTools.Script("StartupInventory.ps1"),token:cancel.Token))??throw new IOException("Inventory could not be read");startupEntries=snapshot.Rows.ToList();RenderStartup();startupSummary.Text=$"{startupEntries.Count:N0} items · Windows/boot components and services with dependents are managed through Windows controls. "+snapshot.Notes;status.Text="Startup inventory complete · No settings changed.";}
        catch(OperationCanceledException){startupSummary.Text="Search cancelled · Previously loaded inventory remains visible. Windows returns this inventory as a single response.";status.Text=startupSummary.Text;}catch(Exception e){startupSummary.Text="Inventory unavailable: "+e.Message;status.Text=startupSummary.Text;}
        finally{startupWorking=false;cancel?.Dispose();cancel=null;SetBusy(false);ShowStartupSelection();}
    }
    void RenderStartup(){for(int i=0;i<startupGrids.Count;i++){var grid=startupGrids[i];grid.Rows.Clear();string kind=startupTabs.TabPages[i].Text;foreach(var r in startupEntries.Where(r=>r.Kind==kind&&(!hideMicrosoftStartup.Checked||!StartupDescriptions.IsMicrosoft(r))&&$"{r.Name} {r.Title} {r.Publisher} {StartupDescriptions.Purpose(r)} {r.Description} {r.Account} {r.Author} {r.Location} {r.Extra}".Contains(startupFind.Text,StringComparison.OrdinalIgnoreCase)).OrderBy(r=>r.Title)){int row=grid.Rows.Add(r.Title,r.Publisher,StartupDescriptions.Purpose(r),r.State,(r.Mode=="Auto"?"Automatic":r.Mode)+(r.Delayed&&r.Mode=="Auto"?" · delayed":""),string.IsNullOrWhiteSpace(r.Author)?r.Account:r.Author,r.Location);grid.Rows[row].Tag=r;}}ShowStartupSelection();}
    StartupEntry? SelectedStartup=>startupTabs.SelectedIndex>=0&&startupGrids[startupTabs.SelectedIndex].SelectedRows.Count>0?startupGrids[startupTabs.SelectedIndex].SelectedRows[0].Tag as StartupEntry:null;
    void UpdateStartupFilterCount(){if(startupTabs.SelectedIndex<0)return;string kind=startupTabs.TabPages[startupTabs.SelectedIndex].Text;int total=startupEntries.Count(r=>r.Kind==kind),hidden=hideMicrosoftStartup.Checked?startupEntries.Count(r=>r.Kind==kind&&StartupDescriptions.IsMicrosoft(r)):0;ShowStartupVisual();startupFilterCount.Text=$"{startupGrids[startupTabs.SelectedIndex].Rows.Count:N0} of {total:N0} shown · {hidden:N0} Microsoft items hidden · Unknown publishers stay visible";}
    List<StartupBackup> StartupBackups(){try{return File.ReadLines(StartupBackupFile).Select(line=>{try{return JsonSerializer.Deserialize<StartupBackup>(line);}catch(JsonException){return null;}}).Where(r=>r!=null).Cast<StartupBackup>().GroupBy(r=>r.Id).Select(g=>g.Last()).ToList();}catch(IOException){return [];}catch(UnauthorizedAccessException){return [];}}
    StartupBackup? BackupFor(StartupEntry r)=>StartupBackups().Where(b=>(b.Outcome is "Applied" or "Pending" or "Restore pending")&&b.Item.Kind==r.Kind&&b.Item.Name==r.Name&&b.Item.Location==r.Location).OrderByDescending(b=>b.At).FirstOrDefault();
    void ShowStartupSelection(){if(startupDisable==null)return;UpdateStartupFilterCount();var r=SelectedStartup;startupDisable.Enabled=!startupWorking&&IsAdmin&&r!=null&&r.Enabled&&!StartupTools.Protected(r);startupRestore.Enabled=!startupWorking&&IsAdmin&&r!=null&&!r.Enabled&&BackupFor(r)!=null&&!StartupTools.Protected(r);
        startupDetail.Text=r==null?"Select an item to see its description and configuration.":$"{r.Title} · {r.Name}\r\n{StartupDescriptions.Purpose(r)}\r\nExplanation source: {StartupDescriptions.Source(r)}\r\nPublisher: {r.Publisher} · {r.PublisherEvidence}\r\nOriginal description: {r.Description}\r\nLocation: {r.Location}\r\nAccount: {r.Account}   Author (reported): {r.Author}\r\n{r.Extra}\r\n{(StartupTools.Protected(r)?"Manage this item through Windows controls.":"Disabling changes future startup/scheduling; it does not terminate a running service or task.")}{(!IsAdmin?" Administrator permission is required to change settings.":"")}";
    }
    void RecordStartup(StartupBackup backup){using var file=new FileStream(StartupBackupFile,FileMode.Append,FileAccess.Write,FileShare.Read);byte[] bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(backup)+Environment.NewLine);file.Write(bytes);file.Flush(true);using var journal=store.StartJournal();LocalStore.Append(journal,new{Kind="Startup change",Summary=$"{backup.Item.Kind}: {backup.Item.Name} · {backup.Outcome}",OriginalMode=backup.Item.Mode});}
    async Task ChangeStartup(bool restore){var r=SelectedStartup;if(smoke||startupWorking||r==null||!IsAdmin||StartupTools.Protected(r))return;var previous=restore?BackupFor(r):null;if(restore&&previous==null)return;if(!restore&&!r.Enabled)return;
        string desired=restore?(r.Kind=="Services"?previous!.Item.Mode:"Enabled"):"Disabled";
        if(MessageBox.Show(this,$"{(restore?"Restore":"Disable")} {r.Title}?\n\n{r.Kind} · {r.Name}\nNew setting: {desired}\n{r.Location}\n\nThis can affect app features, backup, updates or hardware support. Running work is not stopped. The previous setting is saved locally.","Review startup change",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        if(restore&&(previous!.Item.Definition!=r.Definition||previous.Item.Account!=r.Account)){MessageBox.Show(this,"The item definition or account changed after the saved setting. Review it in Windows controls.");return;}
        var backup=previous??new StartupBackup(Guid.NewGuid().ToString("N"),r,"Pending",DateTimeOffset.UtcNow);startupWorking=true;ShowStartupSelection();status.Text="Applying selected startup setting…";string outcome="";int generation=stopGeneration;
        try{RecordStartup(backup with{Outcome=restore?"Restore pending":"Pending",At=DateTimeOffset.UtcNow});await StartupTools.Run(StartupTools.Script("StartupChange.ps1"),new{r.Kind,r.Name,r.Location,Expected=r.Mode,ExpectedEnabled=r.Enabled,ExpectedAccount=r.Account,Desired=desired,Delayed=previous?.Item.Delayed??r.Delayed,Definition=r.Definition});RecordStartup(backup with{Outcome=restore?"Restored":"Applied",At=DateTimeOffset.UtcNow});status.Text="Startup setting changed and verified.";}
        catch(OperationCanceledException){status.Text="Change stopped · Current setting is unconfirmed. Review History and refresh before retrying.";}catch(Exception e){status.Text="Change could not be confirmed. Refresh and inspect the current setting: "+e.Message;MessageBox.Show(this,status.Text,"Startup setting",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{outcome=status.Text;startupWorking=false;if(generation==stopGeneration)await ReadStartup();status.Text=outcome;RefreshHistory();}
    }
    async Task InspectStartupFile(){var r=SelectedStartup;if(r==null||r.Kind=="Scheduled tasks")return;string command=r.Kind=="Services"?r.Location:r.Description;var match=System.Text.RegularExpressions.Regex.Match(command,"^\\s*\"?(.+?\\.exe)(?:\"|\\s|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);if(!match.Success){startupDetail.Text+="\r\nExecutable path could not be resolved.";return;}string path=Environment.ExpandEnvironmentVariables(match.Groups[1].Value);if(!Path.IsPathFullyQualified(path)||!File.Exists(path)){startupDetail.Text+="\r\nExecutable unavailable.";return;}
        try{string info=await StartupTools.Run("$v=[Diagnostics.FileVersionInfo]::GetVersionInfo($p.Path);$s=Get-AuthenticodeSignature -LiteralPath $p.Path; 'Publisher (file metadata): '+$v.CompanyName; 'Product: '+$v.ProductName; 'Version: '+$v.FileVersion; 'Signature: '+$s.Status; 'Signer: '+$s.SignerCertificate.Subject",new{Path=path});if(SelectedStartup==r)startupDetail.Text+="\r\n"+info+"\r\nA signature is not a safety verdict.";}catch(Exception e){startupDetail.Text+="\r\nInspection unavailable: "+e.Message;}
    }
}









