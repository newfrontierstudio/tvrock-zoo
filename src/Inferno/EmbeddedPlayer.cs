using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
namespace CMCutLab;
internal sealed class EmbeddedPlayer:IDisposable
{
 Process? process;NamedPipeClientStream? pipe;StreamWriter? writer;CancellationTokenSource? lifetime;readonly SemaphoreSlim writes=new(1,1);Task? reader;string pipeName="";bool disposed;
 readonly System.Windows.Forms.Timer reverseClock=new(){Interval=50};
 readonly Stopwatch reverseElapsed=new();double reverseRate,reverseTarget;bool reverseSeeking;long reverseSeekAt;
 async void ReverseTick(object? sender,EventArgs e){
  if(reverseRate>=0||!Ready)return;
  double elapsed=reverseElapsed.Elapsed.TotalSeconds;reverseElapsed.Restart();
  reverseTarget=Math.Max(0,reverseTarget+reverseRate*elapsed);
  if(reverseSeeking&&Environment.TickCount64-reverseSeekAt<500)return;
  reverseSeeking=true;reverseSeekAt=Environment.TickCount64;
  await Seek(reverseTarget);
 }
 internal EmbeddedPlayer(){reverseClock.Tick+=ReverseTick;}

 internal readonly System.Collections.Concurrent.ConcurrentQueue<string> Trace=new();
 void AddTrace(string s){Trace.Enqueue(s);while(Trace.Count>100)Trace.TryDequeue(out _);}
 internal int VideoProcessId=>process?.Id??0;
 internal bool Ready=>writer!=null;internal bool Paused{get;private set;}=true;internal double Position{get;private set;}
 internal event Action? VideoShuttleBegan;internal event Action<double?>? VideoShuttle;internal bool MouseScriptReady{get;private set;}
 internal string ShuttleLabel{get;private set;}="";internal event Action<string>? Shortcut;internal event Action<double>? PositionChanged;internal event Action<bool>? PauseChanged;internal event Action<string>? Error;
 internal async Task Open(IntPtr host,string file,CancellationToken ct){Close();disposed=false;pipeName="CMEditLab-"+Guid.NewGuid().ToString("N");lifetime=CancellationTokenSource.CreateLinkedTokenSource(ct);var token=lifetime.Token;
 Directory.CreateDirectory(Engine.Home);string input=Path.Combine(Engine.Home,"manual-input.conf");File.WriteAllText(input,"SPACE cycle pause\nLEFT frame-back-step\nRIGHT frame-step\ni script-message cm-in\no script-message cm-out\nShift+LEFT script-message cm-back10\nShift+RIGHT script-message cm-forward10\nCtrl+z script-message cm-undo\nCtrl+y script-message cm-redo\n",Encoding.UTF8);
 var si=Engine.Info("mpv",["--no-config","--no-terminal","--background-color=#272A2E","--osd-font-size=32","--osd-font=Arial","--osd-shadow-offset=0","--osd-border-size=0","--osd-align-x=center","--osd-align-y=center","--osd-margin-x=24","--osd-margin-y=20","--idle=yes","--keep-open=yes","--pause=yes","--hr-seek=yes","--hr-seek-framedrop=no","--hr-seek-demuxer-offset=1.5","--force-window=yes","--wid="+host.ToInt64(),"--input-ipc-server=\\\\.\\pipe\\"+pipeName,"--input-conf="+input,"--input-default-bindings=no","--osc=no","--osd-level=0","--hwdec=auto-safe","--input-vo-keyboard=yes",file]);process=Process.Start(si)??throw new IOException("再生部品を起動できません。");_=process.StandardOutput.ReadToEndAsync();_=DrainErrors(process,token);
 pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);try{await pipe.ConnectAsync(5000,token);writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};reader=Read(pipe,token);await Command(["observe_property",1,"time-pos"]);await Command(["observe_property",2,"pause"]);Paused=true;}catch{Close();throw;}}
 async Task DrainErrors(Process p,CancellationToken ct){try{while(await p.StandardError.ReadLineAsync(ct) is string line){if(line.Contains("Error")||line.Contains("Failed"))Error?.Invoke(line);}}catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(IOException){}}
 async Task Read(Stream stream,CancellationToken ct){using var input=new StreamReader(stream,Encoding.UTF8,false,4096,true);try{while(await input.ReadLineAsync(ct) is string line){using var json=JsonDocument.Parse(line);var e=json.RootElement;if(e.TryGetProperty("error",out var error))AddTrace("REPLY "+line);if(e.TryGetProperty("event",out var finished)&&finished.GetString()=="playback-restart")reverseSeeking=false;if(e.TryGetProperty("event",out var commandEvent)&&commandEvent.GetString()=="client-message"&&e.TryGetProperty("args",out var arguments)&&arguments.GetArrayLength()>0){string command=arguments[0].GetString()??"";if(command=="inferno-mouse-ready")MouseScriptReady=true;else if(command=="cm-shuttle-begin")VideoShuttleBegan?.Invoke();else if(command=="cm-shuttle-end")VideoShuttle?.Invoke(null);else if(command=="cm-shuttle-distance"&&arguments.GetArrayLength()>1&&double.TryParse(arguments[1].GetString(),System.Globalization.CultureInfo.InvariantCulture,out var distance))VideoShuttle?.Invoke(distance);else Shortcut?.Invoke(command);}if(e.TryGetProperty("event",out var ev)&&ev.GetString()=="property-change"&&e.TryGetProperty("name",out var name)&&e.TryGetProperty("data",out var value)){if(name.GetString()=="time-pos"&&value.ValueKind==JsonValueKind.Number){Position=value.GetDouble();PositionChanged?.Invoke(Position);}else if(name.GetString()=="pause"&&value.ValueKind is JsonValueKind.True or JsonValueKind.False){Paused=value.GetBoolean();PauseChanged?.Invoke(Paused);}}}}catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(IOException){}catch(JsonException){}}
 internal async Task Command(object[] command){if(writer==null)return;await writes.WaitAsync();try{if(writer!=null){string line=JsonSerializer.Serialize(new{command});AddTrace(line);await writer.WriteLineAsync(line);}}catch(IOException){if(!disposed)Error?.Invoke("再生との接続が切れました。");}catch(ObjectDisposedException){}finally{writes.Release();}}
 internal Task Seek(double time)=>Command(["seek",time,"absolute+exact"]);
 internal Task Pause(bool pause)=>Command(["set_property","pause",pause]);
 internal Task Toggle()=>Command(["cycle","pause"]);
 internal async Task Shuttle(double rate){
  ShuttleLabel=rate==0?"PAUSED":FormattableString.Invariant($"{(rate<0?"REVERSE":"FORWARD")} x{Math.Abs(rate):0.00}");
  bool wasReverse=reverseRate<0;
  if(rate<0){
   reverseRate=rate;
   if(!wasReverse){
    reverseTarget=Math.Max(0,Position-1.0/30);reverseSeeking=false;reverseElapsed.Restart();
    await Pause(true);await Command(["set_property","speed",1]);
    reverseClock.Start();ReverseTick(null,EventArgs.Empty);
   }
  }else{
   reverseRate=0;reverseClock.Stop();reverseElapsed.Reset();
   if(rate==0){await Pause(true);await Command(["set_property","speed",1]);}
   else {await Command(["set_property","speed",Math.Clamp(rate,.01,128)]);if(wasReverse||Paused)await Pause(false);}
  }
  await Command(["show-text",ShuttleLabel,rate==0?800:60000]);
 }
 internal Task Step(bool back)=>Command([back?"frame-back-step":"frame-step"]);
 internal void Close(){reverseRate=0;reverseClock.Stop();reverseElapsed.Reset();reverseSeeking=false;disposed=true;lifetime?.Cancel();writer?.Dispose();writer=null;pipe?.Dispose();pipe=null;if(process!=null){try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}process.Dispose();process=null;}lifetime?.Dispose();lifetime=null;Paused=true;Position=0;MouseScriptReady=false;}
 public void Dispose(){Close();reverseClock.Dispose();}
}
