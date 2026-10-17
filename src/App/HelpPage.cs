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
右クリックの「録画の開始・終了を調整」で秒単位の補正を指定できます。
開始：−で早く／＋で遅く。終了：−で短縮／＋で延長。
「今後も常に」は同じ局・番組名の先頭一致で、既存予約にも上書きします（Zoo起動中）。
「予約一覧」の右端の▼から、分割表示／日時順のまとめ表示を切り替えられます。
○・×の押し間違いは「元に戻す」。上下の境界はドラッグで高さを調整できます。
録画時間中は薄赤で表示します（実際の録画成功を示すものではありません）。");
        TextBlock(@"番組表・検索／自動検索予約",true);
        TextBlock(@"「番組表を取得」で読み込み、日付を選んで番組を探します。番組をダブルクリック、または選択して「選択番組を予約」で予約できます。
自動検索予約の編集は行をダブルクリック。候補番組は取得済み番組表からの参考表示です。
番組の詳しい内容はマウスオーバーで確認できます。");
        TextBlock(@"① まずはZooにおまかせ（ChatGPT不要）",true);
        TextBlock(@"「自動おすすめ予約」をONにするだけ。検索条件や予約履歴を参考に、Zooが関連する番組を選んで予約します。
ふだんはこれでOK。ChatGPTの準備は要りません。
今すぐ選ぶときは「おすすめ更新」。自動実行は3日ごとに最大20件で、初期状態はOFFです。
OFFにしても予約済みの番組は残ります。太字・斜体がおすすめ予約です。");
        TextBlock(@"② もっと好みに合わせたいならChatGPT（任意）",true);
        TextBlock(@"好きな番組や避けたい内容をChatGPTに伝え、検索条件を考えてもらいます。
例：「街歩き番組が好き。通販は避けたい。TVRock用の検索ワードと除外条件を考えて」
提案された条件を、Zooの「自動検索予約」→対象行をダブルクリックして入力します。
設定した条件が、番組探しやおすすめの参考になります。
相談しただけではZooに反映されません。ChatGPTとは自動連携していないため、条件は自分で入力してください。");
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
        if(control is Button||control is CheckBox {Appearance:Appearance.Button})RoundedControls.Apply(control);
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
    internal FlatTabControl(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??Color.White);
        for(int i=0;i<TabCount;i++){
            var bounds=GetTabRect(i);if(!bounds.IntersectsWith(e.ClipRectangle))continue;
            OnDrawItem(new DrawItemEventArgs(e.Graphics,Font,bounds,i,i==SelectedIndex?DrawItemState.Selected:DrawItemState.Default));
        }
    }
    protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}

    public override Rectangle DisplayRectangle {
        get{var area=base.DisplayRectangle;int top=Math.Max(0,area.Top-3);return new Rectangle(0,top,ClientSize.Width,Math.Max(0,ClientSize.Height-top));}
    }
}
