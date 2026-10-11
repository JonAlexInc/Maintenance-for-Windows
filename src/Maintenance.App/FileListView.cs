using System.Runtime.InteropServices;

namespace Maintenance.App;

sealed class FileListView : ListView
{
    public Color SelectionAccent {get;set;}=Color.FromArgb(34,116,204);
    bool groupCheckboxes;
    public bool GroupCheckboxes {get=>groupCheckboxes;set{if(groupCheckboxes==value)return;groupCheckboxes=value;groupChecks.Clear();Invalidate();}}
    readonly Dictionary<ListViewGroup,Rectangle> groupChecks=[];
    internal bool ClickFirstGroupCheckbox(){if(groupChecks.Count==0)return false;var r=groupChecks.First().Value;OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,r.Left+r.Width/2,r.Top+r.Height/2,0));return true;}
    internal void ToggleGroup(ListViewGroup group)
    {
        if(!GroupCheckboxes||!CheckBoxes)return;
        var rows=group.Items.Cast<ListViewItem>().Where(i=>i.ListView==this).ToArray();bool check=rows.Any(i=>!i.Checked);
        BeginUpdate();try{foreach(var row in rows)row.Checked=check;}finally{EndUpdate();}Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if(e.Button==MouseButtons.Left&&GroupCheckboxes&&CheckBoxes)foreach(var pair in groupChecks.ToArray())if(pair.Value.Contains(e.Location)){ToggleGroup(pair.Key);return;}
        base.OnMouseDown(e);
    }
    public FileListView() { DoubleBuffered = true; }
    // NMLVCUSTOMDRAW layout from the Windows common-controls API.
    [StructLayout(LayoutKind.Sequential)] struct Header { public IntPtr Window; public UIntPtr Id; public int Code; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Draw
    {
        public Header Header; public uint Stage; public IntPtr Hdc; public Rect Bounds;
        public UIntPtr Item; public uint State; public IntPtr Param;
    }
    [StructLayout(LayoutKind.Sequential)] struct ListDraw
    {
        public Draw Draw; public int Text, Background, SubItem; public uint ItemType;
        public int Face, Effect, Phase, Part, State; public Rect TextBounds; public uint Align;
    }
    [StructLayout(LayoutKind.Sequential)] struct GroupInfo
    {
        public uint Size, Mask; public IntPtr Header; public int HeaderLength; public IntPtr Footer; public int FooterLength, Id;
        public uint StateMask, State, Align; public IntPtr Subtitle; public uint SubtitleLength; public IntPtr Task; public uint TaskLength;
        public IntPtr DescriptionTop; public uint DescriptionTopLength; public IntPtr DescriptionBottom; public uint DescriptionBottomLength;
        public int TitleImage, ExtendedImage, FirstItem; public uint Items; public IntPtr Subset; public uint SubsetLength;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, int message, UIntPtr wParam, ref GroupInfo group);
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0xf)groupChecks.Clear();
        const int reflectedNotify = 0x204e, customDraw = -12;
        if (m.Msg == reflectedNotify && m.LParam != IntPtr.Zero && Marshal.PtrToStructure<Header>(m.LParam).Code == customDraw)
        {
            var draw = Marshal.PtrToStructure<ListDraw>(m.LParam);
            if (draw.ItemType == 1 && (draw.Draw.Stage == 0x10001 || draw.Draw.Stage == 1))
            {
                var buffer = Marshal.AllocHGlobal(32768);
                try
                {
                    var group = new GroupInfo { Size = (uint)Marshal.SizeOf<GroupInfo>(), Mask = 1, Header = buffer, HeaderLength = 16384 };
                    if (SendMessage(Handle, 0x1095, draw.Draw.Item, ref group).ToInt64() != -1)
                    {
                        using var graphics = Graphics.FromHdc(draw.Draw.Hdc);
                        var bounds = Rectangle.FromLTRB(draw.Draw.Bounds.Left, draw.Draw.Bounds.Top, draw.Draw.Bounds.Right, draw.Draw.Bounds.Top + Font.Height + 12);
                        bool dark=BackColor.GetBrightness()<.5f;
                        var highlight=Color.FromArgb((BackColor.R*4+SelectionAccent.R)/5,(BackColor.G*4+SelectionAccent.G)/5,(BackColor.B*4+SelectionAccent.B)/5);
                        var heading=dark?ControlPaint.Light(SelectionAccent,.5f):SelectionAccent;
                        using var brush = new SolidBrush(highlight); graphics.FillRectangle(brush, bounds);
                        using var stripe=new SolidBrush(heading);graphics.FillRectangle(stripe,bounds.Left,bounds.Top,4,bounds.Height);
                        bounds.Inflate(-8, 0);
                        string title=Marshal.PtrToStringUni(buffer)??"";
                        if(GroupCheckboxes&&CheckBoxes) {
                            var matching=Groups.Cast<ListViewGroup>().FirstOrDefault(g=>g.Header==title);
                            if(matching!=null) {
                                var box=new Rectangle(bounds.Left,bounds.Top+(bounds.Height-16)/2,16,16);groupChecks[matching]=box;
                                bool all=matching.Items.Count>0&&matching.Items.Cast<ListViewItem>().All(i=>i.Checked);
                                bool some=matching.Items.Cast<ListViewItem>().Any(i=>i.Checked);
                                if(some&&!all)ControlPaint.DrawMixedCheckBox(graphics,box,ButtonState.Checked);else ControlPaint.DrawCheckBox(graphics,box,all?ButtonState.Checked:ButtonState.Normal);
                                bounds.X+=26;bounds.Width-=26;
                            }
                        }
                        using var titleFont=new Font(Font,FontStyle.Bold);
                        TextRenderer.DrawText(graphics, title, titleFont, bounds, heading, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                }
                finally { Marshal.FreeHGlobal(buffer); }
                m.Result = (IntPtr)4;
                return;
            }
        }
        base.WndProc(ref m);
    }
}
