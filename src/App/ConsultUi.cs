using System.Diagnostics;
using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly DataGridView _consultResults=NativeGrid();
    readonly Label _consultSummary=new(){AutoSize=true,Margin=new Padding(0,12,0,8)};
    void RefreshConsultResults()
    {
        if(_consultResults.Columns.Count==0)return;
        try{
            var prefs=ConsultPreferences.Load(RecommendationHome);_consultResults.Rows.Clear();
            if(prefs.Include.Length==0){_consultSummary.Text="相談した好みを取り込むと、ここに条件に合った番組が出ます。";return;}
            foreach(var show in _nativeShows.Where(show=>show.End>DateTimeOffset.Now&&prefs.Allows(show)&&prefs.Include.Any(word=>(show.Title+" "+show.Description).Contains(word,StringComparison.OrdinalIgnoreCase))).OrderBy(show=>show.Start)){
                int row=_consultResults.Rows.Add(show.Start.ToString("MM/dd HH:mm"),show.Station.Replace("MX1","MX"),show.Title);_consultResults.Rows[row].Tag=show;_consultResults.Rows[row].Cells["title"].ToolTipText=show.Description;
            }
            _consultSummary.Text=$"フィルタリング結果：{_consultResults.Rows.Count}件　番組をダブルクリックすると予約できます。\n含む：{string.Join("、",prefs.Include)}　除外：{string.Join("、",prefs.Exclude)}"+(_nativeShows.Count==0?"\n番組表がありません。「番組表・検索」で番組表を取得してください。":"");
        }catch(Exception error){_consultSummary.Text="条件を読み込めませんでした："+error.Message;}
    }
    internal const string ConsultPrompt="""TVRock Zooというテレビ録画アプリの「おすすめ予約」を設定します。最初に、好きな番組・ジャンルと避けたい内容を私に質問してください。私が答えてから、番組名や説明に実際に出てくる短い単語を選び、Zooへ取り込む回答を作ってください。好きな内容はinclude、避けたい内容はexcludeの配列です。excludeは番組名と説明の部分一致で全おすすめ候補から除くため、広すぎる語を避けてください。最終回答はJSONのコードブロック1つだけにし、「この回答をコピーしてZooの『ChatGPTの回答を取り込む』を押してください」と案内してください。形式は {"include":["街歩き","旅行"],"exclude":["通販","ショッピング"]}。各配列は最大30項目、1項目100文字以内。コードや正規表現は使わないでください。""";
    void InitializeConsult(FlowLayoutPanel bar)
    {
        var page=new TabPage("ChatGPT"){BackColor=Color.White};
        _tabs.TabPages.Insert(3,page);
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(22,18,22,18),BackColor=Color.White};
        page.Controls.Add(body);
        body.Dock=DockStyle.Top;body.AutoSize=true;
        ModernScrollBar.Attach(_consultResults);
        Col(_consultResults,"start","開始日時",170);Col(_consultResults,"station","局",85);Col(_consultResults,"title","条件に合った番組",400,true);
        page.Controls.Add(_consultResults);_consultResults.BringToFront();
        _consultResults.CellDoubleClick+=async(_,e)=>{if(e.RowIndex>=0&&_consultResults.Rows[e.RowIndex].Tag is Show show){await NativeBookShow(show);RefreshConsultResults();}};
        page.Enter+=(_,_)=>RefreshConsultResults();
        var title=new Label{Text="ChatGPTで好みに合う番組を探す",AutoSize=true,Font=new Font(Font.FontFamily,12,FontStyle.Bold),Margin=new Padding(0,0,0,14)};_readingFonts.Add(title.Font);body.Controls.Add(title);
        var instructions=new Label{AutoSize=true,Text="1. 下の①を押します。相談文がコピーされ、ChatGPTが開きます。\n2. ChatGPTの入力欄に貼り付けて、送信します（Ctrl＋Vで貼り付け）。\n3. ChatGPTの質問に、好きな番組・避けたい内容を答えます。\n4. 最後に出る回答の「コピーする」を押します。\n5. Zooのこの画面に戻り、②を押して内容を確認・適用します。",Margin=new Padding(0,0,0,16)};
        body.Controls.Add(instructions);
        bar=new FlowLayoutPanel{AutoSize=true,WrapContents=true,Margin=Padding.Empty};body.Controls.Add(bar);
        var note=new Label{AutoSize=true,Text="回答はブラウザーのChatGPT画面に出ます。Zooへはコピーして取り込みます。\n適用後は、次のおすすめ選びから反映。今すぐ選ぶには上の「おすすめ更新」。\nChatGPTを使わなくても、通常のおすすめ予約は利用できます。",Margin=new Padding(0,16,0,0)};body.Controls.Add(note);
        void Fit(){int width=Math.Max(180,body.ClientSize.Width-60);instructions.MaximumSize=new Size(width,0);note.MaximumSize=new Size(width,0);_consultSummary.MaximumSize=new Size(width,0);bar.MaximumSize=new Size(width,0);}body.SizeChanged+=(_,_)=>Fit();Fit();
        var ask=new Button{Text="ChatGPTに相談する",AutoSize=true};var import=new Button{Text="ChatGPTの回答を取り込む",AutoSize=true};
        bar.Controls.AddRange([ask,import]);StyleControls(ask);StyleControls(import);
        ask.Text="ChatGPTに相談する";import.Text="ChatGPTの回答を取り込む";import.Visible=false;
        ask.BackColor=Accent;ask.ForeColor=Color.White;import.BackColor=Accent;import.ForeColor=Color.White;
        instructions.Text="下のボタンから始めてください。";
        note.Text="ChatGPTを使わなくても、通常のおすすめ予約は利用できます。";
        body.Controls.Add(_consultSummary);
        ask.Click+=(_,_)=>{ask.Visible=false;import.Visible=true;instructions.Text="ChatGPTの入力欄に貼り付けて送信します（Ctrl＋V）。\n質問に答え、最後の回答をコピーして、この画面に戻ってください。";note.Text="回答をコピーしたら、上の「ChatGPTの回答を取り込む」を押してください。";};
        ask.Click+=(_,_)=>{try{Clipboard.SetText(ConsultPrompt);Process.Start(new ProcessStartInfo("https://chatgpt.com/"){UseShellExecute=true});MessageBox.Show(this,"① ChatGPTに貼り付けて送信します。\n② 好きな番組・避けたい内容を答えます。\n③ 最後の回答をコピーし、Zooの「回答を取り込む」を押してください。\n\nZooから予約履歴などは送信しません。","相談文をコピーしました");}catch(Exception e){MessageBox.Show(this,e.Message,"ChatGPTを開けませんでした");}};
        import.Click+=(_,_)=>ShowConsultImport();
        var clear=new Button{Text="相談した好みを解除",AutoSize=true,Visible=false};bar.Controls.Add(clear);StyleControls(clear);
        var options=new ContextMenuStrip();options.Items.Add("最初から相談する",null,(_,_)=>{ask.Visible=true;import.Visible=false;instructions.Text="下のボタンから始めてください。";});options.Items.Add("取り込んだ好みを解除",null,(_,_)=>{clear.Visible=true;clear.PerformClick();clear.Visible=false;RefreshConsultResults();});import.ContextMenuStrip=options;ask.ContextMenuStrip=options;Disposed+=(_,_)=>options.Dispose();
        clear.Click+=(_,_)=>{if(MessageBox.Show(this,"取り込んだ好みを解除し、通常のおすすめ選びに戻しますか？\n予約済みの番組は残ります。","好みの解除",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;try{File.Delete(Path.Combine(RecommendationHome,"consult-preferences.json"));MessageBox.Show(this,"通常のおすすめ選びに戻しました。");}catch(Exception e){MessageBox.Show(this,e.Message,"解除できませんでした");}};
    }
    void ShowConsultImport(string? supplied=null)
    {
        using var dialog=new Form{Text="ChatGPTの回答を取り込む",Size=new Size(650,470),StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};
        var input=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};if(supplied!=null)input.Text=supplied;else try{if(Clipboard.ContainsText())input.Text=Clipboard.GetText();}catch{}
        var caption=new Label{Text="回答を貼り付けて「内容を確認」。適用しても今すぐ予約は追加しません。",Dock=DockStyle.Top,Height=52,Padding=new Padding(8)};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};var check=new Button{Text="内容を確認",AutoSize=true};var cancel=new Button{Text="閉じる",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.AddRange([cancel,check]);dialog.Controls.Add(input);dialog.Controls.Add(caption);dialog.Controls.Add(buttons);StyleControls(check);StyleControls(cancel);
        check.Click+=(_,_)=>{try{var prefs=ConsultPreferences.Parse(input.Text);string preview="好きな内容（おすすめを増やす）\n"+string.Join("、",prefs.Include)+"\n\n避けたい内容（おすすめから除外）\n"+(prefs.Exclude.Length==0?"なし":string.Join("、",prefs.Exclude))+"\n\n前回取り込んだ好みを置き換えます。既存予約・TVRockの検索条件は変更しません。\nこの内容を適用しますか？";if(MessageBox.Show(dialog,preview,"好みの確認",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;prefs.Save(RecommendationHome);RefreshConsultResults();MessageBox.Show(dialog,"条件に合う番組を、この画面の下に表示しました。\n番組をダブルクリックすると予約できます。","適用しました");dialog.Close();}catch(Exception error){MessageBox.Show(dialog,"取り込めませんでした。\n"+error.Message,"回答を確認してください");}};
        dialog.ShowDialog(this);
    }
}
