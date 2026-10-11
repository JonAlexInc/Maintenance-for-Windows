using System.Text.Json;
namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly HardwarePages hardwareTabs=new() {Dock=DockStyle.Fill};
    readonly FlowLayoutPanel hardwareNavigation=new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(0,4,0,10)};
    readonly List<Button> hardwareButtons=[];
    readonly System.Windows.Forms.Timer systemSearchDelay=new() {Interval=250};
    readonly List<HardwareCardGrid> hardwarePanels=[];
    static readonly (string Title,string[] Sources,string[] Fields)[] hardwareKinds=
    [
        ("CPU",["Processors","CPU specifications"],["Manufacturer","NumberOfCores","NumberOfLogicalProcessors","MaxClockSpeed","SocketDesignation","L2CacheSize","L3CacheSize","VirtualizationFirmwareEnabled","SecondLevelAddressTranslationExtensions","AddressWidth"]),
        ("Motherboard",["Motherboard overview","Motherboard","BIOS and firmware","Board specifications"],["Manufacturer","Product","Version","SMBIOSBIOSVersion","ReleaseDate","Enabled"]),
        ("Graphics",["Graphics"],["VideoProcessor","DriverVersion","CurrentHorizontalResolution","CurrentVerticalResolution","CurrentRefreshRate"]),
        ("Memory",["Memory overview","Memory modules","Memory specifications"],["Manufacturer","PartNumber","Capacity","ConfiguredClockSpeed","DeviceLocator","BankLabel","DataWidth","TotalWidth","ConfiguredVoltage","SMBIOSMemoryType"]),
        ("Storage",["Physical disks"],["Model","Size","InterfaceType","MediaType","FirmwareRevision"]),
        ("Mouse",["Mouse"],["Interface count","Identification"]),
        ("Keyboard",["Keyboard"],["Interface count","Identification"]),
        ("Network",["Network adapters"],["Manufacturer","NetConnectionID","Speed","MACAddress"]),
        ("Audio",["Audio"],["Manufacturer","Status"]),
        ("Displays",["Connected displays","Displays"],["MonitorManufacturer","MonitorType","ScreenWidth","ScreenHeight","Manufacturer","Manufactured","Panel size","Connection","Active"]),
        ("Windows",["Windows overview","Operating system"],["Caption","OSArchitecture","LastBootUpTime"]),
        ("Other hardware",["Battery","Cooling","USB controllers","Input interfaces"],["Manufacturer","Status","EstimatedChargeRemaining"])
    ];
    void BuildSystem()
    {
        var body=Body(Page("System","Choose a hardware tab for a quick overview. Export the complete inventory when you need it."));
        var actions=Bar(60);actions.Dock=DockStyle.Top;
        var filters=ResultFilters(systemSearch,Button("Clear filter",()=>systemSearch.Clear()));
        systemSearch.Width=310;systemSearch.PlaceholderText="Search brand, model, or specification";
        actions.Controls.Add(StartSearch(RefreshSystem));systemStop=new AppearanceButton {Text="Cancel",Tag=CancelRole,Size=new(104,42),Enabled=false};systemStop.Click+=(_,_)=>systemSearchCancel?.Cancel();actions.Controls.Add(systemStop);actions.Controls.Add(Button("Export full inventory…",ExportSystem));
        systemState.Height=34;systemState.Font=new("Segoe UI",8);
        foreach(var kind in hardwareKinds)
        {
            var tab=new Panel {Text=kind.Title,Padding=new(0,10,0,0)};
            var panel=new HardwareCardGrid {Dock=DockStyle.Fill,Padding=new(4)};
            tab.Controls.Add(panel);hardwarePanels.Add(panel);hardwareTabs.TabPages.Add(tab);hardwareTabs.Controls.Add(tab);tab.Dock=DockStyle.Fill;
            int index=hardwareButtons.Count;
            var button=Button(kind.Title,()=>hardwareTabs.SelectedIndex=index);button.AutoSize=true;button.MinimumSize=new(72,40);button.Margin=new(0,0,8,8);hardwareButtons.Add(button);hardwareNavigation.Controls.Add(button);
        }
        body.Controls.Add(hardwareTabs);body.Controls.Add(hardwareNavigation);body.Controls.Add(systemState);body.Controls.Add(filters);body.Controls.Add(actions);
        systemSearch.TextChanged+=(_,_)=>{systemSearchDelay.Stop();systemSearchDelay.Start();};
        systemSearchDelay.Tick+=(_,_)=>{systemSearchDelay.Stop();RenderSystem();};FormClosed+=(_,_)=>systemSearchDelay.Dispose();
        hardwareTabs.SelectedIndexChanged+=(_,_)=>{StyleHardwareNavigation();UpdateUptime();};hardwareTabs.SelectedIndex=0;
        uptimeTimer.Tick+=(_,_)=>{if(activePage==5&&hardwareTabs.SelectedIndex==10)UpdateUptime();};uptimeTimer.Start();FormClosed+=(_,_)=>uptimeTimer.Dispose();
        RenderSystem();
    }
    string SystemExport(){var presented=HardwareView.Prepare(systemFacts);HardwareSpecs.Add(presented);return JsonSerializer.Serialize(new { Application="Maintenance for Windows", ExportedAt=DateTimeOffset.Now, Facts=systemFacts, Presented=presented },new JsonSerializerOptions {WriteIndented=true});}
    void ExportSystem()
    {
        if(systemFacts.Count==0){MessageBox.Show(this,"Wait for the hardware inventory to finish.");return;}
        using var dialog=new SaveFileDialog {Title="Export full inventory (includes device identifiers)",Filter="JSON inventory|*.json",FileName="Maintenance-system-inventory.json"};
        if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllText(dialog.FileName,SystemExport());
    }
    void RenderSystem()
    {
        var displayFacts=HardwareView.Prepare(systemFacts);HardwareSpecs.Add(displayFacts);
        string search=systemSearch.Text.Trim();int matches=0,firstMatch=-1;
        var driverIds=systemFacts.Where(f=>f.Section=="Signed drivers"&&f.Property=="DeviceID").GroupBy(f=>f.Value,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First().Component,StringComparer.OrdinalIgnoreCase);
        for(int index=0;index<hardwareKinds.Length;index++)
        {
            var kind=hardwareKinds[index];var panel=hardwarePanels[index];panel.SuspendLayout();
            if(index==10)uptimeLabel.Parent?.Controls.Remove(uptimeLabel);
            foreach(Control old in panel.Controls.Cast<Control>().ToArray()){panel.Controls.Remove(old);old.Dispose();}
            int count=0;
            if(index==10 && (search.Length==0 || "Windows uptime live system uptime".Contains(search,StringComparison.OrdinalIgnoreCase))) {
                var uptimeCard=new Panel{Width=panel.ClientSize.Width>=1000?(panel.ClientSize.Width-45)/2:Math.Max(350,panel.ClientSize.Width-30),Height=176,Padding=new(18),Margin=new(0,0,12,14),Tag=CardRole};
                var title=TextLabel("Windows uptime",12,true);title.Dock=DockStyle.Top;title.Height=36;
                var note=TextLabel("Kernel session duration. Sleep / Fast Startup can extend it.",9);note.Dock=DockStyle.Bottom;note.Height=48;
                uptimeLabel.Dock=DockStyle.Fill;uptimeLabel.Padding=Padding.Empty;uptimeLabel.Font=new("Segoe UI",18,FontStyle.Bold);
                uptimeCard.Controls.Add(uptimeLabel);uptimeCard.Controls.Add(note);uptimeCard.Controls.Add(title);panel.Controls.Add(uptimeCard);UpdateUptime();PaintTheme(uptimeCard,canvas);count++;
            }
            foreach(var device in displayFacts.Where(f=>kind.Sources.Contains(f.Section)).OrderBy(f=>Array.IndexOf(kind.Sources,f.Section)).GroupBy(f=>(f.Section,f.Component)))
            {
                var facts=device.ToList();string Value(string property)=>facts.FirstOrDefault(f=>f.Property==property&&UsefulValue(f.Value))?.Value.Trim()??"";
                if(device.Key.Section=="Network adapters"&&!Value("PhysicalAdapter").Equals("True",StringComparison.OrdinalIgnoreCase))continue;
                string title=new[]{Value("BusReportedName"),Value("Model"),Value("Product"),Value("Name"),Value("Description"),device.Key.Component}.First(v=>!string.IsNullOrWhiteSpace(v));
                if(device.Key.Section=="Memory modules")title=$"{Value("Manufacturer")} {Value("PartNumber")}".Trim();
                if(device.Key.Section=="Operating system")title=Value("Caption");
                if(device.Key.Section=="BIOS and firmware")title=device.Key.Component=="Secure Boot"?"Secure Boot":"BIOS / firmware";
                title=System.Text.RegularExpressions.Regex.Replace(title,@"\s*\[\d+\]$","");
                var rows=facts.Where(f=>(kind.Fields.Contains(f.Property)||device.Key.Section.EndsWith("specifications")||device.Key.Section.EndsWith("overview"))&&f.Property!="Source"&&UsefulValue(f.Value)).OrderBy(f=>Array.IndexOf(kind.Fields,f.Property)).Select(f=>(Label:ReadableProperty(f.Property),Value:FriendlyValue(f))).Distinct().ToList();
                string id=Value("PNPDeviceID");if(id.Length==0)id=Value("DeviceID");
                if(kind.Title=="Graphics")title=HardwareView.GpuTitle(Value("VideoProcessor"),id,title);
                if(!rows.Any(r=>r.Label=="Driver version")&&driverIds.TryGetValue(id,out var driver))
                {
                    var version=systemFacts.FirstOrDefault(f=>f.Section=="Signed drivers"&&f.Component==driver&&f.Property=="DriverVersion");
                    if(version is not null && UsefulValue(version.Value))rows.Add(("Driver version",version.Value));
                }
                if(kind.Title is "Graphics" or "Mouse" or "Keyboard") {
                    rows.AddRange(DeviceIdentity.Lookup(id));
                    if(Value("ParentReportedName").Length>0)rows.Add(("Connected device / receiver",Value("ParentReportedName")));
                    if(kind.Title=="Graphics")title=HardwareView.GpuTitle(Value("VideoProcessor"),id,title);
                if(!rows.Any(r=>r.Label=="USB vendor / product ID"))rows.AddRange(DeviceIdentity.Lookup(Value("ParentDeviceID")));

                }
                if(search.Length>0&&!$"{kind.Title} {title} {string.Join(" ",rows.Select(r=>r.Label+" "+r.Value))}".Contains(search,StringComparison.OrdinalIgnoreCase))continue;
                if(rows.Count==0)rows.Add(("Details","Not reported by Windows"));
                var card=new Panel {Width=panel.ClientSize.Width>=1000?(panel.ClientSize.Width-45)/2:Math.Max(350,panel.ClientSize.Width-30),Height=100+rows.Count*44,Padding=new(18),Margin=new(0,0,12,14),Tag=CardRole};
                var heading=TextLabel(title,12,true);heading.Dock=DockStyle.Top;heading.Height=62;heading.AutoEllipsis=false;heading.UseMnemonic=false;
                var table=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=rows.Count};table.ColumnStyles.Add(new(SizeType.Percent,40));table.ColumnStyles.Add(new(SizeType.Percent,60));
                for(int r=0;r<rows.Count;r++)
                {
                    table.RowStyles.Add(new(SizeType.Absolute,44));var label=TextLabel(rows[r].Label,8);label.Dock=DockStyle.Fill;
                    var value=TextLabel(rows[r].Value,9,true);value.Dock=DockStyle.Fill;value.UseMnemonic=false;table.Controls.Add(label,0,r);table.Controls.Add(value,1,r);
                }
                card.Controls.Add(table);
                HardwareMiniature? miniature=null;
                if(device.Key.Section=="Processors"&&int.TryParse(Value("NumberOfCores"),out int cores)&&cores>0)
                    miniature=new(){Kind="CPU",Count=cores,Occupied=cores,Caption=$"{cores} physical cores · {Value("NumberOfLogicalProcessors")} logical processors"};
                var occupancy=System.Text.RegularExpressions.Regex.Match(Value("DIMM occupancy"),@"^(\d+) of (\d+)");
                if(occupancy.Success)miniature=new(){Kind="Memory",Count=int.Parse(occupancy.Groups[2].Value),Occupied=int.Parse(occupancy.Groups[1].Value),Caption=Value("DIMM occupancy")};
                if(miniature!=null)card.Controls.Add(miniature);
                card.Controls.Add(heading);
                bool fittingCard=false;int fittedCardWidth=-1;
                void FitCard(){if(fittingCard||fittedCardWidth==card.ClientSize.Width)return;fittedCardWidth=card.ClientSize.Width;fittingCard=true;try{
                    int available=Math.Max(200,card.ClientSize.Width-card.Padding.Horizontal);
                    heading.Height=Math.Max(45,TextRenderer.MeasureText(title,heading.Font,new Size(available,10000),TextFormatFlags.WordBreak).Height+12);
                    int total=0;for(int r=0;r<rows.Count;r++){
                        var left=table.GetControlFromPosition(0,r)!;var right=table.GetControlFromPosition(1,r)!;
                        int h=Math.Max(38,Math.Max(TextRenderer.MeasureText(rows[r].Label,left.Font,new Size((int)(available*.4)-10,10000),TextFormatFlags.WordBreak).Height,TextRenderer.MeasureText(rows[r].Value,right.Font,new Size((int)(available*.6)-10,10000),TextFormatFlags.WordBreak).Height)+12);
                        table.RowStyles[r].Height=h;total+=h;
                    }
                    card.Height=total+heading.Height+card.Padding.Vertical+(miniature?.Height??0)+(Value("Source").StartsWith("https://")?32:0);
                }finally{fittingCard=false;}}
                card.SizeChanged+=(_,_)=>FitCard();FitCard();
                if(Value("Source").StartsWith("https://")) {
                    var link=new LinkLabel{Text="Manufacturer specifications · checked 10 Oct 2026",Dock=DockStyle.Bottom,Height=32,LinkColor=accent};string url=Value("Source");link.LinkClicked+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});card.Controls.Add(link);card.Height+=32;
                }
                panel.Controls.Add(card);PaintTheme(card,canvas);count++;
            }
            if(count==0)
            {
                var empty=TextLabel(systemFacts.Count==0?"Collecting hardware…":search.Length>0?"No matching hardware in this tab.":"No device information reported by Windows.",10);empty.Width=700;empty.Height=80;panel.Controls.Add(empty);PaintTheme(empty,canvas);
            }
            else if(firstMatch<0)firstMatch=index;
            matches+=count;hardwareTabs.TabPages[index].Text=kind.Title;panel.ResumeLayout(true);
        }
        if(search.Length>0&&firstMatch>=0)hardwareTabs.SelectedIndex=firstMatch;
        systemState.Text=search.Length>0?$"{matches} matching devices across hardware tabs":"Key specifications and available driver versions · Full raw inventory is available through Export.";
        hardwareTabs.Invalidate();
    }
    void StyleHardwareNavigation(){for(int i=0;i<hardwareButtons.Count;i++){hardwareButtons[i].Tag=i==hardwareTabs.SelectedIndex?PrimaryRole:null;StyleButton(hardwareButtons[i]);hardwareButtons[i].Margin=new(0,0,8,8);}}
}

// Plain panels avoid the native tab strip's unthemed frame and unused header area.
sealed class HardwarePages:Panel
{
    public List<Panel> TabPages {get;}=[];
    int selected=-1;
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex {get=>selected;set{if(value<0||value>=TabPages.Count)return;selected=value;for(int i=0;i<TabPages.Count;i++)TabPages[i].Visible=i==value;TabPages[value].BringToFront();SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}}
}









