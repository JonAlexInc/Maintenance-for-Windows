using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Maintenance.App;
sealed partial class MainWindow
{
    record Preferences(string Measurements="US / inches",Dictionary<string,int>? CategoryColors=null,bool AutomaticUpdates=false);
    Preferences preferences=new();
    readonly System.Windows.Forms.Timer uptimeTimer=new(){Interval=1000};
    readonly Label uptimeLabel=new(){Dock=DockStyle.Top,Height=42,Padding=new(8),Font=new("Segoe UI",10,FontStyle.Bold)};
    void LoadPreferences(){try{preferences=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(store.Folder,"settings.json")))??new();}catch(IOException){}catch(JsonException){}ApplyCategoryColors();}
    void ApplyCategoryColors(){StorageKinds.Colors=StorageKinds.Labels.Select((name,i)=>preferences.CategoryColors?.TryGetValue(name,out int rgb)==true?Color.FromArgb(255,Color.FromArgb(rgb)):StorageKinds.DefaultColors[i]).ToArray();foreach(var bar in usageBars.Values)bar.Invalidate();foreach(var legend in categoryBars.Values)legend.Invalidate();}
    void ShowSettings()
    {
        using var dialog=new Form{Text="Settings",Size=new(800,850),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new(24)};
        var units=new AppearanceChoice{Width=360,Height=40,Text=preferences.Measurements};units.Items.AddRange(["US / inches","Metric / centimeters"]);
        var theme=new AppearanceChoice{Width=360,Height=40,Text=themeChoice.Text};theme.Items.AddRange(["Light","Dark","Follow Windows"]);
        var color=new AppearanceChoice{Width=360,Height=40,Text=accentChoice.Text};color.Items.AddRange(["Blue","Teal","Violet"]);
        foreach(var pair in new[]{("Measurement units",units),("Appearance",theme),("Accent color",color)}){layout.Controls.Add(new Label{Text=pair.Item1,AutoSize=true,Margin=new(0,10,0,5)});layout.Controls.Add(pair.Item2);}
        layout.Controls.Add(new Label{Text="Monitor dimensions use these units. File-size and benchmark units stay as labeled.",AutoSize=true,MaximumSize=new(650,0),Margin=new(0,14,0,10)});
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new(24,8,0,0)};
        var draftColors=(Color[])StorageKinds.Colors.Clone();var swatches=new List<AppearanceButton>();
        layout.Controls.Add(new Label{Text="Storage category colors",AutoSize=true,Font=new("Segoe UI",11,FontStyle.Bold),Margin=new(0,12,0,8)});
        var palette=new FlowLayoutPanel{Width=720,Height=294,WrapContents=true};
        void PaintSwatches(){for(int i=0;i<swatches.Count;i++){swatches[i].Margin=new(0,0,10,2);swatches[i].BackColor=draftColors[i];swatches[i].ForeColor=draftColors[i].GetBrightness()>.55?Color.Black:Color.White;swatches[i].Invalidate();}}
        for(int i=0;i<StorageKinds.Labels.Length;i++){int index=i;var pick=new AppearanceButton{Text=StorageKinds.Labels[i],Width=340,Height=38,Margin=new(0,0,10,2)};swatches.Add(pick);palette.Controls.Add(pick);pick.Click+=(_,_)=>{using var chooser=new ColorDialog{Color=draftColors[index],FullOpen=true};if(chooser.ShowDialog(dialog)==DialogResult.OK){draftColors[index]=chooser.Color;PaintSwatches();}};}
        layout.Controls.Add(palette);var defaults=new AppearanceButton{Text="Restore default colors",Width=230,Height=38};defaults.Click+=(_,_)=>{draftColors=(Color[])StorageKinds.DefaultColors.Clone();PaintSwatches();};layout.Controls.Add(defaults);
        var save=new AppearanceButton{Text="Save settings",Width=150,Height=36};var close=new AppearanceButton{Text="Cancel",Width=100,Height=36,DialogResult=DialogResult.Cancel};actions.Controls.Add(save);actions.Controls.Add(close);
        save.Click+=(_,_)=>{try{var next=new Preferences(units.Text,StorageKinds.Labels.Select((name,i)=>(name,i)).ToDictionary(p=>p.name,p=>draftColors[p.i].ToArgb()),preferences.AutomaticUpdates);File.WriteAllText(Path.Combine(store.Folder,"settings.json"),JsonSerializer.Serialize(next));preferences=next;ApplyCategoryColors();themeChoice.SelectedItem=theme.Text;accentChoice.SelectedItem=color.Text;RenderSystem();dialog.DialogResult=DialogResult.OK;}catch(IOException e){MessageBox.Show(dialog,"Settings could not be saved: "+e.Message);}};
        dialog.Controls.Add(layout);dialog.Controls.Add(actions);dialog.CancelButton=close;
        using var smokeClose=new System.Windows.Forms.Timer{Interval=400};
        if(smoke){smokeClose.Tick+=(_,_)=>{smokeClose.Stop();using var capture=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(capture,new Rectangle(0,0,dialog.Width,dialog.Height));capture.Save("artifacts/settings-preview.png");dialog.Close();};dialog.Shown+=(_,_)=>smokeClose.Start();}
        PaintTheme(dialog,surface);PaintSwatches();dialog.ShowDialog(this);
    }
    internal static string PanelDimensions(string raw,bool metric)
    {
        var match=Regex.Match(raw,@"^(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)\s*cm$");if(!match.Success)return raw;
        double w=double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),h=double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture),scale=metric?1:2.54;
        return $"{w/scale:N1} × {h/scale:N1} {(metric?"cm":"in")} · ≈{Math.Sqrt(w*w+h*h)/scale:N1} {(metric?"cm":"in")} diagonal (EDID)";
    }
    void UpdateUptime(){var up=TimeSpan.FromMilliseconds(Environment.TickCount64);uptimeLabel.Text=$"{up.Days:N0} {(up.Days==1?"day":"days")}  {up.Hours:00}:{up.Minutes:00}:{up.Seconds:00}";uptimeLabel.AccessibleName="Live system uptime";}
}



