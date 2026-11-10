using System.Text.Json;
using TsRecommend;

namespace TsRecommend;
internal readonly record struct QAxis(double X,double Y,double Z){internal static QAxis Interest=>new(0,0,1);internal static QAxis Allocation=>new(1,0,0);}
// rho=(I+r.sigma)/2. |r|<=1 guarantees trace one, Hermiticity and positivity.
// Born probability Tr(rho P_n)=(1+r.n)/2. Weak update is the square-root (Luders) instrument
// M_s=sqrt((1+s*eta)/2)P_n+sqrt((1-s*eta)/2)(I-P_n).
internal readonly record struct QDensity(double X,double Y,double Z)
{
 internal double Length=>Math.Sqrt(X*X+Y*Y+Z*Z);
 internal double Probability(QAxis n)=>Math.Clamp((1+X*n.X+Y*n.Y+Z*n.Z)/2,0,1);
 internal QDensity Observe(QAxis n,int outcome,double strength){
  double eta=Math.Clamp(strength,0,.95),v=X*n.X+Y*n.Y+Z*n.Z,den=1+outcome*eta*v,k=Math.Sqrt(1-eta*eta);
  return new((k*(X-v*n.X)+(v+outcome*eta)*n.X)/den,(k*(Y-v*n.Y)+(v+outcome*eta)*n.Y)/den,(k*(Z-v*n.Z)+(v+outcome*eta)*n.Z)/den);
 }
 internal static QDensity Prior=>new(Math.Sqrt(.75),0,.5); // pure prior P(interest)=.75
}
internal sealed class QuantumCognitionOptions
{
 public bool Enabled{get;set;}=true;public double Weight{get;set;}=.70;
 internal static QuantumCognitionOptions Load(string home){string path=Path.Combine(home,"quantum-options.json");var options=File.Exists(path)?JsonSerializer.Deserialize<QuantumCognitionOptions>(File.ReadAllText(path))??new():new();if(!double.IsFinite(options.Weight)||options.Weight<0||options.Weight>1)throw new InvalidDataException("量子認知の反映率は0〜100%で指定してください。");return options;}
 internal void Save(string home){if(!double.IsFinite(Weight)||Weight<0||Weight>1)throw new InvalidDataException("量子認知の反映率を確認してください。");ZooStorage.DurableJson.Write(Path.Combine(home,"quantum-options.json"),JsonSerializer.Serialize(this));}
}
internal sealed record QuantumEvaluation(double Probability,double DephasedProbability,double QuantumFactor,double ClassicalFactor,double Factor,int OrderedEvents,int LegacyEvents,bool Busy,string Reason);
internal sealed class QuantumCognition
{
 readonly List<CognitionBooking> history;readonly DateTimeOffset now;readonly QuantumCognitionOptions options;
 readonly Dictionary<string,(QDensity State,double Evidence,int Ordered,int Legacy)> states=new();readonly Dictionary<string,CognitionBooking[]> groups;
 internal QuantumCognition(IEnumerable<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions options){this.now=now;this.options=options;history=bookings.Select(b=>b with{}).GroupBy(b=>(b.ChannelKey,b.Start.AddSeconds(-b.StartAdjustment))).Select(g=>g.LastOrDefault(Positive)??g.Last()).ToList();groups=history.GroupBy(b=>CognitionLearning.Series(b.Title)).ToDictionary(g=>g.Key,g=>g.ToArray());}
 static bool Negative(CognitionBooking b)=>(b.UserCancelled||b.State=="Cancelled")&&b.CancellationContext!="stop-recording";
 static bool Positive(CognitionBooking b)=>!b.UserCancelled&&b.State is not("Cancelled" or "Disabled" or "Failed")&&(b.UserSelected||b.RecommendationKey.Length==0);
 bool Competing(CognitionBooking b)=>history.Any(other=>other.Id!=b.Id&&Positive(other)&&CognitionHistory.Overlap(b.Start,b.End,other.Start,other.End));
 DateTimeOffset? Stamp(CognitionBooking b)=>Negative(b)?b.UserCancelledAt:b.UserActionAt;
 double Weight(CognitionBooking b){var date=Stamp(b)??(Negative(b)?b.ObservedAt:null)??b.RecommendationCreatedAt??b.Start;double days=Math.Max(0,(now-date).TotalDays);return days>180?0:Math.Pow(.5,days/45);}
 (QDensity State,double Evidence,int Ordered,int Legacy) State(string series){
  if(states.TryGetValue(series,out var found))return found;
  var relevant=groups.GetValueOrDefault(series,[]).Where(b=>Positive(b)||Negative(b)).ToArray();
  double x=QDensity.Prior.X*4,y=0,z=QDensity.Prior.Z*4,total=4,evidence=0;int legacy=0,ordered=0;
  // Unknown historical action times are a convex mixture, never an invented event sequence.
  foreach(var b in relevant.Where(b=>Stamp(b)==null)){double w=Weight(b);if(w<=0)continue;bool context=Negative(b)&&Competing(b);if(context){x+=w;evidence+=.35*w;}else{z+=(Positive(b)?1:-1)*w;evidence+=w;}total+=w;legacy++;}
  var state=new QDensity(x/total,y/total,z/total);
  foreach(var b in relevant.Where(b=>Stamp(b)!=null&&Stamp(b)<=now).OrderBy(b=>Stamp(b)).ThenBy(b=>b.Id).TakeLast(64)){
   double w=Weight(b);if(w<=0)continue;bool context=Negative(b)&&Competing(b);state=state.Observe(context?QAxis.Allocation:QAxis.Interest,context||Positive(b)?1:-1,(context?.12:Positive(b)?.12:.20)*w);evidence+=w*(context?.35:1);ordered++;
  }
  return states[series]=(state,evidence,ordered,legacy);
 }
 internal QuantumEvaluation Evaluate(Show show,double classicalFactor){
  var prior=State(CognitionLearning.Series(show.Title));bool busy=history.Any(b=>Positive(b)&&b.Id!=show.Key&&CognitionHistory.Overlap(b.Start,b.End,show.Start,show.End));
  var state=prior.State;var dephased=new QDensity(0,0,state.Z);
  if(busy){state=state.Observe(QAxis.Allocation,1,.18);dephased=dephased.Observe(QAxis.Allocation,1,.18);}
  double probability=state.Probability(QAxis.Interest),withoutCoherence=dephased.Probability(QAxis.Interest),confidence=prior.Evidence/(prior.Evidence+2);if(prior.Evidence==0&&busy)confidence=.2;
  double quantum=Math.Clamp(1+confidence*(probability/.75-1),.45,1.3),factor=options.Enabled?Math.Clamp(options.Weight*quantum+(1-options.Weight)*classicalFactor,.45,1.3):classicalFactor;
  string reason=options.Enabled?$" 量子認知の文脈係数 {factor:0.00}（試験運用）。":"";
  return new(probability,withoutCoherence,quantum,classicalFactor,factor,prior.Ordered,prior.Legacy,busy,reason);
 }
}
internal sealed record QuantumRankAudit(string Key,string Title,DateTimeOffset Start,double ContentScore,double ClassicalScore,double ProductionScore,QuantumEvaluation Evaluation){public bool Selected{get;set;}}
