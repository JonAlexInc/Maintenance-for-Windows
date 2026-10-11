using System.Runtime.InteropServices;
using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly AppearanceChoice reviewFocus=new() { Width=205,Height=38,Text="All files",AccessibleName="Review focus" };
    readonly Label scopeHint=new() { AutoSize=false,Width=700,Height=48,Padding=new(8,5,0,0) };
    readonly Label pagePosition=new() { AutoSize=true,Padding=new(8,10,8,0) };
    readonly NumericUpDown jumpPage=new() { Minimum=1,Maximum=1,Width=85,AccessibleName="Result page" };
    string? chosenReviewFolder;
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHGetKnownFolderPath(in Guid id,uint flags,IntPtr token,out IntPtr path);
    static string DownloadsFolder()
    {
        var id=new Guid("374DE290-123F-4565-9164-39C4925E467B");
        int result=SHGetKnownFolderPath(in id,0,IntPtr.Zero,out var path);
        if(result!=0) { if(path!=IntPtr.Zero)Marshal.FreeCoTaskMem(path);return ""; }
        try{return Marshal.PtrToStringUni(path)??"";}finally{Marshal.FreeCoTaskMem(path);}
    }
    void ChooseReviewFolder()
    {
        using var dialog=new FolderBrowserDialog { Description="Choose a folder to review",UseDescriptionForTitle=true,ShowNewFolderButton=false };
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        var path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(dialog.SelectedPath));
        if(path==Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path)!)) { MessageBox.Show(this,"Choose a specific folder, such as Downloads, instead of an entire drive.");return; }
        if(!IsLocalStorage(new DriveInfo(Path.GetPathRoot(path)!).DriveType)) {MessageBox.Show(this,"Choose a folder on a local drive.");return;}
        chosenReviewFolder=path;RefreshScanLocations();scanLocation.SelectedItem="Chosen folder";scopeHint.Text="Review only · "+path;
    }
    Control BuildResultPager()
    {
        var pager=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=52,WrapContents=false,Padding=new(0,5,0,0) };
        previousFiles.Text="‹ Previous page";nextFiles.Text="Next page ›";previousFiles.Padding=nextFiles.Padding=new(10,5,10,5);
        pager.Controls.Add(previousFiles);pager.Controls.Add(pagePosition);pager.Controls.Add(nextFiles);
        pager.Controls.Add(new Label {Text="Go to page",AutoSize=true,Padding=new(12,10,0,0)});pager.Controls.Add(jumpPage);
        pager.Controls.Add(Button("Go",()=>MoveFilePage((int)jumpPage.Value-1)));
        return pager;
    }
    void MoveFilePage(int page)
    {
        if(busy||pendingFilter||scan is null)return;
        filePage=Math.Clamp(page,0,Math.Max(0,(filteredRows.Count-1)/FilePageSize));fileColumnsSized=false;RenderFilePage();
        if(files.Items.Count>0)files.EnsureVisible(0);
    }
    static bool MatchesReview(Candidate c,string focus)
    {
        return focus switch {"Large files"=>c.Size>=100L*1024*1024,"Older than 90 days"=>c.Written<DateTime.UtcNow.AddDays(-90).ToFileTimeUtc(),"Installers & archives"=>new[]{".exe",".msi",".zip",".7z",".rar",".iso"}.Contains(Path.GetExtension(c.Path),StringComparer.OrdinalIgnoreCase),_=>true};
    }
    string ReviewReason(Candidate c)
    {
        if(streamReview)return "Named stream: "+StreamCatalog.StreamName(c.Path)+" · Owner: "+StreamCatalog.OwnerPath(c.Path)+" · Metadata is not proof of malware. Read-only.";
        if(!driveReview)return "Checked temporary-file source · created and modified at least 7 days ago · rechecked before deletion. Not a guarantee that the file is unused.";
        if(MatchesReview(c,"Installers & archives"))return "Installer or archive · may still be needed. Review its purpose before removing it.";
        if(MatchesReview(c,"Large files"))return "Large file · size alone does not make it safe to remove.";
        if(MatchesReview(c,"Older than 90 days"))return "Not modified in 90 days · may still be important.";
        return "Personal file · no automatic removal recommendation.";
    }
}


