using System.Net;using System.Text;using TsScheduler;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
void Assert(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
var read=new Fake(2);string value=await TvRockHttp.Get(new Uri("http://localhost/user/reg?i=1"),read);Assert(read.Calls==3&&value=="成功","Read retries recover from two connection resets");
foreach(var path in new[]{"regsch?i=1&valid=true","day?r=1&c=1&e=2&d=5","kwl?i=1&val=0"}){var mutation=new Fake(99);try{await TvRockHttp.Get(new Uri("http://localhost/user/"+path),mutation);throw new Exception("Unexpected success");}catch(HttpRequestException){Assert(mutation.Calls==1,"Mutation is sent once: "+path.Split('?')[0]);}}
var parallel=new Fake(0);await Task.WhenAll(Enumerable.Range(0,5).Select(_=>TvRockHttp.Get(new Uri("http://localhost/user/list"),parallel)));Assert(parallel.MaxActive==1,"Requests are serialized to avoid overloading TVRock");
Console.WriteLine("PASS: all network tests; no live reservation writes");

sealed class Fake(int failures):HttpMessageHandler{public int Calls,Active,MaxActive;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Calls++;Active++;MaxActive=Math.Max(Active,MaxActive);try{await Task.Delay(10,cancellationToken);if(Calls<=failures)throw new HttpRequestException("Simulated connection reset");return new(HttpStatusCode.OK){Content=new ByteArrayContent(Encoding.GetEncoding(932).GetBytes("成功"))};}finally{Active--;}}}
