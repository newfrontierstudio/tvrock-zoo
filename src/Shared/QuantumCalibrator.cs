namespace TsRecommend;

// D: calibrates the v2 observation strength (EtaScale) and evidence half-life
// (HalfLifeDays) against logged outcomes. Each timed event is scored
// leave-one-out: an engine is built from the history as it stood just before
// the event, the probability of the outcome that actually happened is read
// off the series state, and log-loss is averaged over the sample. The grid
// is deliberately coarse and the result is only applied with >= 8 samples.
// Unknown-time (legacy) events cannot be leave-one-out scored and stay in
// every training state as mixture evidence, as in the engine itself.
internal sealed record QuantumCalibration(double EtaScale,double HalfLifeDays,double LogLoss,int Samples,double PrecessionDays=21,double DecoherenceHalfLifeDays=60);

internal static class QuantumCalibrator
{
 // The sample set both Calibrate and loss evaluation operate on: timed
 // positive/negative outcome events, oldest first, capped at the latest 120.
 internal static CognitionBooking[] ExtractEvents(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now)=>
  bookings.Where(b=>(QuantumCognition.IsPositive(b)||QuantumCognition.IsNegative(b))&&QuantumCognition.EventStamp(b)is {} s&&s<=now)
   .OrderBy(b=>QuantumCognition.EventStamp(b)).ThenBy(b=>b.Id).TakeLast(120).ToArray();

 // Mean leave-one-out log-loss of one fixed option set over the sample.
 // (Parallelising this loop was tried and measured SLOWER on a
 // CPU-throttled machine: each event is only ~1 ms of work, so scheduling
 // overhead dominates. The engine's precomputed positives list is the
 // optimisation that measured faster; this loop stays sequential.)
 internal static double MeanLogLoss(IReadOnlyList<CognitionBooking> bookings,IReadOnlyList<CognitionBooking> events,QuantumCognitionOptions options)
 {
  double loss=0;
  foreach(var e in events){
   var stamp=QuantumCognition.EventStamp(e)!.Value;
   var engine=new QuantumCognition(bookings.Where(b=>b.Id!=e.Id&&(QuantumCognition.EventStamp(b) is not {} action||action<stamp)),stamp,options);
   var state=engine.SeriesState(CognitionLearning.Series(e.Title));
   bool context=QuantumCognition.IsNegative(e)&&engine.IsCompeting(e);
   double p=state.Probability(context?QAxis.Allocation:QAxis.Interest);
   double observed=context||QuantumCognition.IsPositive(e)?p:1-p;
   loss+=-Math.Log(Math.Clamp(observed,.02,.98)); }
  return loss/events.Count; }

 internal static QuantumCalibration? Calibrate(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions baseline)
 {
  var events=ExtractEvents(bookings,now);
  if(events.Length<8)return null;
  QuantumCalibration? best=null;
  foreach(double eta in new[]{.7,1,1.4})foreach(double half in new[]{30d,45d,70d}){
   var options=new QuantumCognitionOptions{Enabled=true,Weight=baseline.Weight,EtaScale=eta,HalfLifeDays=half,
    PrecessionDays=baseline.PrecessionDays,DecoherenceHalfLifeDays=baseline.DecoherenceHalfLifeDays,
    BusyRotationRadians=baseline.BusyRotationRadians,RivalryBoost=baseline.RivalryBoost};
   double mean=MeanLogLoss(bookings,events,options);
   if(best==null||mean<best.LogLoss)best=new(eta,half,mean,events.Length); }
  return best; }

 // Bounded deterministic chaotic search used by the v3 adaptation layer.
 // A logistic map explores the option space; adoption remains guarded by the
 // caller's minimum-improvement, sample-count, and regression checks.
 internal static QuantumCalibration? CalibrateChaos(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions baseline)
 {
  var events=ExtractEvents(bookings,now);if(events.Length<8)return null;
  double seed=(events.Length*0.013+events.Sum(e=>e.Start.UtcTicks%997)*0.000001)%0.9+0.05;
  double x=seed;QuantumCalibration? best=null;
  for(int i=0;i<24;i++){
   x=3.99*x*(1-x);double eta=.55+1.2*x;
   x=3.99*x*(1-x);double half=14+106*x;
   x=3.99*x*(1-x);double precession=5+85*x;
   x=3.99*x*(1-x);double decoherence=14+166*x;
   var options=new QuantumCognitionOptions{Enabled=true,Weight=baseline.Weight,EtaScale=eta,HalfLifeDays=half,
    PrecessionDays=precession,DecoherenceHalfLifeDays=decoherence,BusyRotationRadians=baseline.BusyRotationRadians,RivalryBoost=baseline.RivalryBoost};
   double loss=MeanLogLoss(bookings,events,options);
   if(best==null||loss<best.LogLoss)best=new(eta,half,loss,events.Length,precession,decoherence);
  }
  return best;
 }
}
