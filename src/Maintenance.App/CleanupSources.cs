using Maintenance.Core;
using System.Security.Principal;
namespace Maintenance.App;
sealed partial class MainWindow
{
    const string TempMode="Temporary files";
    readonly FlowLayoutPanel cleanupSources=new(){Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,AutoScroll=false,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Padding=new(4)};
    readonly ToolTip sourceTips=new();
    readonly List<(CheckBox Check,string[] Roots,bool Allowed)> sourceChecks=[];
    bool IsAdmin=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    Control BuildCleanupSources()
    {
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows),common=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        void Add(string label,string[] roots,bool check=false,bool admin=false) {
            bool enabled=!admin||IsAdmin;var box=new AppearanceCheckBox{AutoSize=true,Font=new Font("Segoe UI",9),Text=label.Split(" (")[0]+(admin?" · admin":""),Checked=check&&enabled,AutoCheck=enabled,Enabled=enabled,Margin=new(4,4,4,4)};
            sourceTips.SetToolTip(box,label+"\r\n"+string.Join("\r\n",roots));sourceChecks.Add((box,roots,enabled));cleanupSources.Controls.Add(box);box.CheckedChanged+=(_,_)=>{if(!busy)ClearResults();};
        }
        Add("Current account temporary files",[Path.Combine(local,"Temp")],true);
        Add("Windows temporary files",[Path.Combine(windows,"Temp")],true,true);
        Add("Application crash dumps (diagnostics will be lost)",[Path.Combine(local,"CrashDumps")]);
        Add("Windows minidumps (diagnostics will be lost)",[Path.Combine(windows,"Minidump")],false,true);
        Add("Archived Windows error reports",[Path.Combine(common,"Microsoft","Windows","WER","ReportArchive")],false,true);
        Add("Current account archived error reports",[Path.Combine(local,"Microsoft","Windows","WER","ReportArchive")]);
        Add("NVIDIA shader caches (will rebuild)",[Path.Combine(local,"NVIDIA","DXCache"),Path.Combine(local,"NVIDIA","GLCache")]);
        Add("AMD shader caches (will rebuild)",[Path.Combine(local,"AMD","DxCache"),Path.Combine(local,"AMD","GLCache")]);
        Add("DirectX shader cache (will rebuild)",[Path.Combine(local,"D3DSCache")]);
        var note=new Label{AutoSize=true,Font=new Font("Segoe UI",9),Text=$"{(IsAdmin?"Administrator":"Standard user")} · Account: {Environment.UserName} · Only checked sources. Missing folders are skipped. Minimum age: 7 days."};cleanupSources.Controls.Add(note);
        var managed=new LinkLabel{AutoSize=true,Font=new Font("Segoe UI",9),LinkColor=Color.CornflowerBlue,ActiveLinkColor=Color.DeepSkyBlue,Text="Windows-managed cleanup: updates, Delivery Optimization, previous Windows, Recycle Bin…"};
        managed.LinkClicked+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true});cleanupSources.Controls.Add(managed);
        void LayoutSources(){int columns=Math.Max(1,cleanupSources.ClientSize.Width/430);int width=Math.Max(220,(cleanupSources.ClientSize.Width-12)/columns-8);foreach(var source in sourceChecks){source.Check.AutoSize=false;source.Check.Width=width;source.Check.Height=Math.Max(38,TextRenderer.MeasureText(source.Check.Text,source.Check.Font,new Size(width-30,0),TextFormatFlags.WordBreak).Height+16);}note.MaximumSize=new(Math.Max(220,cleanupSources.ClientSize.Width-20),0);managed.MaximumSize=note.MaximumSize;cleanupSources.SetFlowBreak(sourceChecks.Last().Check,true);cleanupSources.SetFlowBreak(note,true);}
        cleanupSources.SizeChanged+=(_,_)=>LayoutSources();LayoutSources();FormClosed+=(_,_)=>sourceTips.Dispose();
        return cleanupSources;
    }
    bool ConfigureCleanupEngine()
    {
        if(smoke)return true;
        var roots=sourceChecks.Where(s=>s.Check.Checked&&s.Allowed).SelectMany(s=>s.Roots).ToArray();
        if(roots.Length==0){MessageBox.Show(this,"Check at least one temporary-file source.");return false;}
        engine=new MultiCleanupEngine(roots);return true;
    }
}








