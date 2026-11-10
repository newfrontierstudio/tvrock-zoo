using TsRecommend;
using System.Text.Json;
namespace TsScheduler;
internal sealed partial class MainForm
{
 DateTime cognitionStamp;
 void RefreshCognitionSnapshot(){
  var stamp=File.GetLastWriteTimeUtc(Path.Combine(RockFolder,"tvrock.sch"));if(stamp==cognitionStamp)return;
  try{var before=CognitionHistory.Load(RecommendationHome);string path=Path.Combine(RecommendationHome,"ai-recommendations.json");var known=File.Exists(path)?JsonSerializer.Deserialize<List<Recommendation>>(File.ReadAllText(path),CognitionHistory.Json)??[]:[];var rows=CognitionHistory.Reconcile(RecommendationHome,_nativeBookings,known,CognitionHistory.ReadState(RecommendationHome),DateTimeOffset.Now);cognitionStamp=stamp;if(before.Count>0&&(rows.Any(r=>r.UserCancelled&&!before.Any(b=>b.Id==r.Id&&b.Start==r.Start&&b.UserCancelled))||rows.Any(r=>r.RecommendationKey.Length==0&&!before.Any(b=>b.Id==r.Id&&b.Start==r.Start))))CognitionHistory.Schedule(RecommendationHome,DateTimeOffset.Now.AddMinutes(2));}
  catch(Exception error){Log("推薦履歴の照合を保留: "+error.Message);}
 }
 void AddQuantumSettings(FlowLayoutPanel body){
  var options=QuantumCognitionOptions.Load(RecommendationHome);var enabled=new CheckBox{Text="量子コンテキストで予約の文脈も評価する（試験運用）",Checked=options.Enabled,AutoSize=true};body.Controls.Add(enabled);
  var row=new FlowLayoutPanel{AutoSize=true,WrapContents=false};var weight=new NumericUpDown{Minimum=0,Maximum=100,Value=(decimal)(options.Weight*100),Width=75,AccessibleName="量子認知の反映率"};row.Controls.AddRange([new Label{Text="量子認知の反映率",AutoSize=true,Margin=new Padding(0,8,8,0)},weight,new Label{Text="%（残りは通常モデル）",AutoSize=true,Margin=new Padding(0,8,0,0)}]);body.Controls.Add(row);
  body.Controls.Add(new Label{Text="アルゴリズム："+CognitionHistory.Algorithm+" ／ STUDIO 0.7.4と同じ量子計算核。予約登録はTVRock方式です。",AutoSize=true,MaximumSize=new Size(650,0)});
  bool reverting=false;void Save(){if(reverting)return;var old=QuantumCognitionOptions.Load(RecommendationHome);try{options.Enabled=enabled.Checked;options.Weight=(double)weight.Value/100;options.Save(RecommendationHome);CognitionHistory.Schedule(RecommendationHome,DateTimeOffset.Now.AddMinutes(2));}catch(Exception error){reverting=true;options=old;enabled.Checked=old.Enabled;weight.Value=(decimal)(old.Weight*100);reverting=false;MessageBox.Show(this,error.Message,"量子コンテキスト設定");}}
  enabled.CheckedChanged+=(_,_)=>Save();weight.ValueChanged+=(_,_)=>Save();
 }
}
