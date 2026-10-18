using System.Text.Json;
using System.Text.RegularExpressions;
using TsRecommend;
namespace TsScheduler;
internal sealed record GuideAssignment(string Driver,bool Planned);
internal sealed partial class MainForm
{
    Dictionary<int,string> guideDrivers=[];DateTimeOffset nextDriverRefresh;bool driverRefreshBusy;
    string DriverCache=>Path.Combine(RecommendationHome,"guide-driver-map.json");
    void InitializeGuideAssignments()
    {
        try{if(File.Exists(DriverCache))guideDrivers=JsonSerializer.Deserialize<Dictionary<int,string>>(File.ReadAllText(DriverCache))??[];}catch{}
        _nativeTimer.Tick+=async(_,_)=>{
            if(driverRefreshBusy||DateTimeOffset.Now<nextDriverRefresh||_nativeBookings.Any(b=>b.Fields.ContainsKey("STUDIOSTATE")))return;
            driverRefreshBusy=true;nextDriverRefresh=DateTimeOffset.Now.AddMinutes(5);
            try{var devices=Data.TunerDevices(await NativeGet("reg"));if(devices.Count==0)return;guideDrivers=MapGuideDrivers(Path.Combine(RockFolder,"dtv.ini"),devices);Directory.CreateDirectory(RecommendationHome);File.WriteAllText(DriverCache+".tmp",JsonSerializer.Serialize(guideDrivers));File.Move(DriverCache+".tmp",DriverCache,true);RenderSchedule();}catch{}finally{driverRefreshBusy=false;}
        };
    }
    internal static Dictionary<int,string> MapGuideDrivers(string file,IReadOnlyDictionary<int,int> devices)
    {
        var sections=new Dictionary<int,Dictionary<string,string>>();Dictionary<string,string>? fields=null;
        foreach(string raw in File.ReadLines(file,Data.Cp932)){string line=raw.Trim();if(line.StartsWith('[')){var match=Regex.Match(line,@"^\[TUNER(\d+)\]$",RegexOptions.IgnoreCase);fields=match.Success?sections[int.Parse(match.Groups[1].Value)]=new(StringComparer.OrdinalIgnoreCase):null;continue;}int separator=line.IndexOf('=');if(fields!=null&&separator>0)fields[line[..separator]]=line[(separator+1)..];}
        var result=new Dictionary<int,string>();foreach(var pair in devices){if(!sections.TryGetValue(pair.Key,out var settings))continue;string slot=!string.IsNullOrWhiteSpace(settings.GetValueOrDefault("APPNAME2"))?"2":"1";string arguments=settings.GetValueOrDefault("APPOPT"+slot,"");var match=Regex.Match(arguments,"(?:^|\\s)[/-](?:d|driver)\\s+(?:\"(?<path>[^\"]+)\"|(?<path>\\S+))",RegexOptions.IgnoreCase);string name=match.Success?Path.GetFileName(match.Groups["path"].Value):"";if(!name.StartsWith("BonDriver",StringComparison.OrdinalIgnoreCase)||!name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))continue;if(devices.Values.Count(v=>v==pair.Value)!=1)continue;result[pair.Value]=name;}
        return result;
    }
    GuideAssignment AssignmentFor(Booking booking)
    {
        if(booking.Fields.TryGetValue("BONDRIVER",out string? driver))return new(driver.Length>0?Path.GetFileName(driver):"未割当",booking.Fields.GetValueOrDefault("ASSIGNMENTPLANNED")=="1");
        return new(guideDrivers.GetValueOrDefault(booking.Device,"確認中"),false);
    }
    void UpdateGuideAssignments()
    {
        var assignments=new Dictionary<string,GuideAssignment>();foreach(var show in _nativeShows){var booking=_nativeBookings.FirstOrDefault(b=>b.Enabled&&Planner.SameBroadcast(show,b));if(booking!=null)assignments[show.Key]=AssignmentFor(booking);}_schedule.Assignments=assignments;
    }
    string AssignmentHover(Show show)=>_schedule.Assignments.TryGetValue(show.Key,out var value)?"\n\n割当先："+value.Driver+(value.Planned?"（予定・開始時に確定）":"（設定／実行先）"):"";
}
