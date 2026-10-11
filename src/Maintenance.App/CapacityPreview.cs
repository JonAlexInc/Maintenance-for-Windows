namespace Maintenance.App;
sealed partial class MainWindow
{
    void CaptureCapacityPreview()
    {
        using var sheet=new Bitmap(1280,560);using var g=Graphics.FromImage(sheet);
        double[] fractions=[0,.00045,.145,.728,.832,.95,1];
        for(int theme=0;theme<2;theme++)for(int row=0;row<fractions.Length;row++){
            using var gauge=new UsageBar{Size=new(620,76),Fraction=fractions[row],BackColor=theme==0?Color.FromArgb(34,41,49):Color.White,ForeColor=theme==0?Color.FromArgb(237,243,250):Color.FromArgb(25,42,61),Font=new("Segoe UI",9)};
            gauge.UsedBytes=1000;gauge.Values=[170,120,90,60,20,40,250,30,10,50,10,0,40];
            using var cell=new Bitmap(620,76);gauge.DrawToBitmap(cell,new Rectangle(0,0,620,76));g.DrawImageUnscaled(cell,theme*640,row*80);
        }
        sheet.Save("artifacts/capacity-gauge-preview.png");SelectPage(0);themeChoice.SelectedItem="Dark";Refresh();
        using var window=new Bitmap(Width,Height);DrawToBitmap(window,new Rectangle(0,0,Width,Height));window.Save("artifacts/capacity-overview-preview.png");
        File.WriteAllText("artifacts/capacity-preview-success.txt",DateTimeOffset.Now.ToString("o"));
        SelectPage(1);scanLocation.PerformClick();
        if(scanLocation.ActiveMenu is {} menu){using var picture=new Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(picture,new Rectangle(Point.Empty,menu.Size));picture.Save("artifacts/scan-menu-preview.png");menu.Close();}
    }
}
