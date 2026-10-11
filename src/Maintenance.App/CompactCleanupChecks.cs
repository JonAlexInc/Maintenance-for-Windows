namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyCompactCleanup()
    {
        try {
            SetBusy(false);SelectPage(1);scanLocation.SelectedItem="Downloads";
            string root=Directory.CreateDirectory(Path.GetFullPath(Path.Combine(".test-data","compact-"+Guid.NewGuid().ToString("N")))).FullName;
            File.WriteAllText(Path.Combine(root,"alpha.txt"),"alpha");File.WriteAllText(Path.Combine(root,"beta.txt"),"beta");
            await ScanLocalDrive(root);await renderTask;
            if(filtersExpanded||filterContainer!.Visible||filterContainer.Height!=0)throw new Exception("Filters should start collapsed");
            foreach(var size in new[]{new Size(1760,1120),new Size(1280,900)})foreach(string theme in new[]{"Dark","Light"}){
                Size=size;themeChoice.SelectedItem=theme;await Task.Delay(80);
                int collapsedHeight=fileArea.Height;
                using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save($"artifacts/compact-{size.Width}-{theme}-collapsed.png");}
                filterToggle.PerformClick();await Task.Delay(80);
                if(!filterContainer.Visible||fileArea.Height>=collapsedHeight)throw new Exception("Expanding filters did not use result space");
                foreach(Control c in filterBar!.Controls)if(c.Right>filterBar.ClientSize.Width+2||c.Bottom>filterBar.ClientSize.Height+2)throw new Exception("Filter control clipped: "+c.Text);
                using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save($"artifacts/compact-{size.Width}-{theme}-expanded.png");}
                filterToggle.PerformClick();
            }
            nameFilter.Text="alpha";await Task.Delay(400);await renderTask;
            if(files.Items.Count!=1||ActiveCleanupFilters!=1||!filterToggle.Text.Contains("1 active")||filterContainer.Visible)throw new Exception("Collapsed filters lost their criteria or active indicator");
            minSize.Value=1;hideEmpty.Checked=true;reviewFocus.SelectedItem="Older than 90 days";
            if(ActiveCleanupFilters!=4)throw new Exception("Active criteria count incorrect");
            var reset=filterBar!.Controls.OfType<Button>().First(b=>b.Text=="Reset filters");
            filterToggle.PerformClick();reset.PerformClick();await Task.Delay(400);await renderTask;
            if(ActiveCleanupFilters!=0||files.Items.Count!=2)throw new Exception("Reset did not clear all narrowing criteria");
            filterToggle.PerformClick();scanLocation.SelectedItem=UpdateMode;scanLocation.SelectedItem="Downloads";
            if(filterContainer.Visible||filtersExpanded)throw new Exception("Scan mode change reopened collapsed filters");
            filterToggle.PerformClick();scanLocation.SelectedItem=RestoreMode;
            if(filterContainer.Visible)throw new Exception("Special mode displayed file filters");
            scanLocation.SelectedItem="Downloads";
            if(!filterContainer.Visible||!filtersExpanded)throw new Exception("Expanded preference lost across modes");
            File.WriteAllText("artifacts/compact-cleanup-check-success.txt",DateTimeOffset.Now.ToString("o")+" · Default collapse, result-space recovery, wrapped controls at two sizes in light/dark, active criteria count, hidden filtering, full reset and mode restoration passed. Generated fixtures only; no deletion.");
        }catch(Exception e){File.WriteAllText("artifacts/compact-cleanup-check-failure.txt",e.ToString());throw;}
    }
}
