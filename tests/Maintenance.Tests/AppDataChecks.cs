using Maintenance.Core;
static class AppDataChecks
{
    public static int Run(string root)
    {
        int passed=0;
        void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS: "+label);passed++;}
        string local=Directory.CreateDirectory(Path.Combine(root,"app-map")).FullName;
        string folder=Directory.CreateDirectory(Path.Combine(local,"Example","nested","deeper")).FullName;
        File.WriteAllBytes(Path.Combine(folder,"settings.db"),new byte[1234]);
        string owner=Path.Combine(local,"Example");
        var scan=AppDataMap.Scan([("Local",local)],[],[],CancellationToken.None);
        Check(scan.Folders.Count==1&&scan.Folders[0].Bytes==1234&&scan.Folders[0].Files==1&&!scan.Partial,"app data recursively measures nested settings without modifying them");
        Check(scan.Folders[0].Association=="Ownership unknown"&&File.Exists(Path.Combine(folder,"settings.db")),"unknown ownership is not abandonment");
        Check(AppDataMap.Associate(owner,[new("Example","C:\\Elsewhere")],[]).State=="Name match only","name match stays unverified");
        Check(AppDataMap.Associate(owner,[new("Registered",Path.Combine(owner,"bin"))],[]).State=="Install location present","install path evidence is attributed");
        Check(AppDataMap.Associate(owner,[],[Path.Combine(owner,"bin","app.exe")]).State=="Running app present","running path evidence is attributed");
        Check(AppDataMap.Associate(owner,[],[owner+"Two\\app.exe"]).State=="Ownership unknown","path prefix lookalikes do not establish ownership");
        var limited=AppDataMap.Scan([("Local",local)],[],[],CancellationToken.None,entryLimit:2);
        Check(limited.Partial&&limited.Issues.Count>0&&limited.Folders[0].Partial,"entry budget marks incomplete measurements");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Check(AppDataMap.Scan([("Local",local)],[],[],cancellation.Token).Partial,"cancellation marks partial map");
        var missing=AppDataMap.Scan([("Missing",Path.Combine(local,"absent"))],[],[],CancellationToken.None);
        Check(missing.Partial&&missing.Issues.Count>0,"inaccessible or missing scope is reported");
        string linked=Path.Combine(owner,"redirected");
        using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",$"/c mklink /J \"{linked}\" \"{folder}\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true})) {
            process!.WaitForExit();if(process.ExitCode!=0)throw new Exception("Cannot create map junction fixture");
        }
        var links=AppDataMap.Scan([("Local",local)],[],[],CancellationToken.None);
        Check(links.Folders[0].Bytes==1234&&links.Folders[0].Skipped==1&&links.Folders[0].Partial,"linked folders are omitted and not double counted");
        Check(scan.Folders[0].Categories.Values.Sum()==scan.Folders[0].Bytes,"composition totals reconcile with measured bytes");
        Check(AppDataMap.Category(Path.Combine("Cache","one.bin"))=="Cache candidates"&&AppDataMap.Category("settings.db")=="Settings / other data","cache candidates stay distinct from valuable unknown data");
        var data=new AppDataFolder("Code","Roaming",Path.Combine(local,"Code"),100,2,0,false,"Ownership unknown","");
        var empty=new PresenceEvidence([],[],[],[],true);
        Check(AppRemnants.Assess(data,empty,["User","logs"],true,false).State=="Likely leftovers","recognized layout and completed absence checks yield qualified likelihood");
        Check(AppRemnants.Assess(data,empty,[],true,false).State=="Ownership needs review","folder name alone cannot establish leftovers");
        Check(AppRemnants.Assess(data,empty with{InstalledNames=["Microsoft Visual Studio Code (User)"]},["User","logs"],true,false).State=="App present / referenced","installed app overrides absence assessment");
        Check(AppRemnants.Assess(data,empty,["User","logs"],true,true).State=="App present / referenced","discovered portable binary prevents remnant recommendation");
        Check(AppRemnants.Assess(data,empty with{CatalogsComplete=false},["User","logs"],true,false).State=="Assessment incomplete","inventory gaps block likely remnant assessment");
        Check(AppRemnants.Assess(data with{Partial=true},empty,["User","logs"],true,false).State=="Assessment incomplete","partial folder blocks likely remnant assessment");
        Check(AppRemnants.Assess(data,empty with{StartupCommands=["C:\\Apps\\Code.exe --background"]},["User","logs"],true,false).State=="App present / referenced","service or task reference prevents remnant recommendation");
        Check(AppRemnants.Assess(data with{Name="Microsoft"},empty,[],true,false).State=="Shared / review separately","shared publisher data never treated as one abandoned app");
        Check(AppRemnants.Assess(data with{Name="SampleApp"},empty with{PackageNames=["Vendor.SampleApp"]},[],true,false).State=="Package name match","Store package name evidence remains qualified for uncatalogued apps");
        return passed;
    }
}
