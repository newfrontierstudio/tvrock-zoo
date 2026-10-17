namespace TsScheduler;
internal sealed partial class MainForm
{
    internal const string SupportNoteUrl="https://note.com/discreet/n/n3e2ee058b3bd";
    void InitializeHelpPage()
    {
        var page=new TabPage("HELP"){BackColor=Color.White};
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(22,14,22,14),BackColor=Color.White};
        page.Controls.Add(body);_tabs.TabPages.Add(page);
        Label TextBlock(string text,bool heading=false){var label=new Label{Text=text,AutoSize=true,Margin=new Padding(0,heading?18:5,0,6),ForeColor=Ink};if(heading){var font=new Font(Font.FontFamily,12,FontStyle.Bold);_readingFonts.Add(font);label.Font=font;}body.Controls.Add(label);return label;}
        TextBlock(@"TVRock Zoo の使い方",true);
        TextBlock(@"TVRockの予約・番組表を専用画面で操作する非公式ツールです。録画はTVRockが担当します。");
        TextBlock(@"予約一覧",true);
        TextBlock(@"○で「録画しない」へ、×で有効な予約に戻します。編集は行をダブルクリック。
「予約一覧」の右端の▼をクリックすると、分割表示／日時順のまとめ表示を切り替えられます。
押し間違いは「元に戻す」。上下の境界はドラッグで高さを調整できます。
録画時間中は薄赤で表示します（実際の録画成功を示すものではありません）。");
        TextBlock(@"番組表・検索／自動検索予約",true);
        TextBlock(@"「番組表を取得」で読み込み、日付を選んで番組を探します。番組をダブルクリック、または選択して「選択番組を予約」で予約できます。
自動検索予約の編集は行をダブルクリック。候補番組は取得済み番組表からの参考表示です。
番組の詳しい内容はマウスオーバーで確認できます。");
        TextBlock(@"おすすめ予約",true);
        TextBlock(@"検索条件と予約履歴の言葉の類似度から、PC内で番組を選びます。
上部のON／OFFで自動実行を切り替えます。3日ごとに最大20件。OFFでも既存予約は残ります。
太字・斜体がおすすめ予約です。ChatGPTがなくても利用できます。");
        TextBlock(@"ChatGPTで好みを相談するには",true);
        TextBlock(@"好きな番組や避けたい内容をChatGPTに伝え、検索ワード・除外条件を相談します。
提案された条件をZooの「自動検索予約」で設定してください。現在は手動での反映です。");
        TextBlock(@"表示と更新",true);
        TextBlock(@"「文字 −／＋」で一覧の文字サイズを調整。列幅と表示位置は保存されます。
予約の変更確認と録画時間の表示は1秒ごと。終了した番組は一覧から自動で外れます。手動更新は「一覧を更新」です。");
        TextBlock(@"開発を応援する",true);
        TextBlock(@"気に入っていただけたら、コーヒー代で開発継続を応援していただけると嬉しいです。");
        var support=new Button{Text="☕ 開発を応援する（note）",AutoSize=true,Margin=new Padding(0,6,0,6)};body.Controls.Add(support);StyleControls(support);
        support.Click+=(_,_)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SupportNoteUrl){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"noteを開けませんでした");}};
        TextBlock(@"noteの紹介記事を開きます。記事末尾の「チップで応援する」から応援できます。");
        string paypalFile=Path.Combine(RecommendationHome,"support-paypal-url.txt");
        string paypal=File.Exists(paypalFile)?File.ReadAllText(paypalFile).Trim():"";
        bool hasPayPal=Uri.TryCreate(paypal,UriKind.Absolute,out var paymentUri)&&paymentUri.Scheme=="https"&&paymentUri.UserInfo.Length==0&&(paymentUri.Host=="paypal.me"||paymentUri.Host=="www.paypal.com"||paymentUri.Host=="paypal.com");
        var secondary=new Button{Text=hasPayPal?"PayPalで応援する":"PayPal（受け取りリンク準備中）",Enabled=hasPayPal,AutoSize=true,Margin=new Padding(0,3,0,6)};body.Controls.Add(secondary);StyleControls(secondary);
        secondary.Click+=(_,_)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(paypal){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"PayPalを開けませんでした");}};
        void FitText(){int width=Math.Max(180,body.ClientSize.Width-body.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8);foreach(var label in body.Controls.OfType<Label>())label.MaximumSize=new Size(width,0);}
        body.SizeChanged+=(_,_)=>FitText();FitText();
    }
    static void ApplyFlatSurfaces(Control control)
    {
        if(control is MainForm){control.Padding=Padding.Empty;control.BackColor=Color.White;}
        if(control is TabPage page)page.Padding=Padding.Empty;
        if(control is Panel panel)panel.BorderStyle=BorderStyle.None;
        if(control is UserControl user)user.BorderStyle=BorderStyle.None;
        if(control is SplitContainer split){split.BorderStyle=BorderStyle.None;split.Panel1.BorderStyle=BorderStyle.None;split.Panel2.BorderStyle=BorderStyle.None;}
        if(control is DataGridView grid)grid.BorderStyle=BorderStyle.None;
        if(control is TextBox text)text.BorderStyle=BorderStyle.FixedSingle;
        if(control is CheckBox check){check.FlatStyle=FlatStyle.Flat;check.FlatAppearance.BorderSize=0;}
        if(control is ComboBox combo)combo.FlatStyle=FlatStyle.Flat;
        foreach(Control child in control.Controls)ApplyFlatSurfaces(child);
    }
}
internal sealed class FlatTabControl:TabControl
{
    public override Rectangle DisplayRectangle {
        get{var area=base.DisplayRectangle;int top=Math.Max(0,area.Top-3);return new Rectangle(0,top,ClientSize.Width,Math.Max(0,ClientSize.Height-top));}
    }
}
