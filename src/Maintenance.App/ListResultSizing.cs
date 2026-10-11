using System.Runtime.CompilerServices;
namespace Maintenance.App;
static class ListResultSizing
{
    static readonly ConditionalWeakTable<ListView,State> states=new();
    public static void Attach(ListView list){if(list is FileListView||states.TryGetValue(list,out _))return;states.Add(list,new State(list));}
    sealed class State
    {
        readonly ListView list;readonly System.Windows.Forms.Timer delay=new(){Interval=180};
        readonly Dictionary<int,int> userWidths=new();bool fitting;int signature;
        public State(ListView value){list=value;list.Invalidated+=(_,_)=>{if(!fitting){delay.Stop();delay.Start();}};list.VisibleChanged+=(_,_)=>delay.Start();
            list.ColumnWidthChanged+=(_,e)=>{if(!fitting&&list.Capture)userWidths[e.ColumnIndex]=list.Columns[e.ColumnIndex].Width;};
            delay.Tick+=(_,_)=>Fit();list.Disposed+=(_,_)=>delay.Dispose();}
        void Fit(){delay.Stop();if(!list.Visible||list.IsDisposed||list.View!=View.Details)return;
            var hash=new HashCode();hash.Add(list.Font);hash.Add(list.Items.Count);foreach(ColumnHeader col in list.Columns)hash.Add(col.Text);
            foreach(ListViewItem row in list.Items)foreach(ListViewItem.ListViewSubItem cell in row.SubItems)hash.Add(cell.Text);
            int next=hash.ToHashCode();if(next==signature)return;signature=next;fitting=true;
            try{list.BeginUpdate();for(int c=0;c<list.Columns.Count;c++){
                int width=TextRenderer.MeasureText(list.Columns[c].Text,list.Font).Width+40;
                foreach(ListViewItem row in list.Items)if(row.SubItems.Count>c)width=Math.Max(width,TextRenderer.MeasureText(row.SubItems[c].Text,list.Font).Width+32);
                list.Columns[c].Width=userWidths.GetValueOrDefault(c,Math.Clamp(width,90,30000));
            }}finally{list.EndUpdate();fitting=false;}
        }
    }
}
