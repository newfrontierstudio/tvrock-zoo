using System.Diagnostics;
using System.Globalization;
namespace CMCutLab;
// C# adaptation of FilmCraft crates/export/src/pace.rs, MIT option.
// Copyright (c) 2026 ArtCraft Team and the FilmCraft contributors.
// See licenses/FilmCraft-MIT.txt and THIRD-PARTY-NOTICES.txt.
internal sealed class PaceEstimate
{
 readonly List<(double Time,double Done)> readings=[];
 internal void Observe(double now,double done){if(readings.Count>0&&done<readings[^1].Done)readings.Clear();if(readings.Count==0||now-readings[^1].Time>=.1)readings.Add((now,done));while(readings.Count>2&&now-readings[1].Time>=15)readings.RemoveAt(0);if(readings.Count>0){double first=readings[0].Done;int moved=readings.FindIndex(r=>r.Done!=first);if(moved>=2&&readings[moved-1].Time-readings[0].Time>=1)readings.RemoveRange(0,moved-1);}}
 internal double? Remaining(double remaining){if(readings.Count<2)return null;var a=readings[0];var b=readings[^1];double span=b.Time-a.Time;if(span<1||b.Done<=a.Done)return null;return Math.Clamp(remaining/((b.Done-a.Done)/span),0,100*3600);}
}
internal record RenderSnapshot(string Phase,double Fraction,double Processed,double Total,TimeSpan Elapsed,TimeSpan? Remaining,DateTimeOffset? Finish,double Fps,double Speed,long Bytes,long Frames,bool Active,string Error);
internal sealed class RenderJob
{
 readonly object sync=new();readonly Stopwatch clock=Stopwatch.StartNew();readonly PaceEstimate pace=new();readonly Dictionary<string,string> values=[];string phase="準備中",error="";double processed,fps;long bytes,frames;bool active=true;TimeSpan? finalElapsed;
 internal readonly string Input,Output,Settings;internal readonly double Total;
 internal RenderJob(string input,string output,string settings,double total){Input=input;Output=output;Settings=settings;Total=total;}
 static double Number(string? text)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:0;
 internal void Feed(string line){int split=line.IndexOf('=');if(split<0)return;lock(sync){values[line[..split]]=line[(split+1)..];if(!line.StartsWith("progress="))return;processed=Math.Clamp(Number(values.GetValueOrDefault("out_time_us"))/1e6,0,Total);fps=Number(values.GetValueOrDefault("fps"));bytes=(long)Math.Max(0,Number(values.GetValueOrDefault("total_size")));frames=(long)Math.Max(0,Number(values.GetValueOrDefault("frame")));if(phase!="キャンセル中")phase=processed>=Total-.05?"MP4を仕上げ中":"エンコード中";pace.Observe(clock.Elapsed.TotalSeconds,processed);}}
 internal void Log(string line){if(line.Contains("Starting second pass",StringComparison.OrdinalIgnoreCase))lock(sync){if(phase!="キャンセル中")phase="MP4を仕上げ中";}}
 internal void Cancelling(){lock(sync){if(active)phase="キャンセル中";}}
 internal void Finish(string result,string? message=null){lock(sync){phase=result;error=message??"";active=false;finalElapsed=clock.Elapsed;if(result=="完了"){processed=Total;if(File.Exists(Output))bytes=new FileInfo(Output).Length;}}}
 internal RenderSnapshot Snapshot(){lock(sync){var elapsed=finalElapsed??clock.Elapsed;double? remaining=null;if(active&&phase=="エンコード中"){pace.Observe(elapsed.TotalSeconds,processed);remaining=pace.Remaining(Math.Max(0,Total-processed));}double speed=elapsed.TotalSeconds<1?0:processed/elapsed.TotalSeconds;return new(phase,Total>0?processed/Total:0,processed,Total,elapsed,remaining.HasValue?TimeSpan.FromSeconds(remaining.Value):null,remaining.HasValue?DateTimeOffset.Now.AddSeconds(remaining.Value):null,fps,speed,bytes,frames,active,error);}}
}
