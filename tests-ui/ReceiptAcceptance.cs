using System.Net.Http;
using System.Text.Json;
using TsScheduler;
internal static class ReceiptAcceptance
{
    internal static async Task<int> Run()
    {
        using var client=ProblemReportSender.Client();
        using var status=JsonDocument.Parse(await client.GetStringAsync("api/status"));
        if(!status.RootElement.GetProperty("Ready").GetBoolean()||status.RootElement.GetProperty("Service").GetString()!="PrivateStorage"||status.RootElement.GetProperty("EmailNotifications").GetBoolean())throw new Exception("Acceptance check requires private storage with email notifications OFF");
        // Synthetic data only; do not collect actual files, settings, logs or reservations.
        var report=new ProblemReport("ZOOstudio Pre","0.13.7",DateTimeOffset.UtcNow,"非公開受付の保存テスト（合成データ）",false,[]);
        string requestId=Guid.NewGuid().ToString();
        string first=await ProblemReportSender.Send(client,report,requestId);
        string second=await ProblemReportSender.Send(client,report,requestId);
        if(first!=second)throw new Exception("Repeated report created more than one receipt");
        using var stored=JsonDocument.Parse(await client.GetStringAsync("api/receipts/"+first));
        if(!stored.RootElement.GetProperty("Accepted").GetBoolean())throw new Exception("Receipt is not stored");
        File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"work","private-receipt-acceptance.json"),JsonSerializer.Serialize(new{Receipt=first,RequestId=requestId,Accepted=true,Stored=true,DuplicatePrevented=true,EmailSent=false}));
        Console.WriteLine("PASS: real app sender, private persistence, one receipt for two explicit identical requests, email OFF");return 0;
    }
}
