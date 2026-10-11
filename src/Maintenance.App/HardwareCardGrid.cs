namespace Maintenance.App;

// Keep each row aligned while retaining the content height of every card.
sealed class HardwareCardGrid:Panel
{
    readonly Dictionary<Control,int> contentHeights=[];
    bool arranging;
    public HardwareCardGrid(){AutoScroll=true;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
    protected override void OnControlAdded(ControlEventArgs e)
    {
        if(e.Control is {} card){card.Margin=Padding.Empty;contentHeights[card]=card.Height;card.SizeChanged+=CardSizeChanged;}
        base.OnControlAdded(e);PerformLayout();
    }
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if(e.Control is {} card){card.SizeChanged-=CardSizeChanged;contentHeights.Remove(card);}
        base.OnControlRemoved(e);
    }
    void CardSizeChanged(object? sender,EventArgs e){if(!arranging&&sender is Control card){contentHeights[card]=card.Height;PerformLayout();}}
    protected override void OnLayout(LayoutEventArgs e)
    {
        if(arranging)return;
        arranging=true;
        try{
            base.OnLayout(e);
            int gap=(int)(14*DeviceDpi/96f),available=Math.Max(1,ClientSize.Width-Padding.Horizontal-SystemInformation.VerticalScrollBarWidth);
            int columns=available>=1000?2:1,width=Math.Max(1,(available-gap*(columns-1))/columns),y=Padding.Top;
            var cards=Controls.Cast<Control>().ToArray();
            for(int row=0;row<cards.Length;row+=columns){int height=cards.Skip(row).Take(columns).Max(c=>contentHeights.GetValueOrDefault(c,c.Height));
                for(int col=0;col<columns&&row+col<cards.Length;col++)cards[row+col].SetBounds(Padding.Left+col*(width+gap)+AutoScrollPosition.X,y+AutoScrollPosition.Y,width,height);
                y+=height+gap;
            }
            AutoScrollMinSize=new(0,cards.Length==0?0:y-gap+Padding.Bottom);
            base.OnLayout(e);
        }finally{arranging=false;}
    }
}
