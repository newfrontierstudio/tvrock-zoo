using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TsRecommend;

// The evaluation surface shared by the in-process C# engine and the Rust
// worker, so callers can take either without caring which produced a number.
internal interface IQuantumEngine
{
 QuantumEvaluation Evaluate(Show show,double classicalFactor);
}

// Opt-in bridge to the Rust quantum worker (quantum-core-rs, algorithm
// quantum-cognition-v2 core). Enabled only when NFS_QUANTUM_ENGINE=rust and the
// worker executable is found (NFS_QUANTUM_WORKER_PATH, else next to the
// app). Every failure returns null / throws inside the session only, and
// callers fall back to the C# engine: the C# implementation stays the
// reference and the default.
internal static class QuantumWorker
{
 static string Mode{get{string? mode=Environment.GetEnvironmentVariable("NFS_QUANTUM_ENGINE");if(mode!=null)return mode;try{string path=Path.Combine(AppContext.BaseDirectory,"quantum-engine.txt");return File.Exists(path)?File.ReadAllText(path).Trim():"csharp";}catch{return "csharp";}}}
 internal static bool Requested=>Mode=="rust";
 // Compare mode: the C# engine stays authoritative for every decision,
 // but the Rust worker answers the same inputs and the divergences are
 // recorded in QuantumCompareStats for the run's audit file. This is the
 // dual-run stage before the default can move to Rust.
 internal static bool CompareMode=>Mode=="compare";
 static readonly object Gate=new();
 static QuantumWorkerSession? shared;

 internal static string? Locate()
 {
  string? env=Environment.GetEnvironmentVariable("NFS_QUANTUM_WORKER_PATH");
  if(!string.IsNullOrWhiteSpace(env)&&File.Exists(env))return env;
  string exe=Path.Combine(AppContext.BaseDirectory,OperatingSystem.IsWindows()?"quantum-core-rs.exe":"quantum-core-rs");
  return File.Exists(exe)?exe:null;
 }

 internal static IQuantumEngine? TryEngine(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions options)
 {
  if(!Requested)return null;
  return TryEngineForced(bookings,now,options);
 }

 internal static IQuantumEngine? TryEngineForced(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions options)
 {
  lock(Gate)
  {
   try
   {
    shared??=QuantumWorkerSession.Start();
    if(shared==null)return null;
    shared.Init(bookings,now,options);
    return shared;
   }
   catch(Exception)
   {
    try{shared?.Dispose();}catch(Exception){}
    shared=null;
    return null;
   }
  }
 }

 internal static QuantumCalibration? TryCalibrate(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions baseline)
 {
  if(!Requested)return null;
  try
  {
   using var session=QuantumWorkerSession.Start();
   if(session==null)return null;
   var response=session.Request(new{type="calibrate",now,baseline=OptionsFileJson(baseline),history=bookings.Select(BookingJson).ToArray()});
   var node=response.GetProperty("calibration");
   if(node.ValueKind==JsonValueKind.Null)return null;
   return new QuantumCalibration(node.GetProperty("EtaScale").GetDouble(),node.GetProperty("HalfLifeDays").GetDouble(),node.GetProperty("LogLoss").GetDouble(),node.GetProperty("Samples").GetInt32());
  }
  catch(Exception){return null;}
 }

 internal static QuantumCalibration? TryCalibrateChaos(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions baseline)
 {
  if(!Requested)return null;
  try
  {
   using var session=QuantumWorkerSession.Start();
   if(session==null)return null;
   var response=session.Request(new{type="calibrateChaos",now,baseline=OptionsFileJson(baseline),history=bookings.Select(BookingJson).ToArray()});
   var node=response.GetProperty("calibration");
   if(node.ValueKind==JsonValueKind.Null)return null;
   return new QuantumCalibration(node.GetProperty("EtaScale").GetDouble(),node.GetProperty("HalfLifeDays").GetDouble(),node.GetProperty("LogLoss").GetDouble(),node.GetProperty("Samples").GetInt32(),node.GetProperty("PrecessionDays").GetDouble(),node.GetProperty("DecoherenceHalfLifeDays").GetDouble());
  }
  catch(Exception){return null;}
 }

 static QuantumCalibration? WorkerCalibration(string type,IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions baseline)
 {
  using var session=QuantumWorkerSession.Start();
  if(session==null)return null;
  var response=session.Request(new{type,now,baseline=OptionsFileJson(baseline),history=bookings.Select(BookingJson).ToArray()});
  var node=response.GetProperty("calibration");
  if(node.ValueKind==JsonValueKind.Null)return null;
  return new QuantumCalibration(node.GetProperty("EtaScale").GetDouble(),node.GetProperty("HalfLifeDays").GetDouble(),node.GetProperty("LogLoss").GetDouble(),node.GetProperty("Samples").GetInt32(),node.TryGetProperty("PrecessionDays",out var p)?p.GetDouble():21,node.TryGetProperty("DecoherenceHalfLifeDays",out var d)?d.GetDouble():60);
 }

 internal static void CompareCalibrate(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions baseline,QuantumCalibration? csharp)
 {
  if(!CompareMode)return;
  try{QuantumCompareStats.RecordCalibration("grid",csharp,WorkerCalibration("calibrate",bookings,now,baseline));}
  catch(Exception){QuantumCompareStats.RecordFailure();}
 }

 internal static void CompareCalibrateChaos(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions baseline,QuantumCalibration? csharp)
 {
  if(!CompareMode)return;
  try{QuantumCompareStats.RecordCalibration("chaos",csharp,WorkerCalibration("calibrateChaos",bookings,now,baseline));}
  catch(Exception){QuantumCompareStats.RecordFailure();}
 }

 internal static object BookingJson(CognitionBooking b)=>new{id=b.Id,title=b.Title,station=b.Station,channelKey=b.ChannelKey,start=b.Start,end=b.End,startAdjustment=b.StartAdjustment,state=b.State,userSelected=b.UserSelected,userCancelled=b.UserCancelled,userActionAt=b.UserActionAt,userCancelledAt=b.UserCancelledAt,observedAt=b.ObservedAt,cancellationContext=b.CancellationContext,recommendationKey=b.RecommendationKey,recommendationCreatedAt=b.RecommendationCreatedAt,minimumDurationSuppressed=b.MinimumDurationSuppressed,registrationOrigin=b.RegistrationOrigin,importSourceKey=b.ImportSourceKey,searchRuleId=b.SearchRuleId};
 internal static object OptionsJson(QuantumCognitionOptions o)=>new{enabled=o.Enabled,weight=o.Weight,etaScale=o.EtaScale,halfLifeDays=o.HalfLifeDays,precessionDays=o.PrecessionDays,decoherenceHalfLifeDays=o.DecoherenceHalfLifeDays,busyRotationRadians=o.BusyRotationRadians,rivalryBoost=o.RivalryBoost};
 internal static object OptionsFileJson(QuantumCognitionOptions o)=>new{Enabled=o.Enabled,Weight=o.Weight,EtaScale=o.EtaScale,HalfLifeDays=o.HalfLifeDays,PrecessionDays=o.PrecessionDays,DecoherenceHalfLifeDays=o.DecoherenceHalfLifeDays,BusyRotationRadians=o.BusyRotationRadians,RivalryBoost=o.RivalryBoost};
}

internal sealed class QuantumWorkerSession:IQuantumEngine,IDisposable
{
 readonly Process process;
 readonly object gate=new();
 QuantumCognitionOptions? options;
 bool disposed;

 QuantumWorkerSession(Process process)=>this.process=process;

 internal static QuantumWorkerSession? Start()
 {
  string? path=QuantumWorker.Locate();
  if(path==null)return null;
  var process=new Process{StartInfo=new ProcessStartInfo(path,"worker"){RedirectStandardInput=true,RedirectStandardOutput=true,UseShellExecute=false,CreateNoWindow=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=new UTF8Encoding(false)}};
  process.Start();
  var session=new QuantumWorkerSession(process);
  try{var pong=session.Request(new{type="ping"});if(pong.GetProperty("type").GetString()!="pong")throw new IOException("Invalid worker response");return session;}catch{session.Dispose();throw;}
 }

 internal JsonElement Request(object payload)
 {
  lock(gate)
  {
   if(disposed||process.HasExited)throw new InvalidOperationException("量子ワーカーが終了しています。");
   string? line;try{process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(payload)).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();process.StandardInput.FlushAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();line=process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();}catch{try{process.Kill(true);}catch{}throw;}
   if(line==null)throw new InvalidOperationException("量子ワーカーから応答がありません。");
   using var doc=JsonDocument.Parse(line);
   var root=doc.RootElement.Clone();
   if(root.GetProperty("type").GetString()=="error")throw new InvalidOperationException("量子ワーカー: "+root.GetProperty("message").GetString());
   return root;
  }
 }

 internal void Init(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,QuantumCognitionOptions engineOptions)
 {
  options=engineOptions;
  Request(new{type="init",now,options=QuantumWorker.OptionsJson(engineOptions),history=bookings.Select(QuantumWorker.BookingJson).ToArray()});
 }

 public QuantumEvaluation Evaluate(Show show,double classicalFactor)
 {
  var r=Request(new{type="evaluate",query=new{key=show.Key,title=show.Title,station=show.Station,start=show.Start,end=show.End,classicalFactor}});
  double factor=r.GetProperty("factor").GetDouble();
  // The reason text is presentation, not math: build it exactly as the C#
  // engine does so audit files read the same whichever engine answered.
  string reason=options?.Enabled==true?$" 量子認知の文脈係数 {factor:0.00}（試験運用）。":"";
  return new(r.GetProperty("probability").GetDouble(),r.GetProperty("dephasedProbability").GetDouble(),r.GetProperty("quantumFactor").GetDouble(),r.GetProperty("classicalFactor").GetDouble(),factor,r.GetProperty("orderedEvents").GetInt32(),r.GetProperty("legacyEvents").GetInt32(),r.GetProperty("busy").GetBoolean(),reason);
 }

 public void Dispose()
 {
  if(disposed)return;disposed=true;
  try{process.StandardInput.Close();}catch(Exception){}
  try{if(!process.WaitForExit(2000))process.Kill();}catch(Exception){}
  process.Dispose();
 }
}

// Per-run divergence record for compare mode. Reset when a run's engine
// is created; the run's audit file embeds the snapshot.
internal static class QuantumCompareStats
{
 static readonly object Gate=new();
 static int evaluated,failed;static double maxFactor,maxProbability;
 static object? calibration,chaos;
 internal static void Reset(){lock(Gate){evaluated=failed=0;maxFactor=maxProbability=0;calibration=chaos=null;}}
 internal static void RecordEvaluate(double factorDiff,double probabilityDiff){lock(Gate){evaluated++;if(factorDiff>maxFactor)maxFactor=factorDiff;if(probabilityDiff>maxProbability)maxProbability=probabilityDiff;}}
 internal static void RecordFailure(){lock(Gate){failed++;}}
 internal static void RecordCalibration(string kind,QuantumCalibration? csharp,QuantumCalibration? rust)
 {
  object entry=new{csharp=csharp==null?null:new{etaScale=csharp.EtaScale,halfLifeDays=csharp.HalfLifeDays,logLoss=csharp.LogLoss,precessionDays=csharp.PrecessionDays,decoherenceHalfLifeDays=csharp.DecoherenceHalfLifeDays},rust=rust==null?null:new{etaScale=rust.EtaScale,halfLifeDays=rust.HalfLifeDays,logLoss=rust.LogLoss,precessionDays=rust.PrecessionDays,decoherenceHalfLifeDays=rust.DecoherenceHalfLifeDays},logLossDiff=csharp!=null&&rust!=null?Math.Abs(csharp.LogLoss-rust.LogLoss):(double?)null};
  lock(Gate){if(kind=="chaos")chaos=entry;else calibration=entry;}
 }
 internal static object? Snapshot(){lock(Gate){if(evaluated==0&&failed==0&&calibration==null&&chaos==null)return null;return new{evaluated,failed,maxFactorDiff=maxFactor,maxProbabilityDiff=maxProbability,calibration,chaos};}}
}

// Decorator for compare mode: the C# engine's numbers are returned; the
// Rust engine only shadows them into QuantumCompareStats.
internal sealed class ComparingEngine:IQuantumEngine
{
 readonly IQuantumEngine primary;readonly IQuantumEngine shadow;
 internal ComparingEngine(IQuantumEngine primary,IQuantumEngine shadow){this.primary=primary;this.shadow=shadow;}
 public QuantumEvaluation Evaluate(Show show,double classicalFactor)
 {
  var result=primary.Evaluate(show,classicalFactor);
  try{var other=shadow.Evaluate(show,classicalFactor);QuantumCompareStats.RecordEvaluate(Math.Abs(result.Factor-other.Factor),Math.Abs(result.Probability-other.Probability));}
  catch(Exception){QuantumCompareStats.RecordFailure();}
  return result;
 }
}

internal sealed class ResilientQuantumEngine(IQuantumEngine worker,IQuantumEngine fallback):IQuantumEngine {
 bool failed;
 public QuantumEvaluation Evaluate(Show show,double classicalFactor){if(!failed)try{return worker.Evaluate(show,classicalFactor);}catch{failed=true;QuantumCompareStats.RecordFailure();}return fallback.Evaluate(show,classicalFactor);}
}
