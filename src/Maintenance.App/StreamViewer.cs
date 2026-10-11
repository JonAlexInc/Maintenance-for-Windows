using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    Button? viewStreamButton;
    async Task ShowStreamInfo()
    {
        if(!streamReview||files.SelectedItems.Count==0)return;
        var candidate=(Candidate)files.SelectedItems[0].Tag!;
        using var dialog=new Form{Text="Alternate data stream",Size=new(1050,760),MinimumSize=new(780,520),StartPosition=FormStartPosition.CenterParent};
        var heading=new Label{Dock=DockStyle.Top,Height=72,Font=new("Segoe UI",15,FontStyle.Bold),Padding=new(14),Text=StreamCatalog.StreamName(candidate.Path)};
        var owner=new TextBox{Dock=DockStyle.Top,Height=82,Multiline=true,ReadOnly=true,WordWrap=false,ScrollBars=ScrollBars.Both,Text="File: "+StreamCatalog.OwnerPath(candidate.Path)+"\r\nStream: "+candidate.Path};
        var summary=new Label{Dock=DockStyle.Top,Height=55,Padding=new(12),Text=$"Scan size: {FormatBytes(candidate.Size)} · Reading preview…"};
        var tabs=new HardwarePages{Dock=DockStyle.Fill};
        TextBox Add(string title,bool mono=true){var tab=new Panel{Text=title,Dock=DockStyle.Fill};var box=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new(mono?"Consolas":"Segoe UI",10),MaxLength=0};tab.Controls.Add(box);tabs.TabPages.Add(tab);tabs.Controls.Add(tab);return box;}
        var text=Add("Text preview");var hex=Add("Hex bytes");TextBox? origin=StreamCatalog.StreamName(candidate.Path).Equals("Zone.Identifier",StringComparison.OrdinalIgnoreCase)?Add("Download origin",false):null;
        var note=new Label{Dock=DockStyle.Bottom,Height=74,Padding=new(12),Text="Read-only · At most 64 KiB loaded · Content is not executed. URLs are plain text. A stream is not by itself evidence of malware."};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=52,Padding=new(12,6,0,0)};
        var copy=new AppearanceButton{Text="Copy stream path",Width=180,Height=34};copy.Click+=(_,_)=>Clipboard.SetText(candidate.Path);actions.Controls.Add(copy);
        dialog.Controls.Add(tabs);dialog.Controls.Add(ModernTabs(tabs));dialog.Controls.Add(summary);dialog.Controls.Add(owner);dialog.Controls.Add(heading);dialog.Controls.Add(note);dialog.Controls.Add(actions);
        dialog.Shown+=async(_,_)=>{
            try {
                var preview=await Task.Run(()=>StreamPreview.Read(candidate.Path)).WaitAsync(TimeSpan.FromSeconds(10));if(dialog.IsDisposed)return;
                text.Text=preview.Text;hex.Text=preview.Hex();summary.Text=$"{FormatBytes(preview.Size)} current stream size · {preview.Bytes.Length:N0} bytes shown · {(preview.Truncated?"Partial preview":"Entire stream loaded")} · {(preview.Binary?"Binary / mixed encoding":"Text")}";
                if(preview.Binary)tabs.SelectedIndex=1;
                if(origin!=null){origin.WordWrap=true;origin.Text=OriginDetails(preview.Text);tabs.SelectedIndex=2;origin.SelectionLength=0;}copy.Focus();
            }catch(Exception e){if(!dialog.IsDisposed)summary.Text=e is TimeoutException?"Preview timed out. The drive or network may be unavailable.":"Cannot read this stream: "+e.Message;}
        };
        using var smokeClose=new System.Windows.Forms.Timer{Interval=500};int attempts=0;
        if(smoke){smokeClose.Tick+=(_,_)=>{if(summary.Text.Contains("Reading preview")&&attempts++<24)return;smokeClose.Stop();using var capture=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(capture,new Rectangle(0,0,dialog.Width,dialog.Height));capture.Save("artifacts/stream-viewer.png");dialog.Close();};dialog.Shown+=(_,_)=>smokeClose.Start();}
        PaintTheme(dialog,surface);dialog.ShowDialog(this);await Task.CompletedTask;
    }
    internal static string OriginDetails(string content)
    {
        var lines=new List<string>{"Windows download-origin metadata","These values are file metadata, not a verified provenance or safety verdict.",""};
        foreach(string line in content.Split('\n')){
            int split=line.IndexOf('=');if(split<1)continue;string key=line[..split].Trim(),value=line[(split+1)..].Trim();
            if(key.Equals("ZoneId",StringComparison.OrdinalIgnoreCase))value=value switch{"0"=>"0 · Local computer","1"=>"1 · Local intranet","2"=>"2 · Trusted sites","3"=>"3 · Internet","4"=>"4 · Restricted sites",_=>value};
            string title=key switch{"HostUrl"=>"Source URL","ReferrerUrl"=>"Referrer URL","ZoneId"=>"Security zone",_=>key};lines.Add(title+": "+value);
        }
        if(lines.Count==3)lines.Add("No recognized key/value fields. See the raw text preview.");return string.Join("\r\n\r\n",lines);
    }
}


