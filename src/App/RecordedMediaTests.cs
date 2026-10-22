using System.Diagnostics;
using System.Text.Json;
namespace TsScheduler;
internal static class RecordedMediaTests
{
    internal static async Task<int> Run()
    {
        string root=Path.Combine(Environment.CurrentDirectory,"work","media-tests-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);var lines=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new Exception(name);lines.Add("PASS "+name);}
        try
        {
            var old=new RecordedFile("old.ts","Z",new DateTime(2020,1,1),100,false);var fresh=old with{Path="new.ts",Name="A",Date=new DateTime(2021,1,1),Bytes=50};
            Check(RecordedMediaEngine.Sort([old,fresh],"日付",true)[0]==fresh,"default newest first");Check(RecordedMediaEngine.Sort([old,fresh],"サイズ",false)[0]==fresh,"size ascending");Check(RecordedMediaEngine.Sort([old,fresh],"名前",false)[0]==fresh,"name ascending");
            string locked=Path.Combine(root,"recording.ts");using(var writer=new FileStream(locked,FileMode.Create,FileAccess.Write,FileShare.ReadWrite)){Check(RecordedMediaEngine.BeingWritten(locked),"active writer excluded from encoding");}Check(!RecordedMediaEngine.BeingWritten(locked),"completed recording becomes eligible");
            string first=Path.Combine(root,"日本語 [試験] clip.ts");using var ct=new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var generated=await RecordedMediaEngine.Run("ffmpeg",["-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=30000/1001","-f","lavfi","-i","sine=frequency=440:sample_rate=48000","-t","3","-c:v","mpeg2video","-c:a","mp2","-f","mpegts",first],ct.Token);
            Check(generated.Code==0,"synthetic broadcast-like TS with audio");var before=await RecordedMediaEngine.Probe(first,ct.Token);Check(before.Width==320&&before.Height==180,"FFprobe metadata");
            string? thumb=await RecordedMediaEngine.Thumbnail(new(first,"clip",File.GetLastWriteTime(first),new FileInfo(first).Length,false),Path.Combine(root,"cache"),ct.Token);Check(thumb!=null&&File.Exists(thumb),"FFmpeg preview image");
            long originalBytes=new FileInfo(first).Length;var cpu=new MediaEncoder("CPU","libsvtav1");string encoded=await RecordedMediaEngine.Encode(first,cpu,2,ct.Token,_=>{});var after=await RecordedMediaEngine.Probe(encoded,ct.Token);
            var codec=await RecordedMediaEngine.Run("ffprobe",["-v","error","-select_streams","v:0","-show_entries","stream=codec_name","-of","default=nw=1:nk=1",encoded],ct.Token);
            Check(codec.Output.Trim()=="av1"&&Path.GetExtension(encoded)==".mp4","AV1 inside MP4, no AVC/HEVC encoder");Check(before.Width==after.Width&&before.Height==after.Height&&Math.Abs(RecordedMediaEngine.Rate(before.Fps)-RecordedMediaEngine.Rate(after.Fps))<.02,"size and frame rate retained");Check(new FileInfo(first).Length==originalBytes,"original TS preserved");
            string another=await RecordedMediaEngine.Encode(first,cpu,2,ct.Token,_=>{});Check(another!=encoded&&File.Exists(encoded),"existing output not overwritten");
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool observed=false;try{await RecordedMediaEngine.Encode(first,cpu,2,cancelled.Token,_=>{});}catch(OperationCanceledException){observed=true;}Check(observed&&!Directory.EnumerateFiles(root,".zoo-av1-*.mp4").Any(),"cancellation leaves no partial MP4");}
            var args=RecordedMediaEngine.EncodeArguments(first,"out.mp4",before with{FieldOrder="tt"},new("NVENC BLACKWELL","av1_nvenc"),6);
            Check(args.Contains("bwdif=mode=send_frame:parity=auto:deint=all")&&!args.Contains("-r")&&!args.Any(x=>x.StartsWith("scale=")),"interlaced input does not double frame rate or resize");
            var encoders=await RecordedMediaEngine.Encoders(ct.Token);Check(encoders.Any(e=>e.Codec=="libsvtav1"),"CPU AV1 available for unsupported GPUs");
            if(Environment.GetEnvironmentVariable("STUDIO_TEST_NVENC")=="1")
            {
                string hw=await RecordedMediaEngine.Encode(first,encoders.First(e=>e.Codec=="av1_nvenc"),6,ct.Token,_=>{});Check(File.Exists(hw),"local RTX NVENC AV1 conversion");
            }
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"work","media-test-sample.txt"),first);return 0;
        }
        catch(Exception e){lines.Add(e.ToString());return 1;}
        finally{File.WriteAllLines(Path.Combine(root,"result.txt"),lines);File.WriteAllLines(Path.Combine(Environment.CurrentDirectory,"work","media-tests-latest.txt"),lines);}
    }
    internal static int Ui()
    {
        string root=Path.Combine(Environment.CurrentDirectory,"work","media-ui-tests-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);
        try
        {
            string file=File.ReadAllText(Path.Combine(Environment.CurrentDirectory,"work","media-test-sample.txt"));
            using var form=new Form{ClientSize=new Size(1050,780),Text="録画済みUI試験"};using var view=new RecordedMediaView(()=>Path.GetDirectoryName(file)!,()=>root){Dock=DockStyle.Fill};form.Controls.Add(view);form.Show();view.ActivateLibrary();
            var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<8){Application.DoEvents();Thread.Sleep(20);}
            IEnumerable<Control> Desc(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Desc(x)));
            var controlFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(string field in new[]{"sort","direction","bitrate","encoder"}){var box=(ComboBox)typeof(RecordedMediaView).GetField(field,controlFlags)!.GetValue(view)!;if(box.SelectedIndex<0)throw new Exception("Missing selected value: "+field);File.AppendAllText(Path.Combine(root,"selector-values.txt"),field+"="+box.Text+Environment.NewLine);}
            var play=Desc(view).OfType<Button>().First(b=>b.Text=="再生");play.PerformClick();watch.Restart();while(watch.Elapsed.TotalSeconds<4){Application.DoEvents();Thread.Sleep(20);}
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var playback=(RecordedMediaPlayback)typeof(RecordedMediaView).GetField("playback",flags)!.GetValue(view)!;
            if(playback.Pid==null)throw new Exception("Embedded mpv did not stay alive");
            var queried=playback.Command("get_property","width");watch.Restart();while(!queried.IsCompleted&&watch.Elapsed.TotalSeconds<4){Application.DoEvents();Thread.Sleep(20);}if(!queried.IsCompleted||queried.Result is not {} reply||!reply.TryGetProperty("data",out var width)||width.GetInt32()!=320)throw new Exception("mpv failed to decode the preview");
            using var shot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(root,"preview.png"));
            view.StopPlayback();if(playback.Pid!=null)throw new Exception("Preview kept running after leaving tab");File.WriteAllText(Path.Combine(root,"result.txt"),"PASS recorded-file UI, 320px embedded mpv decoding with IPC, tab-leave cleanup");form.Close();return 0;
        }
        catch(Exception error){File.WriteAllText(Path.Combine(root,"result.txt"),error.ToString());return 1;}
    }
}
