using System.Text.Json;

namespace TsRecommend;

// Internal self-updating parameter store. No user-facing surface: it is fed
// snapshots of the history the host already keeps, holds the option set the
// host passes to the engine, and revises that set on its own schedule when
// accumulated outcomes justify it. It never wakes itself; the host calls
// Observe. Every failure mode degrades to "no change".
internal sealed class ChaosComponent
{
 internal const int MinNewSamples=24;         // outcome events newer than the watermark before a revision is considered
 internal const double MinHoursBetween=24;    // minimum spacing between adoptions
 internal const double MinImprovement=.002;   // calibrated loss must beat the current options' loss by at least this
 internal const double RegressionTolerance=.01; // post-adoption recheck slack before a revision is rejected

 internal sealed class StateData
 {
  public QuantumCognitionOptions Current{get;set;}=new();
  public QuantumCognitionOptions? Previous{get;set;}
  public int UpdateCount{get;set;}
  public int RejectedCount{get;set;}
  public DateTimeOffset? LastAdoptedAt{get;set;}
  public DateTimeOffset? Watermark{get;set;}
 }

 readonly Func<string> pathProvider;readonly object gate=new();
 StateData state=new();bool loaded,fromFile;

 internal ChaosComponent(Func<string> pathProvider)=>this.pathProvider=pathProvider??throw new ArgumentNullException(nameof(pathProvider));

 internal bool HasState{get{lock(gate){EnsureLoaded();return fromFile;}}}
 internal int UpdateCount{get{lock(gate){EnsureLoaded();return state.UpdateCount;}}}
 internal int RejectedCount{get{lock(gate){EnsureLoaded();return state.RejectedCount;}}}
 internal DateTimeOffset? LastAdoptedAt{get{lock(gate){EnsureLoaded();return state.LastAdoptedAt;}}}
 internal QuantumCognitionOptions CurrentOptions{get{lock(gate){EnsureLoaded();return Clone(state.Current);}}}

 internal void Observe(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now)
 {
  ArgumentNullException.ThrowIfNull(bookings);
  lock(gate){try{
   EnsureLoaded();
   var events=QuantumCalibrator.ExtractEvents(bookings,now);
   if(events.Length<8)return;
   int fresh=state.Watermark is {} mark?events.Count(e=>QuantumCognition.EventStamp(e)>mark):events.Length;
   if(fresh<MinNewSamples)return;
   if(state.LastAdoptedAt is {} last&&(now-last).TotalHours<MinHoursBetween)return;
   var freshEvents=events.Where(e=>state.Watermark==null||QuantumCognition.EventStamp(e)>state.Watermark).ToArray();
   var current=state.Current;
   if(state.Previous!=null&&freshEvents.Length>=MinNewSamples){
    double currentLoss=QuantumCalibrator.MeanLogLoss(bookings,freshEvents,current),previousLoss=QuantumCalibrator.MeanLogLoss(bookings,freshEvents,state.Previous);
    if(currentLoss>previousLoss+RegressionTolerance){state.Current=state.Previous;state.Previous=null;state.RejectedCount++;state.LastAdoptedAt=now;state.Watermark=events.Max(e=>QuantumCognition.EventStamp(e));Persist();return;}
   }
   if(events.Length<48)return;
   var holdout=events.TakeLast(24).ToArray();var cutoff=QuantumCognition.EventStamp(holdout[0])!.Value;
   var training=bookings.Where(b=>QuantumCognition.EventStamp(b) is not {} stamp||stamp<cutoff).ToArray();
   if(QuantumCalibrator.ExtractEvents(training,cutoff.AddTicks(-1)).Length<24)return;
   var calibration=QuantumWorker.TryCalibrateChaos(training,cutoff.AddTicks(-1),current)??QuantumCalibrator.CalibrateChaos(training,cutoff.AddTicks(-1),current);
   if(calibration==null)return;
   QuantumWorker.CompareCalibrateChaos(training,cutoff.AddTicks(-1),current,calibration);
   var adopted=Clone(current);adopted.EtaScale=calibration.EtaScale;adopted.HalfLifeDays=calibration.HalfLifeDays;adopted.PrecessionDays=calibration.PrecessionDays;adopted.DecoherenceHalfLifeDays=calibration.DecoherenceHalfLifeDays;
   double baselineLoss=QuantumCalibrator.MeanLogLoss(bookings,holdout,current),candidateLoss=QuantumCalibrator.MeanLogLoss(bookings,holdout,adopted);
   if(!double.IsFinite(candidateLoss)||candidateLoss>baselineLoss-MinImprovement)return;
   state.Previous=Clone(current);state.Current=adopted;state.UpdateCount++;state.LastAdoptedAt=now;
   state.Watermark=events.Max(e=>QuantumCognition.EventStamp(e));
   Persist();
  }catch{}}
 }

 internal void RestorePrevious()
 {
  lock(gate){try{
   EnsureLoaded();if(state.Previous==null)return;
   state.Current=state.Previous;state.Previous=null;Persist();
  }catch{}}
 }

 void EnsureLoaded()
 {
  if(loaded)return;loaded=true;
  try{string path=pathProvider();if(!File.Exists(path))return;
   var data=JsonSerializer.Deserialize<StateData>(ZooStorage.DurableJson.Read(path));
   if(data?.Current==null)return;
   data.Current=Sanitize(data.Current);if(data.Previous!=null)data.Previous=Sanitize(data.Previous);
   state=data;fromFile=true;
  }catch{state=new();}
 }

 void Persist(){try{ZooStorage.DurableJson.Write(pathProvider(),JsonSerializer.Serialize(state));fromFile=true;}catch{}}

 static QuantumCognitionOptions Clone(QuantumCognitionOptions o)=>new(){Enabled=o.Enabled,Weight=o.Weight,EtaScale=o.EtaScale,HalfLifeDays=o.HalfLifeDays,PrecessionDays=o.PrecessionDays,DecoherenceHalfLifeDays=o.DecoherenceHalfLifeDays,BusyRotationRadians=o.BusyRotationRadians,RivalryBoost=o.RivalryBoost};

 // Same ranges as QuantumCognitionOptions.Load, but never throws: a damaged
 // value falls back to its default instead of breaking the host.
 static QuantumCognitionOptions Sanitize(QuantumCognitionOptions o)
 {
  o.Weight=double.IsFinite(o.Weight)?Math.Clamp(o.Weight,0,1):.7;
  o.EtaScale=double.IsFinite(o.EtaScale)?Math.Clamp(o.EtaScale,.5,2):1;
  o.HalfLifeDays=double.IsFinite(o.HalfLifeDays)?Math.Clamp(o.HalfLifeDays,7,180):45;
  o.PrecessionDays=double.IsFinite(o.PrecessionDays)?Math.Clamp(o.PrecessionDays,3,180):21;
  o.DecoherenceHalfLifeDays=double.IsFinite(o.DecoherenceHalfLifeDays)?Math.Clamp(o.DecoherenceHalfLifeDays,7,365):60;
  o.BusyRotationRadians=double.IsFinite(o.BusyRotationRadians)?Math.Clamp(o.BusyRotationRadians,0,1):.30;
  o.RivalryBoost=double.IsFinite(o.RivalryBoost)?Math.Clamp(o.RivalryBoost,0,.5):.15;
  return o; }
}
