using System.Diagnostics;
using System.Text.RegularExpressions;
namespace Maintenance.App;

sealed partial class MainWindow
{
    const string StartRole="start", CancelRole="cancel";
    readonly List<Button> scanCancelButtons=[];
    FlowLayoutPanel WorkflowRow() => new() { Dock=DockStyle.Top,AutoSize=true,MinimumSize=new(0,58),Padding=new(0,8,0,8),WrapContents=true };
    Button StartSearch(Func<Task> action) { var b=Button("Start search",action);b.Tag=StartRole;b.MinimumSize=new(160,42);return b; }
    Button ScanCancel() { var b=new AppearanceButton {Text="Cancel",Tag=CancelRole,Size=new(104,42),Enabled=false};b.Click+=(_,_)=>cancel?.Cancel();scanCancelButtons.Add(b);StyleButton(b);return b; }
    FlowLayoutPanel ResultFilters(TextBox search,params Control[] options) {
        var row=WorkflowRow();var label=TextLabel("Filter results",9,true);label.AutoSize=true;label.Margin=new(0,9,12,0);row.Controls.Add(label);
        search.Width=Math.Max(320,search.Width);search.AccessibleName="Filter results";row.Controls.Add(SearchField(search));
        foreach(var option in options)row.Controls.Add(option);return row;
    }
    Control SearchField(TextBox search) {
        var frame=new SearchFrame {Size=new(Math.Max(340,search.Width+24),42),Margin=new(0,0,12,0)};
        search.BorderStyle=BorderStyle.None;search.Location=new(12,10);search.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;search.Width=frame.Width-24;frame.Controls.Add(search);return frame;
    }
}

sealed class SearchFrame:Panel
{
    internal Color Line {get;set;}=Color.Gray;
    public SearchFrame(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var shape=ThemeShapes.Rounded(new Rectangle(0,0,Width-1,Height-1),8);using var pen=new Pen(ContainsFocus?ForeColor:Line,ContainsFocus?2:1);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.DrawPath(pen,shape);}
    protected override void OnEnter(EventArgs e){base.OnEnter(e);Invalidate();}
    protected override void OnLeave(EventArgs e){base.OnLeave(e);Invalidate();}
}

// Resolve actual file-system paths only. Descriptions, registry keys and web targets
// are never passed to the shell as commands.
static class ResultLocations
{
    internal static string? Resolve(object? tag,IEnumerable<string>? cells=null) {
        if(tag!=null)foreach(string property in new[]{"Path","Location","Target"}) {
            var value=tag.GetType().GetProperty(property)?.GetValue(tag) as string;
            var path=Existing(value);if(path!=null)return path;
        }
        if(tag?.GetType().GetProperty("Files")?.GetValue(tag) is System.Collections.IEnumerable files)foreach(var file in files){var path=Resolve(file);if(path!=null)return path;}
        if(cells!=null)foreach(string value in cells){var path=Existing(value);if(path!=null)return path;}
        return null;
    }
    static string? Existing(string? value) {
        if(string.IsNullOrWhiteSpace(value))return null;
        value=Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        if(!Path.IsPathFullyQualified(value))return null;
        int colon=value.IndexOf(':',2);if(colon>0&&File.Exists(value[..colon]))return value[..colon];
        if(File.Exists(value)||Directory.Exists(value))return value;
        var match=Regex.Match(value,"^(.+?\\.exe)(?:\"|\\s|$)",RegexOptions.IgnoreCase);
        if(match.Success&&File.Exists(match.Groups[1].Value))return match.Groups[1].Value;
        // NTFS streams open their owning file's location; do not launch stream data.
        if(Directory.Exists(Path.GetDirectoryName(value)))return value;
        return null;
    }
    internal static void Attach(Control control,Func<string?> path,Action<Point> select) {
        var menu=control.ContextMenuStrip??new ContextMenuStrip();control.ContextMenuStrip=menu;
        if(menu.Items.Cast<ToolStripItem>().Any(i=>i.Name=="open-result"))return;
        if(menu.Items.Count>0)menu.Items.Add(new ToolStripSeparator());
        var item=new ToolStripMenuItem("Open item") {Name="open-result"};var folder=new ToolStripMenuItem("Open folder");var copy=new ToolStripMenuItem("Copy full path");
        item.Click+=(_,_)=>{var target=path();if(target!=null)Launch(new ProcessStartInfo(target){UseShellExecute=true});};
        folder.Click+=(_,_)=>{var target=path();if(target==null)return;bool file=File.Exists(target);Launch(new ProcessStartInfo("explorer.exe"){UseShellExecute=true,Arguments=file?"/select,\""+target+"\"":"\""+(Directory.Exists(target)?target:Path.GetDirectoryName(target))+"\""});};
        copy.Click+=(_,_)=>{var target=path();if(target!=null)Clipboard.SetText(target);};
        menu.Items.AddRange([item,folder,copy]);menu.Opening+=(_,_)=>{var target=path();folder.Enabled=copy.Enabled=target!=null;item.Enabled=target!=null&&(File.Exists(target)||Directory.Exists(target));};
        menu.Items.Add("Expand results",null,(_,_)=>{var form=control.FindForm();var main=form as MainWindow??form?.Owner as MainWindow;main?.PopOutResults(control);});
        control.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Right)select(e.Location);};
    }
    static void Launch(ProcessStartInfo info){try{Process.Start(info);}catch(Exception e){MessageBox.Show("Windows could not open this item: "+e.Message,"Open result",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
}
