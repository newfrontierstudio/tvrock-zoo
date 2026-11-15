namespace TsRecommend;
internal static class UserRulePriorityTests
{
 internal static int Run(){
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
  var fields=new Dictionary<string,string>{{"VALIDATE","1"},{"FUZZY","1"},{"TWORD","モヤモヤさまぁ～ず"},{"STATION0","テレ東"},{"WEEK","64"}};
  var rule=new UserRulePriority.Rule(1,fields);var start=new DateTimeOffset(2026,11,21,11,30,0,TimeSpan.FromHours(9));
  var show=new Show("1",1,1,"テレビ東京","モヤモヤさまぁ〜ず２【北区王子】","",start,start.AddMinutes(45),[]);
  Check(rule.Matches(show),"fullwidth/wave dash explicit rule owns broadcast");
  Check(!rule.Matches(show with{Station="NHK"}),"station restriction retained");
  Check(!rule.Matches(show with{Start=start.AddDays(1)}),"weekday restriction retained");
  Check(!rule.Matches(show with{Title="別番組"}),"unrelated show remains available to Satlite");
  Check(UserRulePriority.Query("abc","^abc$",new Dictionary<string,string>{{"FUZZY","0"}}),"explicit regex unchanged");
  var booking=new Booking("x",show.Title,show.Station,1,1,start,show.End,5,true,true,false,new(){{"SEARCHID","-7"}});
  Check(UserRulePriority.HasSearchOrigin(booking),"TVRock search provenance outranks stale Satlite metadata");
  Check(!UserRulePriority.HasSearchOrigin(booking with{Fields=new(){{"SEARCHID","0"}}}),"direct reservation is not inferred to be a search reservation");
  var tuner=new Tuner(5,[show.Station]);var available=show with{Actions=new(){{5,"day?r=1&c=1&e=1&d=5"}}};
  var later=available with{Key="2",Title="別番組「特集の記録」",Start=start.AddHours(2),End=start.AddHours(3)};
  var plan=Planner.Make([available,later],[],[],[tuner],start.AddHours(-2),1,s=>(s.Key=="2"?1:.3,"条件",""),s=>s.Key=="1"?0:1,proposeRemovals:false);
  Check(plan.Add.Count==1&&plan.Add[0].Show.Key=="1","rule priority controls selection before quota and score");
  var existing=Enumerable.Range(0,1000).Select(i=>booking with{Id=i.ToString(),Start=start.AddDays(2).AddMinutes(i),End=start.AddDays(2).AddMinutes(i+20)}).ToList();
  plan=Planner.Make([available],existing,[new(show.Title,1)],[tuner],start.AddHours(-2),5,proposeRemovals:false);
  Check(plan.Remove.Count==0&&existing.Count==1000,"1000 existing reservations retained without removal planning");
  string rejectionHome=Path.Combine(Path.GetTempPath(),"zoo130-rejections-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(rejectionHome);
  var rejected=new CognitionBooking{Id="rejected",Title="旅の記録 第1回",Start=start,End=start.AddHours(1),RecommendationKey="pick",UserCancelled=true,UserCancelledAt=start.AddHours(-1),State="Cancelled"};
  var rejectionStore=new SatliteRejections(rejectionHome);rejectionStore.Sync([rejected],start,persist:false);
  Check(rejectionStore.Suppresses("旅の記録 第2回")&&!File.Exists(Path.Combine(rejectionHome,"satlite-rejections.json")),"explicit series rejection and read-only preview");
  rejectionStore.Sync([rejected with{Id="rejected2",UserCancelledAt=start.AddMinutes(-30)}],start);
  var manual=rejected with{Id="manual",UserCancelled=false,RecommendationKey="",State="Queued",UserSelected=true,UserActionAt=start};
  rejectionStore.Sync([manual],start);Check(!rejectionStore.Suppresses("旅の記録 第2回"),"later manual booking releases Satlite rejection");
  foreach(string file in Directory.GetFiles(rejectionHome))File.Delete(file);Directory.Delete(rejectionHome);
  if(QuantumWorker.CompareMode){
   QuantumCompareStats.Reset();var history=Enumerable.Range(0,12).Select(i=>new CognitionBooking{Id=i.ToString(),Title="旅の記録 第"+i+"回",ChannelKey="NHK",Start=start.AddDays(-i-1),End=start.AddDays(-i-1).AddHours(1),UserSelected=i%3!=0,UserCancelled=i%3==0,State=i%3==0?"Cancelled":"Completed",UserActionAt=start.AddDays(-i-2),UserCancelledAt=i%3==0?start.AddDays(-i-1):null}).ToArray();
   IQuantumEngine engine=new QuantumCognition(history,start,new());var rust=QuantumWorker.TryEngineForced(history,start,new())??throw new Exception("Rust worker missing");var compare=new ComparingEngine(engine,rust);
   for(int i=0;i<120;i++)Check(double.IsFinite(compare.Evaluate(show with{Title="旅の記録 第99回",Start=start.AddHours(i),End=start.AddHours(i+1)},.9).Factor),"Rust compare "+i);
   string json=System.Text.Json.JsonSerializer.Serialize(QuantumCompareStats.Snapshot());using var stats=System.Text.Json.JsonDocument.Parse(json);
   Check(stats.RootElement.GetProperty("failed").GetInt32()==0&&stats.RootElement.GetProperty("maxFactorDiff").GetDouble()<1e-12&&stats.RootElement.GetProperty("maxProbabilityDiff").GetDouble()<1e-12,"C# / Rust v3 agreement including timed history");Console.WriteLine(json);
  }
  return 0;
 }
}
