using TsRecommend;
namespace TsScheduler;
internal static class SetupRecoveryTests
{
    internal static void Run(string root)
    {
        string? previous=ConnectionProfile.TestRoot;ConnectionProfile.TestRoot=root;Directory.CreateDirectory(root);
        try
        {
            var first=Path.Combine(root,"recorderA");var second=Path.Combine(root,"recorderB");foreach(var folder in new[]{first,second}){Directory.CreateDirectory(folder);foreach(var file in new[]{"dtv.ini","tvrock.sch","tvrock.key"})File.WriteAllText(Path.Combine(folder,file),"");}
            var profiles=new ConnectionProfiles{Profiles=[new(){Id="serverA",Name="保存済み接続",Folder=first,Url="http://localhost:8969/test/"},new(){Id="serverB",Name="別の接続",Folder=second,Url="http://localhost:8970/test/"}],ActiveId="serverA"};
            SetupDialog.UpdateProfile(profiles,first,"http://localhost:8969/test/list");if(profiles.ActiveId!="serverA"||profiles.Active.Name!="保存済み接続"||profiles.Profiles.Count!=2)throw new Exception("Reconfiguration must preserve profile identity and other connections");profiles.Save();
            string before=File.ReadAllText(ConnectionProfiles.FilePath);using(var dialog=new SetupDialog()){dialog.DialogResult=DialogResult.Cancel;}if(File.ReadAllText(ConnectionProfiles.FilePath)!=before)throw new Exception("Cancelling setup must not change settings");
            SetupDialog.UpdateProfile(profiles,second,"http://localhost:8971/new/");if(profiles.ActiveId=="serverA"||profiles.Profiles.Count!=3||profiles.Profiles.Single(p=>p.Id=="serverA").Folder!=first)throw new Exception("A changed destination needs isolated settings without destroying the previous profile");
            File.WriteAllText(ConnectionProfiles.FilePath,"invalid json");if(SetupDialog.ReadForSetup().Profiles.Count!=1||File.ReadAllText(ConnectionProfiles.FilePath)!="invalid json")throw new Exception("Broken configuration must reopen setup without silently overwriting it");
            var mail=MainForm.SupportMailUri("TVRock Zoo","1.2.3");if(!mail.StartsWith("mailto:newfrontierstudio@gmail.com?")||!Uri.UnescapeDataString(mail).Contains("1.2.3")||mail.Contains("attachment",StringComparison.OrdinalIgnoreCase))throw new Exception("Support action must prepare an addressed draft without auto-attaching data");
            var now=new DateTimeOffset(2030,1,1,10,0,0,TimeSpan.Zero);var show=new Show("test",1,1,"テスト局","テスト番組","",now,now.AddMinutes(30),new(){{0,"a"},{7,"b"}});var tuners=new List<Tuner>{new(0,["テスト局"]),new(7,["テスト局"])};
            if(!MainForm.ReservationDeviceAvailable(show,tuners,[],7)||MainForm.ReservationDeviceAvailable(show,tuners,[new("busy","別番組","テスト局",1,1,now,now.AddHours(1),7,true,true,false,[])],7))throw new Exception("A selected free tuner must be accepted even when it is not the first device; occupied selected tuners must be rejected");
        }
        finally{ConnectionProfile.TestRoot=previous;}
    }
}
