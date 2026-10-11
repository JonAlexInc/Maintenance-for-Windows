using System.Text.Json;
namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly AppearanceCheckBox automaticUpdates=new(){AutoSize=true,Height=42,AccessibleName="Automatic updates preference"};
    readonly Label updatePreferenceState=new(){AutoSize=true,MaximumSize=new(780,0),Margin=new(0,10,0,0)};
    bool loadingUpdatePreference;
    void BuildUpdates()
    {
        var body=Body(Page("Updates","App version, update preferences, and your license."));body.AutoScroll=true;
        var stack=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};body.Controls.Add(stack);
        FlowLayoutPanel Section(string title){var card=new FlowLayoutPanel{Width=880,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new(880,0),Padding=new(22),Margin=new(0,0,0,16),FlowDirection=FlowDirection.TopDown,WrapContents=false,Tag=CardRole};var heading=TextLabel(title,14,true);heading.AutoSize=true;heading.Margin=new(0,0,0,14);card.Controls.Add(heading);stack.Controls.Add(card);return card;}
        void Copy(Control card,string text){card.Controls.Add(new Label{Text=text,AutoSize=true,MaximumSize=new(800,0),Margin=new(0,0,0,12)});}
        var version=Section("Maintenance for Windows");Copy(version,"Version 0.01 Preview · Personal edition");Copy(version,"Updates are coming soon. This preview does not check for, download, or install updates.");
        var check=Button("Check for updates · Coming soon",()=>{});check.Enabled=false;version.Controls.Add(check);
        var options=Section("Automatic updates");Copy(options,"Choose your preference for when automatic updates become available. Saved on this computer; no update activity runs yet.");
        loadingUpdatePreference=true;automaticUpdates.Checked=preferences.AutomaticUpdates;loadingUpdatePreference=false;options.Controls.Add(automaticUpdates);options.Controls.Add(updatePreferenceState);RefreshUpdatePreference();
        automaticUpdates.CheckedChanged+=(_,_)=>{
            if(loadingUpdatePreference)return;
            try{var next=preferences with{AutomaticUpdates=automaticUpdates.Checked};File.WriteAllText(Path.Combine(store.Folder,"settings.json"),JsonSerializer.Serialize(next));preferences=next;RefreshUpdatePreference();}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException){loadingUpdatePreference=true;automaticUpdates.Checked=preferences.AutomaticUpdates;loadingUpdatePreference=false;RefreshUpdatePreference();updatePreferenceState.Text="Could not save this preference: "+e.Message;}
        };
        var license=Section("Personal license");Copy(license,"Free personal edition · Default license");Copy(license,"Technician tools and licensing are not available in this preview.");
    }
    void RefreshUpdatePreference(){automaticUpdates.Text=automaticUpdates.Checked?"Automatic updates: On":"Automatic updates: Off";updatePreferenceState.Text=automaticUpdates.Checked?"Preference saved: On · Waiting for the updater to become available.":"Preference saved: Off · Automatic updates are not active.";}
    void VerifyUpdatesPage()
    {
        SetBusy(false);SelectPage(6);bool original=preferences.AutomaticUpdates;
        try{foreach(bool enabled in new[]{true,false}){automaticUpdates.Checked=enabled;var persisted=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(store.Folder,"settings.json")))!;if(persisted.AutomaticUpdates!=enabled)throw new InvalidOperationException("Update preference was not saved");}
            foreach(string theme in new[]{"Light","Dark"}){themeChoice.SelectedItem=theme;Refresh();using var capture=new Bitmap(Width,Height);DrawToBitmap(capture,new Rectangle(0,0,Width,Height));capture.Save("artifacts/updates-"+theme.ToLowerInvariant()+".png");}
            File.WriteAllText("artifacts/updates-check-success.txt",DateTimeOffset.Now.ToString("o"));
        }finally{automaticUpdates.Checked=original;}
    }
}
