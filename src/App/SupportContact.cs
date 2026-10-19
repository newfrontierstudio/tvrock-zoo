namespace TsScheduler;
internal sealed partial class MainForm
{
    internal const string SupportEmail="newfrontierstudio@gmail.com";
    internal static string SupportMailUri(string product,string version)=>"mailto:"+SupportEmail+"?subject="+Uri.EscapeDataString(product+" 不具合報告 "+version)+"&body="+Uri.EscapeDataString($"製品：{product}\r\nバージョン：{version}\r\n\r\n行った操作：\r\n\r\n起きたこと：\r\n\r\n期待した動作：\r\n\r\n必要に応じて、内容を確認したログを手動で添付してください。");
    void AddSupportContact(Control.ControlCollection controls)
    {
        var row=new FlowLayoutPanel{AutoSize=true,WrapContents=true,Margin=new Padding(0,8,0,8)};
        var report=new Button{Text="不具合を報告",AutoSize=true};var email=new LinkLabel{Text=SupportEmail,AutoSize=true,Margin=new Padding(8,9,0,0)};
        void Open(){try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SupportMailUri("TVRock Zoo",Application.ProductVersion.Split('+')[0])){UseShellExecute=true});}catch{MessageBox.Show(this,"メールアプリを開けませんでした。宛先："+SupportEmail+"\n製品名・バージョン・操作と結果を記載してお送りください。","不具合の連絡先");}}
        report.Click+=(_,_)=>Open();email.LinkClicked+=(_,_)=>Open();row.Controls.AddRange([report,email]);controls.Add(row);StyleControls(report);
        controls.Add(new Label{Text="メール作成画面を開きます。ログは内容を確認して手動で添付してください。",AutoSize=true,MaximumSize=new Size(620,0),Margin=new Padding(0,0,0,10)});
    }
}
