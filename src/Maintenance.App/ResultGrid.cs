namespace Maintenance.App;

// Fit after a batch of results, not once for every inserted row. Explicit user widths survive refreshes.
sealed class ResultGrid : DataGridView
{
    readonly System.Windows.Forms.Timer fitting = new() { Interval = 140 };
    readonly Dictionary<string,int> userWidths = new();
    bool sizing;
    readonly Dictionary<int,double> maxima = new();
    public ResultGrid()
    {
        Dock=DockStyle.Fill;ReadOnly=true;AllowUserToAddRows=false;AllowUserToDeleteRows=false;
        RowHeadersVisible=false;SelectionMode=DataGridViewSelectionMode.FullRowSelect;MultiSelect=false;
        BorderStyle=BorderStyle.None;RowTemplate.Height=38;ScrollBars=ScrollBars.Both;
        ClipboardCopyMode=DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        DefaultCellStyle.Padding=new Padding(5,3,5,7);
        ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.False;
        ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        fitting.Tick+=(_,_)=>{fitting.Stop();if(Visible)FitResults();};
        RowsAdded+=(_,_)=>QueueFit();RowsRemoved+=(_,_)=>QueueFit();CellValueChanged+=(_,_)=>QueueFit();
        VisibleChanged+=(_,_)=>QueueFit();FontChanged+=(_,_)=>QueueFit();
        ColumnWidthChanged+=(_,e)=>{if(!sizing&&Capture&&e.Column!=null){userWidths[e.Column.Name]=e.Column.Width;QueueFit();}};
        ColumnDividerDoubleClick+=(_,e)=>{if(e.ColumnIndex>=0){userWidths.Remove(Columns[e.ColumnIndex].Name);QueueFit();}};
        var menu=new ContextMenuStrip();menu.Items.Add("Fit all columns to results",null,(_,_)=>{userWidths.Clear();FitResults();});ContextMenuStrip=menu;
        ResultLocations.Attach(this,()=>CurrentRow==null?null:ResultLocations.Resolve(CurrentRow.Tag,CurrentRow.Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.Value)??"")),point=>{var hit=HitTest(point.X,point.Y);if(hit.RowIndex>=0){ClearSelection();Rows[hit.RowIndex].Selected=true;CurrentCell=Rows[hit.RowIndex].Cells[Math.Max(0,hit.ColumnIndex)];}});
    }
    void QueueFit(){if(IsDisposed)return;fitting.Stop();fitting.Start();}
    internal void FitResults()
    {
        if(sizing||Columns.Count==0||IsDisposed)return;
        sizing=true;SuspendLayout();
        try {
            maxima.Clear();AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
            foreach(DataGridViewColumn column in Columns) {
                column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;
                int preferred=column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells,true)+16;
                column.Width=userWidths.GetValueOrDefault(column.Name,Math.Clamp(preferred,95,1400));
                column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
                if(IsQuantity(column))maxima[column.Index]=Rows.Cast<DataGridViewRow>().Select(r=>Number(r.Cells[column.Index].Value)).DefaultIfEmpty().Max();
            }
                        AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
            foreach(DataGridViewRow row in Rows){int height=38;
                foreach(DataGridViewCell cell in row.Cells){if(!Columns[cell.ColumnIndex].Visible)continue;
                    var style=cell.InheritedStyle;int available=Math.Max(30,Columns[cell.ColumnIndex].Width-style.Padding.Horizontal-12);
                    string text=Convert.ToString(cell.FormattedValue)??"";
                    int measured=TextRenderer.MeasureText(text,style.Font??Font,new Size(available,100000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+style.Padding.Vertical+8;
                    height=Math.Max(height,measured);
                }
                row.Height=Math.Min(65536,height);
            }
        } finally {ResumeLayout();sizing=false;Invalidate();}
    }
    static bool IsQuantity(DataGridViewColumn c)=>new[]{"Size","Measured size","Extra size","Copies","Files","Size (bytes)","Days since record"}.Contains(c.Name);
    static double Number(object? value)=>value is long l?Math.Max(0,l):value is int i?Math.Max(0,i):value is double d&&double.IsFinite(d)?Math.Max(0,d):0;
    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if(e.Handled||e.RowIndex<0||e.ColumnIndex<0)return;
        if(maxima.TryGetValue(e.ColumnIndex,out double max)&&max>0) {
            double value=Number(e.Value);if(value<=0)return;
            e.Paint(e.ClipBounds,DataGridViewPaintParts.All);
            using var brush=new SolidBrush((e.State&DataGridViewElementStates.Selected)!=0?DefaultCellStyle.SelectionForeColor:DefaultCellStyle.SelectionBackColor);
            int width=(int)((e.CellBounds.Width-12)*Math.Clamp(value/max,0,1));
            e.Graphics!.FillRectangle(brush,e.CellBounds.Left+6,e.CellBounds.Bottom-5,Math.Max(2,width),3);e.Handled=true;
        }
    }
    protected override void Dispose(bool disposing){if(disposing){fitting.Dispose();ContextMenuStrip?.Dispose();}base.Dispose(disposing);}
}

sealed class InsightStrip : Control
{
    public (string Label,long Value,Color Color)[] Segments {get;private set;}=[];
    public string Caption {get;private set;}="";
    public InsightStrip(){Dock=DockStyle.Top;Height=66;DoubleBuffered=true;Padding=new(10);}
    public void ShowData(string caption,params (string Label,long Value,Color Color)[] segments){Caption=caption;Segments=segments;AccessibleName=caption+". "+string.Join(". ",segments.Select(s=>$"{s.Label}: {s.Value:N0}"));Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);int width=Math.Max(1,Width-20);double total=Segments.Sum(s=>(double)Math.Max(0,s.Value));
        TextRenderer.DrawText(e.Graphics,Caption,Font,new Rectangle(10,4,width,22),ForeColor,TextFormatFlags.EndEllipsis);
        if(total<=0){TextRenderer.DrawText(e.Graphics,"No measured data yet",Font,new Point(10,32),ForeColor);return;}
        float x=10;foreach(var s in Segments.Where(s=>s.Value>0)){float w=(float)(width*s.Value/total);using var brush=new SolidBrush(s.Color);e.Graphics!.FillRectangle(brush,x,30,w,6);x+=w;}
        TextRenderer.DrawText(e.Graphics,string.Join("   ·   ",Segments.Where(s=>s.Value>0).Select(s=>s.Label)),Font,new Rectangle(10,40,width,23),ForeColor,TextFormatFlags.EndEllipsis);
    }
}



