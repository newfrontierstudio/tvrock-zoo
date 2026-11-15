namespace TsRecommend;

// Quantum cognition v3-chaos (algorithm id: quantum-cognition-v3-chaos).
// Same evidence model as v1 (unknown times = convex mixture, known times =
// sequential Luders weak measurements), plus:
//  A. Free precession about the interest axis between timed observations, so
//     context (allocation-axis) coherence created by one event rotates before
//     the next event is observed: path interference / order effects.
//  B. Decision-time context (busy slot) is a unitary rotation about Y, not a
//     measurement: it reframes the state without collapsing it.
//  C. Dephasing since the series' latest evidence: coherence (X, Y) decays
//     with its own half-life, faster than the population (Z) memory.
//  D. Observation strengths and the evidence half-life are options, calibrated
//     from logged outcomes by QuantumCalibrator.
//  E. Rivalry: a series repeatedly cancelled against a competing recording is
//     suppressed more strongly when the slot is busy again.
internal sealed class QuantumCognition:IQuantumEngine
{
 readonly List<CognitionBooking> history;
 readonly DateTimeOffset now;
 readonly QuantumCognitionOptions options;
 readonly Dictionary<string,(QDensity State,double Evidence,int Ordered,int Legacy,int RivalLosses)> states=new();
 readonly Dictionary<string,CognitionBooking[]> groups;
 // Positive bookings precomputed once: Competing and the busy check scan
 // this list with early exit instead of re-testing every history entry.
 readonly CognitionBooking[] positives;

 internal QuantumCognition(IEnumerable<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions options){this.now=now;this.options=options;
  history=bookings.Select(b=>b with{}).GroupBy(b=>(b.ChannelKey,b.Start.AddSeconds(-b.StartAdjustment))).Select(g=>g.LastOrDefault(Positive)??g.Last()).ToList();
  groups=history.GroupBy(b=>CognitionLearning.Series(b.Title)).ToDictionary(g=>g.Key,g=>g.ToArray());
  positives=history.Where(Positive).ToArray();}

 static bool Negative(CognitionBooking b)=>(b.UserCancelled||b.State=="Cancelled")&&b.CancellationContext!="stop-recording";
 static bool Positive(CognitionBooking b)=>!b.UserCancelled&&b.State is not("Cancelled" or "Disabled" or "Failed")&&(b.UserSelected||b.RecommendationKey.Length==0);
 bool Competing(CognitionBooking b){foreach(var other in positives)if(other.Id!=b.Id&&CognitionHistory.Overlap(b.Start,b.End,other.Start,other.End))return true;return false;}
 DateTimeOffset? Stamp(CognitionBooking b)=>Negative(b)?b.UserCancelledAt:b.UserActionAt;
 static DateTimeOffset EventDate(CognitionBooking b,DateTimeOffset? stamp)=>stamp??(Negative(b)?b.ObservedAt:null)??b.RecommendationCreatedAt??b.Start;

 double Weight(CognitionBooking b){var date=EventDate(b,Stamp(b));double days=Math.Max(0,(now-date).TotalDays);return days>180?0:Math.Pow(.5,days/options.HalfLifeDays);}

 (QDensity State,double Evidence,int Ordered,int Legacy,int RivalLosses) State(string series){
  if(states.TryGetValue(series,out var found))return found;
  var relevant=groups.GetValueOrDefault(series,[]).Where(b=>Positive(b)||Negative(b)).ToArray();
  double x=QDensity.Prior.X*4,y=0,z=QDensity.Prior.Z*4,total=4,evidence=0;
  int legacy=0,ordered=0,rival=0;
  DateTimeOffset? latest=null;
  void Touch(CognitionBooking b){var d=EventDate(b,Stamp(b));if(latest==null||d>latest)latest=d;}

  // Unknown historical action times are a convex mixture, never an invented event sequence.
  foreach(var b in relevant.Where(b=>Stamp(b)==null)){double w=Weight(b);if(w<=0)continue;Touch(b);
   bool context=Negative(b)&&Competing(b);
   if(context){x+=w;evidence+=.35*w;rival++;}
   else{z+=(Positive(b)?1:-1)*w;evidence+=w;}
   total+=w;legacy++; }
  var state=new QDensity(x/total,y/total,z/total);

  DateTimeOffset? previous=null;
  foreach(var b in relevant.Where(b=>Stamp(b)!=null&&Stamp(b)<=now).OrderBy(b=>Stamp(b)).ThenBy(b=>b.Id).TakeLast(64)){
   double w=Weight(b);if(w<=0)continue;Touch(b);
   var stamp=Stamp(b)!.Value;
   if(previous!=null)state=state.Precess(2*Math.PI*(stamp-previous.Value).TotalDays/options.PrecessionDays); // A
   previous=stamp;
   bool context=Negative(b)&&Competing(b);
   state=state.Observe(context?QAxis.Allocation:QAxis.Interest,context||Positive(b)?1:-1,(context?.12:Positive(b)?.12:.20)*options.EtaScale*w);
   evidence+=w*(context?.35:1);ordered++;if(context)rival++; }

  if(latest!=null){double idle=Math.Max(0,(now-latest.Value).TotalDays);state=state.Dephase(Math.Pow(.5,idle/options.DecoherenceHalfLifeDays));} // C
  return states[series]=(state,evidence,ordered,legacy,rival); }

 // Exposed for QuantumCalibrator (leave-one-out scoring of logged events).
 internal QDensity SeriesState(string series)=>State(series).State;
 internal bool IsCompeting(CognitionBooking b)=>Competing(b);
 internal static bool IsPositive(CognitionBooking b)=>Positive(b);
 internal static bool IsNegative(CognitionBooking b)=>Negative(b);
 internal static DateTimeOffset? EventStamp(CognitionBooking b)=>Negative(b)?b.UserCancelledAt:b.UserActionAt;

 internal QuantumEvaluation Evaluate(Show show,double classicalFactor){
  var prior=State(CognitionLearning.Series(show.Title));
  bool busy=false;foreach(var b in positives)if(b.Id!=show.Key&&CognitionHistory.Overlap(b.Start,b.End,show.Start,show.End)){busy=true;break;}
  var state=prior.State;
  var dephased=new QDensity(0,0,state.Z);
  if(busy){state=state.RotateY(options.BusyRotationRadians);dephased=dephased.RotateY(options.BusyRotationRadians);} // B
  double probability=state.Probability(QAxis.Interest),withoutCoherence=dephased.Probability(QAxis.Interest),confidence=prior.Evidence/(prior.Evidence+2);
  if(prior.Evidence==0&&busy)confidence=.2;
  double quantum=Math.Clamp(1+confidence*(probability/.75-1),.45,1.3);
  if(busy&&prior.RivalLosses>0&&quantum<1)quantum=Math.Clamp(1+(quantum-1)*(1+options.RivalryBoost*Math.Min(prior.RivalLosses,3)),.45,1.3); // E
  double factor=options.Enabled?Math.Clamp(options.Weight*quantum+(1-options.Weight)*classicalFactor,.45,1.3):classicalFactor;
  string reason=options.Enabled?$" 量子認知の文脈係数 {factor:0.00}（試験運用）。":"";
  return new(probability,withoutCoherence,quantum,classicalFactor,factor,prior.Ordered,prior.Legacy,busy,reason); }
 QuantumEvaluation IQuantumEngine.Evaluate(Show show,double classicalFactor)=>Evaluate(show,classicalFactor);
}
