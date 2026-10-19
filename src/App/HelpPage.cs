namespace TsScheduler;
internal sealed partial class MainForm
{
    internal const string SupportNoteUrl="https://note.com/discreet/n/n3e2ee058b3bd";
    void InitializeHelpPage()
    {
        var page=new TabPage("HELP"){BackColor=Color.White};
        var viewport=new Panel{Dock=DockStyle.Fill};var scroll=new ModernScrollBar{Dock=DockStyle.Right};
        var body=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(22,14,22,14),BackColor=Color.White};
        viewport.Controls.Add(body);page.Controls.Add(viewport);page.Controls.Add(scroll);_tabs.TabPages.Add(page);
        var sections=new List<HelpSection>();HelpSection? current=null;
        var actions=new FlowLayoutPanel{AutoSize=true};var expand=new Button{Text="すべて開く",AutoSize=true};var collapse=new Button{Text="すべて閉じる",AutoSize=true};actions.Controls.AddRange([expand,collapse]);body.Controls.Add(actions);StyleControls(actions);expand.Click+=(_,_)=>{foreach(var item in sections)item.Expanded=true;};collapse.Click+=(_,_)=>{foreach(var item in sections)item.Expanded=false;};
        void TextBlock(string text,bool heading=false){if(heading){if(text=="TVRock Zoo の使い方"){body.Controls.Add(new Label{Text=text,AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,4,0,8)});return;}current=new HelpSection(text,Font);sections.Add(current);body.Controls.Add(current);return;}var label=new SelectableHelpText{Text=text,ForeColor=Ink,Font=Font,Margin=new Padding(4,4,4,10)};(current?.Content.Controls??body.Controls).Add(label);}
        TextBlock(@"TVRock Zoo の使い方",true);
        AddSupportContact(body.Controls);
        TextBlock(@"TVRockの予約・番組表を専用画面で操作する非公式ツールです。録画はTVRockが担当します。");
        TextBlock(@"予約する・取り消す／録画時間を調整する",true);
        TextBlock(@"○で「録画しない」へ、×で有効な予約に戻します。編集は行をダブルクリック。
右クリックの「録画の開始・終了を調整」で秒単位の補正を指定できます。
開始：−で早く／＋で遅く。終了：−で短縮／＋で延長。
「今後も常に」は同じ局・番組名の先頭一致で、既存予約にも上書きします（Zoo起動中）。
「予約一覧」の右端の▼から、分割表示／日時順のまとめ表示を切り替えられます。
○・×の押し間違いは「元に戻す」。上下の境界はドラッグで高さを調整できます。
録画時間中は薄赤で表示します（実際の録画成功を示すものではありません）。");
        TextBlock(@"番組表で探す・局を並べ替える",true);
        TextBlock(@"「番組表を取得」で読み込み、日付を選んで番組を探します。番組をダブルクリック、または選択して「選択番組を予約」で予約できます。
自動検索予約の編集は行をダブルクリック。候補番組は取得済み番組表からの参考表示です。
番組の詳しい内容はマウスオーバーで確認できます。
局名の見出しを左右にドラッグして並べ替えられます。番組表を開くと現在時刻を表示します。
小さい枠はマウスを置くと番組情報を表示し、1回クリックで予約できます。番組表には「予約済み」「予約無効」を表示します。");
        TextBlock(@"おすすめ予約を使う",true);
        TextBlock(@"「自動おすすめ予約」をONにするだけ。検索条件や予約履歴を参考に、Zooが関連する番組を選んで予約します。
ふだんはこれでOK。ChatGPTの準備は要りません。
今すぐ選ぶときは「おすすめ更新」。自動実行は3日ごとに最大20件で、初期状態はOFFです。
OFFにしても予約済みの番組は残ります。番組名の前の白い「自動」はPC内で選んだおすすめです。「GPT」は取り込んだGPTの好み条件を参考にしたおすすめです。どちらも最終的な番組選定はPC内で行います。");
        TextBlock(@"ChatGPTで好みを相談する（任意）",true);
        TextBlock(@"1. 「ChatGPT」タブの「ChatGPTに相談する」を押します。相談文がコピーされ、ChatGPTが開きます。
2. ChatGPTに貼り付けて送信し、好きな番組・避けたい内容を答えます。
3. 最後の回答をコピーして、Zooの「ChatGPTの回答を取り込む」を押します。
4. 「内容を確認」で好きな内容・除外する内容を確認し、適用します。
取り込むとChatGPTタブの下にフィルタリング結果が出ます。番組をダブルクリックして予約できます。
ChatGPTはブラウザーで使えます。アプリのインストールは不要です。
ChatGPTへの送信と回答のコピーは自分で行います。Zooから予約履歴などは送信しません。
取り込んだ条件はZooのおすすめ専用です。TVRockの検索条件・既存予約は変わりません。");
        TextBlock(@"録画サーバー・複数の接続先",true);
        TextBlock(@"詳細設定の「接続先」から、TVRockのURLとフォルダーを指定します。
別PCのTVRockは、読み取り可能な共有フォルダー（\\サーバー名\共有名）も必要です。
URLと共有フォルダーは同じTVRockを指定してください。保存するとZooを再起動して切り替えます。
おすすめ・番組表キャッシュ・録画補正は接続先ごとに管理します。");
        TextBlock(@"表示のカスタマイズ",true);
        TextBlock(@"詳細設定の「予約一覧のゼブラ背景」で縞模様のON／OFFを切り替えられます。局名の背景色も変更できます。局のバッジをクリックして選び、「色を選ぶ」で変更すると並んだバッジに反映します。色は局ごとに保存し、「標準色に戻す」で戻せます。");
        TextBlock(@"表示と更新",true);
        TextBlock(@"「文字 −／＋」で一覧の文字サイズを調整。列幅と表示位置は保存されます。
予約の変更確認と録画時間の表示は1秒ごと。終了した番組は一覧から自動で外れます。手動更新は「一覧を更新」です。");
        TextBlock(@"開発を応援する",true);
        TextBlock(@"気に入っていただけたら、コーヒー代で開発継続を応援していただけると嬉しいです。");
        var support=new Button{Text="☕ 開発を応援する（note）",AutoSize=true,Margin=new Padding(0,6,0,6)};current!.Content.Controls.Add(support);StyleControls(support);
        support.Click+=(_,_)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SupportNoteUrl){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"noteを開けませんでした");}};
        TextBlock(@"noteの紹介記事を開きます。記事末尾の「チップで応援する」から応援できます。");
        string paypalFile=Path.Combine(RecommendationHome,"support-paypal-url.txt");
        string paypal=File.Exists(paypalFile)?File.ReadAllText(paypalFile).Trim():"";
        bool hasPayPal=Uri.TryCreate(paypal,UriKind.Absolute,out var paymentUri)&&paymentUri.Scheme=="https"&&paymentUri.UserInfo.Length==0&&(paymentUri.Host=="paypal.me"||paymentUri.Host=="www.paypal.com"||paymentUri.Host=="paypal.com");
        var secondary=new Button{Text=hasPayPal?"PayPalで応援する":"PayPal（受け取りリンク準備中）",Enabled=hasPayPal,AutoSize=true,Margin=new Padding(0,3,0,6)};current!.Content.Controls.Add(secondary);StyleControls(secondary);
        secondary.Click+=(_,_)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(paypal){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"PayPalを開けませんでした");}};
        bool fitting=false;
        void FitText(){if(fitting)return;fitting=true;try{int width=Math.Max(180,viewport.ClientSize.Width-body.Padding.Horizontal-8);body.Width=viewport.ClientSize.Width;foreach(var section in sections)section.SetWidth(width);foreach(var label in body.Controls.OfType<SelectableHelpText>()){label.Width=width;label.FitHeight();}scroll.LargeChange=Math.Max(1,viewport.Height);scroll.Maximum=Math.Max(0,body.Height-1);scroll.Visible=body.Height>viewport.Height;body.Top=-scroll.Value;}finally{fitting=false;}}
        scroll.ValueChanged+=(_,_)=>body.Top=-scroll.Value;viewport.SizeChanged+=(_,_)=>FitText();body.SizeChanged+=(_,_)=>FitText();viewport.MouseWheel+=(_,e)=>scroll.Value-=e.Delta;foreach(var section in sections)foreach(Control control in section.Content.Controls)control.MouseWheel+=(_,e)=>scroll.Value-=e.Delta;page.Enter+=(_,_)=>FitText();FitText();

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
    internal static System.Drawing.Drawing2D.GraphicsPath TabShape(Rectangle r,float radius){var p=new System.Drawing.Drawing2D.GraphicsPath();float d=radius*2;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddLine(r.Right,r.Top+radius,r.Right,r.Bottom-radius);p.AddBezier(r.Right,r.Bottom-radius,r.Right,r.Bottom,r.Right+radius,r.Bottom,r.Right+radius,r.Bottom);p.AddLine(r.Right+radius,r.Bottom,r.Left-radius,r.Bottom);p.AddBezier(r.Left-radius,r.Bottom,r.Left,r.Bottom,r.Left,r.Bottom,r.Left,r.Bottom-radius);p.CloseFigure();return p;}
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









internal sealed class SelectableHelpText:RichTextBox
{
 bool fitting;
 internal SelectableHelpText(){ReadOnly=true;BorderStyle=BorderStyle.None;ScrollBars=RichTextBoxScrollBars.None;DetectUrls=false;BackColor=Color.White;WordWrap=true;ContentsResized+=(_,e)=>{if(!fitting)Height=e.NewRectangle.Height+12;};}
 internal void FitHeight(){if(fitting)return;fitting=true;try{if(!IsHandleCreated)CreateControl();Height=Math.Max(Font.Height+12,GetPositionFromCharIndex(TextLength).Y+Font.Height+14);}finally{fitting=false;}}
 protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);FitHeight();}
}
internal sealed class HelpSection:FlowLayoutPanel
{
 readonly Button heading;internal readonly FlowLayoutPanel Content;readonly string title;bool expanded;
 internal bool Expanded{get=>expanded;set{expanded=value;Content.Visible=value;heading.Text=(value?"▾  ":"▸  ")+title;PerformLayout();}}
 internal HelpSection(string title,Font font){this.title=title;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;FlowDirection=FlowDirection.TopDown;WrapContents=false;Margin=new Padding(0,2,0,3);heading=new Button{Text="▸  "+title,Height=38,TextAlign=ContentAlignment.MiddleLeft,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(232,234,240),ForeColor=Color.FromArgb(23,42,62),Font=new Font(font,FontStyle.Bold),Margin=Padding.Empty,Cursor=Cursors.Hand};heading.FlatAppearance.BorderSize=0;RoundedControls.Apply(heading);Content=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false,Margin=Padding.Empty};Controls.Add(heading);Controls.Add(Content);heading.Click+=(_,_)=>Expanded=!Expanded;}
 internal void SetWidth(int width){Width=width;heading.Width=width;Content.Width=width;foreach(var text in Content.Controls.OfType<SelectableHelpText>()){text.Width=Math.Max(80,width-12);text.FitHeight();}}
}
