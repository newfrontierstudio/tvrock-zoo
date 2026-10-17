using System.Diagnostics;
using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    const string ConsultPrompt="TVRock Zooで録画する番組の好みを設定したいです。最初に好きな番組・ジャンルと避けたい内容を私に質問してください。回答を聞いたら番組名や説明に出てくる短い単語を選んでください。好きな内容はinclude、避けたい内容はexcludeの配列にしてください。除外語は番組名と説明の部分一致で全おすすめ候補から除かれるため、広すぎる単語を避けてください。最終回答はJSONだけ。最大各30項目、各100文字以内。形式：{\"include\":[\"街歩き\",\"旅行\"],\"exclude\":[\"通販\",\"ショッピング\"]}。コードや正規表現は使わないでください。";
    void InitializeConsult(FlowLayoutPanel bar)
    {
        var ask=new Button{Text="好みを相談",AutoSize=true};var import=new Button{Text="回答を取り込む",AutoSize=true};
        bar.Controls.AddRange([ask,import]);StyleControls(ask);StyleControls(import);
        ask.Click+=(_,_)=>{try{Clipboard.SetText(ConsultPrompt);Process.Start(new ProcessStartInfo("https://chatgpt.com/"){UseShellExecute=true});MessageBox.Show(this,"① ChatGPTに貼り付けて送信します。\n② 好きな番組・避けたい内容を答えます。\n③ 最後の回答をコピーし、Zooの「回答を取り込む」を押してください。\n\nZooから予約履歴などは送信しません。","相談文をコピーしました");}catch(Exception e){MessageBox.Show(this,e.Message,"ChatGPTを開けませんでした");}};
        import.Click+=(_,_)=>ShowConsultImport();
        var clear=new Button{Text="相談した好みを解除",AutoSize=true};bar.Controls.Add(clear);StyleControls(clear);
        clear.Click+=(_,_)=>{if(MessageBox.Show(this,"取り込んだ好みを解除し、通常のおすすめ選びに戻しますか？\n予約済みの番組は残ります。","好みの解除",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;try{File.Delete(Path.Combine(RecommendationHome,"consult-preferences.json"));MessageBox.Show(this,"通常のおすすめ選びに戻しました。");}catch(Exception e){MessageBox.Show(this,e.Message,"解除できませんでした");}};
    }
    void ShowConsultImport()
    {
        using var dialog=new Form{Text="ChatGPTの回答を取り込む",Size=new Size(650,470),StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};
        var input=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};try{if(Clipboard.ContainsText())input.Text=Clipboard.GetText();}catch{}
        var caption=new Label{Text="回答を貼り付けて「内容を確認」。適用しても今すぐ予約は追加しません。",Dock=DockStyle.Top,Height=52,Padding=new Padding(8)};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};var check=new Button{Text="内容を確認",AutoSize=true};var cancel=new Button{Text="閉じる",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.AddRange([cancel,check]);dialog.Controls.Add(input);dialog.Controls.Add(caption);dialog.Controls.Add(buttons);StyleControls(check);StyleControls(cancel);
        check.Click+=(_,_)=>{try{var prefs=ConsultPreferences.Parse(input.Text);string preview="好きな内容（おすすめを増やす）\n"+string.Join("、",prefs.Include)+"\n\n避けたい内容（おすすめから除外）\n"+(prefs.Exclude.Length==0?"なし":string.Join("、",prefs.Exclude))+"\n\n前回取り込んだ好みを置き換えます。既存予約・TVRockの検索条件は変更しません。\nこの内容を適用しますか？";if(MessageBox.Show(dialog,preview,"好みの確認",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;prefs.Save(RecommendationHome);MessageBox.Show(dialog,"好みを保存しました。次のおすすめ選びから使います。\n今すぐ選ぶには「おすすめ更新」を押してください。","適用しました");dialog.Close();}catch(Exception error){MessageBox.Show(dialog,"取り込めませんでした。\n"+error.Message,"回答を確認してください");}};
        dialog.ShowDialog(this);
    }
}
