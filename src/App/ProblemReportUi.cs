using System.Net.Http;
using System.Net.Http.Json;
namespace TsScheduler;
internal static class ProblemReportSender
{
    internal const string FormUrl="https://zoo-support-report.stonyray64.chatgpt.site/";
    internal static HttpClient Client(HttpMessageHandler? handler=null)=>new(handler??new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(FormUrl),Timeout=TimeSpan.FromSeconds(40)};
    internal static async Task<bool> Ready(HttpClient client){try{using var response=await client.GetAsync("api/status");if(!response.IsSuccessStatusCode)return false;var status=await response.Content.ReadFromJsonAsync<Status>();return status?.Ready==true;}catch{return false;}}
    sealed record Status(bool Ready);
    sealed record Session(string Token);
    sealed record Receipt(bool Accepted,string ReceiptId);
    internal static async Task<string> Send(HttpClient client,ProblemReport report)
    {
        using var session=await client.GetAsync("api/session");session.EnsureSuccessStatusCode();var token=await session.Content.ReadFromJsonAsync<Session>();if(token==null||token.Token.Length is <10 or >1024||token.Token.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not('.' or '-')))throw new IOException("送信確認を準備できませんでした。");
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/report"){Content=JsonContent.Create(report)};request.Headers.Authorization=new("Bearer",token.Token);
        using var response=await client.SendAsync(request);if(response.StatusCode!=System.Net.HttpStatusCode.Accepted)throw new IOException("受付を確認できませんでした。送信結果が不明なため、自動再送はしません。");
        using var result=System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());var root=result.RootElement;
        if(!root.TryGetProperty("Accepted",out var accepted)||accepted.ValueKind!=System.Text.Json.JsonValueKind.True||!root.TryGetProperty("Receipt",out var receipt)||receipt.ValueKind!=System.Text.Json.JsonValueKind.String||!Guid.TryParse(receipt.GetString(),out _))throw new IOException("受付番号を確認できませんでした。自動再送はしません。");
        return receipt.GetString()!;
    }
}
internal sealed partial class MainForm
{
    void InitializeProblemReporting(Control.ControlCollection controls)
    {
        var report=new Button{Text="不具合を報告",AutoSize=true,Margin=new Padding(0,8,0,5)};report.Click+=(_,_)=>ShowProblemReport();controls.Add(report);StyleControls(report);
        controls.Add(new Label{Text="直近の診断ログを自動でまとめます。送信前に確認・解除できます。登録不要・非公開です。",AutoSize=true,MaximumSize=new Size(620,0),Margin=new Padding(0,0,0,10)});
    }
    void ShowProblemReport()
    {
        using var client=ProblemReportSender.Client();using var dialog=new Form{Text="不具合を報告",Font=new Font("Meiryo UI",10),ClientSize=new Size(710,530),MinimumSize=new Size(650,510),StartPosition=FormStartPosition.CenterParent};
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=7};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<7;i++)body.RowStyles.Add(new RowStyle(i==2?SizeType.Percent:SizeType.AutoSize,i==2?100:0));dialog.Controls.Add(body);
        string product="TVRock Zoo",version=Application.ProductVersion.Split('+')[0];var now=DateTimeOffset.Now;var snapshot=ProblemReports.Create(RecommendationHome,product,version,"",true,now);
        body.Controls.Add(new Label{Text=product+" "+version+" — 行った操作と、起きたことを教えてください",AutoSize=true},0,0);
        body.Controls.Add(new Label{Text="送信前にログを確認・解除できます。自動ログには番組名・予約一覧・個人のパスを含めません。\n報告は一般公開されません。送信にはFormSubmitを利用します。",AutoSize=true,MaximumSize=new Size(650,0),Margin=new Padding(0,10,0,12)},0,1);
        var description=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,MaxLength=10000};body.Controls.Add(description,0,2);
        var row=new FlowLayoutPanel{AutoSize=true,Margin=new Padding(0,12,0,4)};var include=new CheckBox{Text=$"診断ログの送信 ON（{snapshot.Events.Count}件）",Checked=true,AutoSize=true,Margin=new Padding(0,8,12,0)};var inspect=new Button{Text="ログを確認",AutoSize=true};row.Controls.AddRange([include,inspect]);body.Controls.Add(row,0,3);
        include.CheckedChanged+=(_,_)=>include.Text=include.Checked?$"診断ログの送信 ON（{snapshot.Events.Count}件）":"診断ログの送信 OFF（本文だけ送信）";
        inspect.Click+=(_,_)=>{using var preview=new Form{Text="送信する診断ログ",ClientSize=new Size(700,450),StartPosition=FormStartPosition.CenterParent};preview.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,WordWrap=false,Text=System.Text.Json.JsonSerializer.Serialize(include.Checked?snapshot.Events:[],new System.Text.Json.JsonSerializerOptions{WriteIndented=true})});preview.ShowDialog(dialog);};
        var state=new Label{Text="受信先を確認しています。",AutoSize=true,MaximumSize=new Size(650,0),Margin=new Padding(0,8,0,8)};body.Controls.Add(state,0,4);
        var buttons=new FlowLayoutPanel{AutoSize=true};var send=new Button{Text="送信",Enabled=false,AutoSize=true,BackColor=Color.FromArgb(255,228,228)};var close=new Button{Text="閉じる",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.AddRange([send,close]);body.Controls.Add(buttons,0,5);dialog.CancelButton=close;
        ProblemReport Build()=>snapshot with{Description=description.Text,LogIncluded=include.Checked,Events=include.Checked?snapshot.Events:[]};
        bool busy=false,ready=false,acceptedReport=false,attempted=false;void State()=>send.Enabled=ready&&!busy&&!acceptedReport&&!attempted&&!string.IsNullOrWhiteSpace(description.Text);description.TextChanged+=(_,_)=>State();
        dialog.Shown+=async(_,_)=>{ready=await ProblemReportSender.Ready(client);if(dialog.IsDisposed)return;state.Text=ready?"「送信」を押すまで、内容は外部に送信されません。":"受信先を確認中のため、まだ送信できません。しばらく待ってからもう一度開いてください。";State();};
        send.Click+=async(_,_)=>{if(busy||!ready||attempted)return;attempted=true;busy=true;send.Enabled=false;close.Enabled=false;description.ReadOnly=true;include.Enabled=false;state.Text="送信しています。";try{string id=await ProblemReportSender.Send(client,Build());acceptedReport=true;state.Text="報告を受け付けました。ありがとうございます。\n受付番号："+id;send.Text="受付済み";}catch{state.Text="送信結果を確認できません。届いている場合があるため、再送せず時間をおいて確認してください。";send.Text="送信結果を確認してください";}finally{busy=false;close.Enabled=true;description.ReadOnly=false;include.Enabled=true;State();}};
        dialog.FormClosing+=(_,e)=>{if(busy)e.Cancel=true;};dialog.ShowDialog(this);
    }
}
