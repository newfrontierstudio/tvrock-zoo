using System.Text.Json;
namespace TsScheduler;

internal sealed record ReportEvent(DateTimeOffset Time,string Event,string Status,int Count,long Milliseconds);
internal sealed record ProblemReport(string Product,string Version,DateTimeOffset Created,string Description,bool LogIncluded,List<ReportEvent> Events);
internal static class ProblemReports
{
    internal static readonly HashSet<string> AllowedEvents=["logging.changed","scheduler.saved","scheduler.save","recording.start","recording.finished","import.disabled",..new[]{"list","day","now","kwl","kws","reg","regsch","cnt","other"}.Select(route=>"tvrock."+route)];
    static readonly HashSet<string> AllowedStatus=["","on","off","ok","Starting","Preparing","Recording","Checking","Stopping","Completed","Failed","Cancelled","IOException","UnauthorizedAccessException","HttpRequestException","TaskCanceledException","OperationCanceledException","InvalidOperationException","InvalidDataException","JsonException","ArgumentException","TimeoutException"];
    internal static List<ReportEvent> Collect(string home,DateTimeOffset now)
    {
        var result=new List<ReportEvent>();
        foreach(string name in new[]{"diagnostic.jsonl.previous","errors.jsonl.previous","diagnostic.jsonl","errors.jsonl"})
        {
            string path=Path.Combine(home,name);
            try
            {
                using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(file.Length>2*1024*1024)continue;
                using var reader=new StreamReader(file);string? line;while((line=reader.ReadLine())!=null)
                {
                    if(line.Length>8192)continue;
                    try
                    {
                        using var document=JsonDocument.Parse(line);var item=document.RootElement;if(item.ValueKind!=JsonValueKind.Object)continue;
                        if(!item.TryGetProperty("Time",out var date)||date.ValueKind!=JsonValueKind.String||!date.TryGetDateTimeOffset(out var time)||time<now.AddHours(-24)||time>now.AddMinutes(5))continue;
                        string? operation=item.TryGetProperty("Event",out var op)&&op.ValueKind==JsonValueKind.String?op.GetString():null;if(operation==null||!AllowedEvents.Contains(operation))continue;
                        string status=item.TryGetProperty("Status",out var code)&&code.ValueKind==JsonValueKind.String?code.GetString()??"":"";
                        if(!AllowedStatus.Contains(status))status="Error";
                        int count=item.TryGetProperty("Count",out var c)&&c.ValueKind==JsonValueKind.Number&&c.TryGetInt32(out int n)?Math.Clamp(n,0,100000000):0;
                        long elapsed=item.TryGetProperty("Milliseconds",out var ms)&&ms.ValueKind==JsonValueKind.Number&&ms.TryGetInt64(out long value)?Math.Clamp(value,0,3600000):0;
                        result.Add(new(time,operation,status,count,elapsed));
                    }catch(JsonException){}
                }
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
        return result.Distinct().OrderBy(e=>e.Time).TakeLast(500).ToList();
    }
    internal static ProblemReport Create(string home,string product,string version,string description,bool includeLogs,DateTimeOffset now)
    {
        if(product is not("TVRock Zoo" or "ZOOstudio Pre"))throw new ArgumentException("Unknown product");
        if(!System.Version.TryParse(version,out _))throw new ArgumentException("Invalid version");
        if(description.Length>10000)throw new ArgumentException("報告内容は1万文字以内にしてください。");
        return new(product,version,now,description,includeLogs,includeLogs?Collect(home,now):[]);
    }
    internal static string Json(ProblemReport report)=>JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});
}
