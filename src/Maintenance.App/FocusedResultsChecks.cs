using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyFocusedResults()
    {
        try {
            SetBusy(false);SelectPage(1);scanLocation.SelectedItem="Downloads";
            string root=Directory.CreateDirectory(Path.GetFullPath(Path.Combine(".test-data","focused-"+Guid.NewGuid().ToString("N")))).FullName;
            File.WriteAllText(Path.Combine(root,"alpha.txt"),"alpha");File.WriteAllText(Path.Combine(root,"beta.txt"),"beta");
            await ScanLocalDrive(root);await renderTask;var parent=fileArea.Parent;
            PopOutResults();if(focusedResults==null||focusedResults.WindowState!=FormWindowState.Maximized||fileArea.FindForm()!=focusedResults)throw new Exception("Results did not open maximized");
            focusedResults.WindowState=FormWindowState.Normal;focusedResults.Size=new(1280,900);
            IEnumerable<Control> Desc(Control c)=>c.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Desc(child)));
            var filter=Desc(focusedResults).OfType<TextBox>().First(b=>b.AccessibleName=="Filter results");filter.Text="alpha";await Task.Delay(400);await renderTask;
            if(files.Items.Count!=1||nameFilter.Text!="alpha")throw new Exception("Focused filter is not shared with app results");
            if(!Desc(focusedResults).OfType<Button>().Any(b=>b.Text=="■  Stop all")||!Desc(focusedResults).OfType<Button>().Any(b=>b.Text==cleanButton.Text))throw new Exception("Focused commands missing");
            var selection=Desc(focusedResults).OfType<AppearanceCheckBox>().First(c=>c.Text=="Enable selection");selection.Checked=true;files.Items[0].Checked=true;await Task.Delay(60);
            if(!enableFileSelection.Checked||!cleanButton.Enabled)throw new Exception("Focused selection cannot enable review actions");
            themeChoice.SelectedItem="Dark";await Task.Delay(100);using(var image=new Bitmap(focusedResults.Width,focusedResults.Height)){focusedResults.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("artifacts/focused-results-Dark.png");}
            focusedResults.Close();if(fileArea.Parent!=parent||files.Items.Count!=1||nameFilter.Text!="alpha")throw new Exception("Closing focused view lost results or filter");
            nameFilter.Clear();await Task.Delay(400);await renderTask;
            scanLocation.SelectedItem=LeftoverMode;leftoverTabs.SelectedIndex=1;installedGrid.Rows.Clear();installedGrid.Rows.Add("Alpha app","Fixture","1",root,"Fixture");installedGrid.Rows.Add("Beta app","Fixture","1",root,"Fixture");
            var gridParent=installedGrid.Parent;PopOutResults();filter=Desc(focusedResults!).OfType<TextBox>().First(b=>b.AccessibleName=="Filter results");filter.Text="Alpha";
            if(installedGrid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible)!=1)throw new Exception("Focused table filtering failed");
            focusedResults!.Close();if(installedGrid.Parent!=gridParent||installedGrid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible)!=2)throw new Exception("Focused grid restoration failed");
            SelectPage(8);var duplicateParent=duplicateResultSurface!.Parent;PopOutResults(duplicateGroups);
            if(duplicateResultSurface.FindForm()!=focusedResults||duplicateFiles.FindForm()!=focusedResults)throw new Exception("Duplicate focused view separated groups from copies");focusedResults!.Close();
            if(duplicateResultSurface.Parent!=duplicateParent)throw new Exception("Duplicate panes did not return");
            SelectPage(1);scanLocation.SelectedItem=UpdateMode;updateList.Items.Clear();updateList.Items.Add(new ListViewItem(new[]{"Alpha", "1 KB", "Fixture", "Fixture"}));updateList.Items.Add(new ListViewItem(new[]{"Beta", "2 KB", "Fixture", "Fixture"}));
            PopOutResults();filter=Desc(focusedResults!).OfType<TextBox>().First(b=>b.AccessibleName=="Filter results");filter.Text="Alpha";if(updateList.Items.Count!=1)throw new Exception("Focused list filtering failed");
            updateList.Items.Clear();updateList.Items.Add(new ListViewItem(new[]{"New alpha", "3 KB", "Fixture", "Fixture"}));await Task.Delay(250);focusedResults!.Close();if(updateList.Items.Count!=1||updateList.Items[0].Text!="New alpha")throw new Exception("Focused list restored stale results after an update");
            string speedJournal;using(var journal=store.StartJournal()){speedJournal=journal.Name;LocalStore.Append(journal,new SpeedHistoryEntry(Guid.NewGuid().ToString("N"),"Internet speed","Europe · Italy · Rome","Download","Download",1,1000000,42,"Mbps",null,null,null,null,"Completed","Fixture target",DateTimeOffset.Now));}
            string contentJournal;string needle="nested-content-"+Guid.NewGuid().ToString("N");using(var journal=store.StartJournal()){contentJournal=journal.Name;LocalStore.Append(journal,new{Kind="Fixture scan",Summary="Generic scan label",Contents=new{Server=needle}});}
            RefreshHistory();SelectPage(4);historyTabs.SelectedIndex=0;historyFind.Text="ROME";
            if(!history.Items.Cast<HistoryItem>().Any(i=>i.Path==speedJournal)||speedHistoryGrid.Rows.Count==0)throw new Exception("All activity cannot find speed destination");
            historyFind.Text=needle;if(!history.Items.Cast<HistoryItem>().Any(i=>i.Path==contentJournal))throw new Exception("All activity ignores nested scan contents");
            historyFind.Text="Rome";PopOutResults();if(history.FindForm()!=focusedResults)throw new Exception("All activity cannot expand");focusedResults!.Close();historyFind.Clear();
            if(File.ReadAllText(Path.Combine(root,"alpha.txt"))!="alpha")throw new Exception("Review changed fixture data");
            File.WriteAllText("artifacts/focused-results-check-success.txt",DateTimeOffset.Now.ToString("o")+" · Maximized results, shared live filter, relevant commands, Stop all, return with rows/filter preserved, standalone table filter/restore, All activity destination and nested-content search passed. Fixtures only; no personal files removed.");
        }catch(Exception e){File.WriteAllText("artifacts/focused-results-check-failure.txt",e.ToString());throw;}
    }
}
