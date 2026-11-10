using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
 string RecommendationSummary(){try{return RecommendationOptions.Load(RecommendationHome).Summary;}catch{return "おすすめ設定を確認してください";}}
 void InitializeRecommendationSettings(FlowLayoutPanel body){
  CognitionHistory.EnsureProtocol(RecommendationHome);var options=RecommendationOptions.Load(RecommendationHome);var row=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
  var number=new NumericUpDown{Minimum=1,Maximum=100000,Value=options.Maximum??30,Width=100,AccessibleName="おすすめ予約の上限件数"};
  var unlimited=new CheckBox{Text="上限なし",AutoSize=true,Checked=options.Maximum==null};number.Enabled=!unlimited.Checked;
  row.Controls.AddRange([new Label{Text="直近3日間の有効なおすすめ予約上限",AutoSize=true,Margin=new Padding(0,8,8,0)},number,new Label{Text="件",AutoSize=true,Margin=new Padding(4,8,8,0)},unlimited]);body.Controls.Add(row);
  var repeats=new CheckBox{Text="指定した番組と同じ回の再録画を防ぐ",AutoSize=true,Checked=options.PreventMarkedRepeats};body.Controls.Add(repeats);
  body.Controls.Add(new Label{Text="録画済み一覧の「再録画を防ぐ」で指定します。Zooのおすすめ選定に適用し、TVRockの検索予約・既存予約は変更しません。",AutoSize=true,MaximumSize=new Size(650,0)});
  bool restoring=false;void Save(){if(restoring)return;var previous=RecommendationOptions.Load(RecommendationHome);try{options.Maximum=unlimited.Checked?null:(int)number.Value;options.PreventMarkedRepeats=repeats.Checked;options.Save(RecommendationHome);number.Enabled=!unlimited.Checked;RefreshRecommendationStatus();}catch(Exception e){restoring=true;options=previous;unlimited.Checked=previous.Maximum==null;number.Value=previous.Maximum??30;number.Enabled=!unlimited.Checked;repeats.Checked=previous.PreventMarkedRepeats;restoring=false;MessageBox.Show(this,e.Message,"おすすめ設定を保存できません");}}
  AddQuantumSettings(body);
  number.ValueChanged+=(_,_)=>Save();unlimited.CheckedChanged+=(_,_)=>Save();repeats.CheckedChanged+=(_,_)=>Save();
 }
}
