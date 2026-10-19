using System.Net;
using System.Net.Http;
using System.Text.Json;
namespace TsScheduler;
internal static class ProblemReportTests
{
    internal static async Task Run(string root)
    {
        Directory.CreateDirectory(root);var now=DateTimeOffset.Now;
        string Line(DateTimeOffset time,string op)=>JsonSerializer.Serialize(new{Time=time,Event=op,Status="ok",Count=2,Milliseconds=30,Title="private-title",Path="C:/private",URL="https://private.invalid/secret",Reservations=new[]{"private-booking"}});
        File.WriteAllLines(Path.Combine(root,"diagnostic.jsonl"),new[]{Line(now,"tvrock.reg"),Line(now.AddDays(-2),"tvrock.day"),Line(now,"private-title"),"[]","{bad json}","{\"Time\":123,\"Event\":\"tvrock.reg\"}"});
        File.WriteAllText(Path.Combine(root,"tvrock.sch"),"private booking - must never be read");
        var report=ProblemReports.Create(root,"TVRock Zoo","1.0.21","manual description",true,now);if(report.Events.Count!=1||ProblemReports.Json(report).Contains("private"))throw new Exception("The report must reconstruct only allowed diagnostic fields and never include raw files");
        var without=ProblemReports.Create(root,"ZOOstudio Pre","0.13.4","manual description",false,now);if(without.LogIncluded||without.Events.Count!=0)throw new Exception("Unchecking logs must remove all attached events");
        File.WriteAllLines(Path.Combine(root,"errors.jsonl"),Enumerable.Range(0,600).Select(i=>Line(now.AddSeconds(-i),"tvrock.day")));if(ProblemReports.Collect(root,now).Count!=500)throw new Exception("Reports must be bounded to the most recent 500 events");
        var handler=new Fake();using(var client=ProblemReportSender.Client(handler)){if(!await ProblemReportSender.Ready(client))throw new Exception("Ready endpoint");string receipt=await ProblemReportSender.Send(client,without);if(!Guid.TryParse(receipt,out _)||handler.Posts!=1)throw new Exception("One confirmed report send");}
        var failure=new Fake{Fail=true};using(var client=ProblemReportSender.Client(failure)){try{await ProblemReportSender.Send(client,report);throw new Exception("Unconfirmed send must fail");}catch(HttpRequestException){}if(failure.Posts!=1)throw new Exception("Report transmission must never auto-retry");}
    }
    sealed class Fake:HttpMessageHandler
    {
        internal int Posts;internal bool Fail;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
        {
            if(request.RequestUri!.GetLeftPart(UriPartial.Authority)!=ProblemReportSender.FormUrl.TrimEnd('/'))throw new Exception("Unexpected report destination");
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent(request.RequestUri.AbsolutePath.EndsWith("status")?"{\"Ready\":true}":"{\"Token\":\"1234567890.test-token\"}",System.Text.Encoding.UTF8,"application/json")};
            Posts++;string body=await request.Content!.ReadAsStringAsync(cancel);if(body.Contains("private"))throw new Exception("Private diagnostic fields leaked into upload");if(Fail)throw new HttpRequestException("synthetic failure");
            return new(HttpStatusCode.Accepted){Content=new StringContent(JsonSerializer.Serialize(new{Accepted=true,Receipt=Guid.NewGuid().ToString()}),System.Text.Encoding.UTF8,"application/json")};
        }
    }
}
