using System.Diagnostics;
using System.Text.Json;
namespace TsScheduler;
internal static class RecordedMediaTests
{
    internal static int ScrollUi()
    {
        string root=Path.Combine(Environment.CurrentDirectory,"work","media-scroll-test-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);var previous=MediaToolsSupport.TestHome;MediaToolsSupport.TestHome=Path.Combine(root,"no-tools");
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        try{
            for(int i=0;i<40;i++)File.WriteAllText(Path.Combine(root,"synthetic-"+i+".mp4"),"synthetic layout test only");
            using var form=new Form{ClientSize=new Size(1050,750),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=false};using var view=new RecordedMediaView(()=>root,()=>root){Dock=DockStyle.Fill};form.Controls.Add(view);form.Show();view.ActivateLibrary();
            var list=(ChromeMediaList)typeof(RecordedMediaView).GetField("list",flags)!.GetValue(view)!;
            void Wait(Func<bool> done){var watch=Stopwatch.StartNew();while(!done()&&watch.Elapsed.TotalSeconds<5){Application.DoEvents();Thread.Sleep(10);}if(!done())throw new Exception("Recorded list did not settle");}
            Wait(()=>list.Controls.Count>=12);while(list.Controls.Count<40)typeof(RecordedMediaView).GetMethod("AppendPage",flags)!.Invoke(view,null);Application.DoEvents();
            list.SetOffset(1200,false);Application.DoEvents();int offset=list.Offset;var first=list.Controls[0];
            view.StopPlayback();view.ActivateLibrary();for(int i=0;i<20;i++){Application.DoEvents();Thread.Sleep(10);}
            if(first.IsDisposed||!ReferenceEquals(first,list.Controls[0])||list.Offset!=offset)throw new Exception("Switching back rebuilt or moved the recorded list");
            typeof(RecordedMediaView).GetMethod("Rebuild",flags)!.Invoke(view,null);Application.DoEvents();if(list.Offset!=offset||list.Controls.Count<40)throw new Exception("Refresh lost the viewport or loaded pages");
            foreach(int width in new[]{560,1050,760}){form.ClientSize=new Size(width,750);Application.DoEvents();if(list.HorizontalScroll.Visible||list.DisplayRectangle.Width>list.ClientSize.Width+1)throw new Exception("Horizontal overflow at width "+width);foreach(Control card in list.Controls)if(card.Width>list.ClientSize.Width)throw new Exception("Card wider than viewport");}
            list.SetOffset(800,false);list.Wheel(-120);if(list.Offset<=800)throw new Exception("Thin scrollbar wheel did not move down");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,"preview.png"));
            File.WriteAllText(Path.Combine(root,"result.txt"),"PASS: tab re-entry preserves card instances and offset; refresh retains pages and offset; no horizontal overflow at 560/760/1050; custom wheel scrolls vertically");return 0;
        }catch(Exception error){File.WriteAllText(Path.Combine(root,"result.txt"),error.ToString());return 1;}finally{MediaToolsSupport.TestHome=previous;}
    }
    internal static int InfernoUi(string sample)
    {
        string root=Path.Combine(Environment.CurrentDirectory,"work","inferno-link-"+Path.GetFileNameWithoutExtension(Application.ExecutablePath));Directory.CreateDirectory(root);
        Process? launched=null;
        try
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            using var form=new Form{ClientSize=new Size(1100,760),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=false};
            using var view=new RecordedMediaView(()=>Path.GetDirectoryName(sample)!,()=>root){Dock=DockStyle.Fill};form.Controls.Add(view);form.Show();
            var info=new FileInfo(sample);var item=new RecordedFile(info.FullName,info.Name,info.LastWriteTime,info.Length,false);
            typeof(RecordedMediaView).GetField("files",flags)!.SetValue(view,new List<RecordedFile>{item,item with{Recording=true}});
            typeof(RecordedMediaView).GetMethod("Rebuild",flags)!.Invoke(view,null);Application.DoEvents();
            var list=(FlowLayoutPanel)typeof(RecordedMediaView).GetField("list",flags)!.GetValue(view)!;
            var cards=list.Controls.Cast<Control>().Where(c=>c.Tag is ValueTuple<RecordedFile,CheckBox>).ToArray();
            Button button=cards[0].Controls.OfType<Button>().Single(b=>b.Text=="Inferno");
            if(!button.Enabled||cards[1].Controls.OfType<Button>().Single(b=>b.Text=="Inferno").Enabled)throw new Exception("Recording exclusion failed");
            if(button.BackColor!=Color.FromArgb(237,247,248))throw new Exception("Button style mismatch");
            using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(root,"preview.png"));}
            var before=Process.GetProcessesByName("inferno").Select(p=>p.Id).ToHashSet();button.PerformClick();var watch=Stopwatch.StartNew();
            while(watch.Elapsed.TotalSeconds<10){Application.DoEvents();launched=Process.GetProcessesByName("inferno").FirstOrDefault(p=>!before.Contains(p.Id)&&p.MainWindowHandle!=IntPtr.Zero);if(launched!=null)break;Thread.Sleep(20);}
            if(launched==null)throw new Exception("Inferno window did not launch");
            File.WriteAllText(Path.Combine(root,"result.txt"),"PASS bundled Inferno button opens separate application; completed recording enabled; active recording disabled; matching button color");return 0;
        }
        catch(Exception error){File.WriteAllText(Path.Combine(root,"result.txt"),error.ToString());return 1;}
        finally{launched?.CloseMainWindow();launched?.Dispose();}
    }
    internal static async Task<int> Run()
    {
        string root=Path.Combine(Environment.CurrentDirectory,"work","media-tests-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);var lines=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new Exception(name);lines.Add("PASS "+name);}
        try
        {
            VerifyPackInstall(root);Check(true,"separate pack install, reuse and integrity failure preserve active pack");
            var old=new RecordedFile("old.ts","Z",new DateTime(2020,1,1),100,false);var fresh=old with{Path="new.ts",Name="A",Date=new DateTime(2021,1,1),Bytes=50};
            Check(RecordedMediaEngine.Sort([old,fresh],"日付",true)[0]==fresh,"default newest first");Check(RecordedMediaEngine.Sort([old,fresh],"サイズ",false)[0]==fresh,"size ascending");Check(RecordedMediaEngine.Sort([old,fresh],"名前",false)[0]==fresh,"name ascending");
            string locked=Path.Combine(root,"recording.ts");using(var writer=new FileStream(locked,FileMode.Create,FileAccess.Write,FileShare.ReadWrite)){Check(RecordedMediaEngine.BeingWritten(locked),"active writer excluded from encoding");}Check(!RecordedMediaEngine.BeingWritten(locked),"completed recording becomes eligible");
            string first=Path.Combine(root,"日本語 [試験] clip.ts");using var ct=new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var generated=await RecordedMediaEngine.Run("ffmpeg",["-v","error","-f","lavfi","-i","testsrc2=size=320x180:rate=30000/1001","-f","lavfi","-i","sine=frequency=440:sample_rate=48000","-t","3","-c:v","mpeg2video","-c:a","mp2","-f","mpegts",first],ct.Token);
            Check(generated.Code==0,"synthetic broadcast-like TS with audio");var before=await RecordedMediaEngine.Probe(first,ct.Token);Check(before.Width==320&&before.Height==180,"FFprobe metadata");
            string anamorphic=Path.Combine(root,"anamorphic.ts");var ar=await RecordedMediaEngine.Run("ffmpeg",["-v","error","-f","lavfi","-i","testsrc2=size=720x480:rate=30000/1001","-vf","setsar=32/27","-t","2","-c:v","mpeg2video","-an","-f","mpegts",anamorphic],ct.Token);Check(ar.Code==0,"anamorphic test fixture");var arInfo=await RecordedMediaEngine.Probe(anamorphic,ct.Token);Check(arInfo.DisplayAspectRatio=="16:9","read display ratio from file metadata");string? arThumb=await RecordedMediaEngine.Thumbnail(new(anamorphic,"anamorphic",File.GetLastWriteTime(anamorphic),new FileInfo(anamorphic).Length,false),Path.Combine(root,"cache"),ct.Token);using(var bitmap=Image.FromFile(arThumb!)){Check(bitmap.Width==320&&bitmap.Height==180,"non-square pixels produce 16:9 thumbnail");}Check(RecordedMediaEngine.EncodeArguments(anamorphic,"test.mp4",arInfo,new("CPU","libsvtav1"),2).Contains("16:9"),"conversion explicitly retains display ratio");
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

    static void VerifyPackInstall(string root)
    {
        var previous=MediaToolsSupport.TestHome;
        try{
            MediaToolsSupport.TestHome=Path.Combine(root,"tool-home");
            var source=Path.Combine(root,"fake-pack");Directory.CreateDirectory(Path.Combine(source,"licenses"));
            var names=new[]{"ffmpeg.exe","ffprobe.exe","mpv.exe","d3dcompiler_43.dll","SOURCES.txt"};
            var rows=new List<string>{"\"Name\",\"SHA256\""};
            foreach(var name in names){var file=Path.Combine(source,name);File.WriteAllText(file,"synthetic test only "+name);rows.Add("\""+name+"\",\""+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)))+"\"");}
            File.WriteAllLines(Path.Combine(source,"SHA256.csv"),rows);
            foreach(var name in new[]{"FFmpeg-GPLv3.txt","FFprobe-GPLv3.txt","mpv-GPL.txt"})File.WriteAllText(Path.Combine(source,"licenses",name),"test");
            var installed=MediaToolsSupport.Install(source);var tool=Path.Combine(installed,"ffmpeg.exe");var stamp=File.GetLastWriteTimeUtc(tool);
            if(MediaToolsSupport.Install(source)!=installed||File.GetLastWriteTimeUtc(tool)!=stamp)throw new Exception("Existing media executable was recopied");
            MediaToolsSupport.Install(installed);
            File.AppendAllText(Path.Combine(source,"ffmpeg.exe"),"corrupted");
            bool rejected=false;try{MediaToolsSupport.Install(source);}catch(InvalidDataException){rejected=true;}
            if(!rejected||File.ReadAllText(Path.Combine(MediaToolsSupport.TestHome,"active.txt"))!=installed)throw new Exception("Bad media pack replaced the current selection");
        }finally{MediaToolsSupport.TestHome=previous;}
    }

    internal static int SelectionUi()
    {
        string root=Path.Combine(Environment.CurrentDirectory,"work","media-selection45-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        try{
            using var form=new Form{ClientSize=new Size(1050,750)};using var view=new RecordedMediaView(()=>root,()=>root);view.Dock=DockStyle.Fill;form.Controls.Add(view);form.Show();
            var files=Enumerable.Range(0,4).Select(i=>new RecordedFile(Path.Combine(root,i+".mp4"),"title"+i,DateTime.Today.AddDays(-i),100,false)).ToList();
            typeof(RecordedMediaView).GetField("files",flags)!.SetValue(view,files);typeof(RecordedMediaView).GetMethod("Rebuild",flags)!.Invoke(view,null);
            var select=typeof(RecordedMediaView).GetMethod("SelectFile",flags)!;var selected=(HashSet<string>)typeof(RecordedMediaView).GetField("selected",flags)!.GetValue(view)!;
            void Choose(int index,Keys keys)=>select.Invoke(view,[files[index],keys,null]);
            Choose(0,Keys.None);Choose(2,Keys.Control);if(!selected.SetEquals(new[]{files[0].Path,files[2].Path}))throw new Exception("Ctrl multi selection");Choose(3,Keys.Shift);if(!selected.SetEquals(new[]{files[2].Path,files[3].Path}))throw new Exception("Shift range selection");Choose(0,Keys.Control|Keys.Shift);if(selected.Count!=4)throw new Exception("Ctrl Shift additive range");Choose(1,Keys.None);if(selected.Count!=1||!selected.Contains(files[1].Path))throw new Exception("Plain selection");
            var list=(FlowLayoutPanel)typeof(RecordedMediaView).GetField("list",flags)!.GetValue(view)!;var card=list.Controls.Cast<Control>().First(c=>c.Tag is ValueTuple<RecordedFile,CheckBox>);var title=card.Controls.OfType<Label>().First();if(!title.Font.Bold||Math.Abs(title.Font.Size-view.Font.Size)>.01)throw new Exception("Title font weight/size");
            var unused=card.Controls.OfType<Label>().Last();typeof(Control).GetMethod("OnMouseDown",flags)!.Invoke(unused,[new MouseEventArgs(MouseButtons.Left,1,unused.Width-10,unused.Height-10,0)]);if(!selected.SetEquals(new[]{((ValueTuple<RecordedFile,CheckBox>)card.Tag!).Item1.Path}))throw new Exception("Right-hand card whitespace not selectable");
            var play=card.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().First();play.Text="Ⅱ";using(var image=new Bitmap(play.Width,play.Height)){play.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"pause.png"));}
            using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(root,"preview.png"));}
            if(!new DisplayOptions().ReservationDetailsPopup)throw new Exception("Popup default must be on");File.WriteAllText(Path.Combine(root,"result.txt"),"PASS Ctrl/Shift/CtrlShift/plain selection; MP4 selectable; title bold same size; pause drawn; popup default on");form.Close();return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(root,"result.txt"),e.ToString());return 1;}
    }
    internal static int Ui()
    {
        Environment.SetEnvironmentVariable("STUDIO_MEDIA_TEST_MUTE","1");
        string root=Path.Combine(Environment.CurrentDirectory,"work","media-ui-tests-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);
        try
        {
            string file=File.ReadAllText(Path.Combine(Environment.CurrentDirectory,"work","media-test-sample.txt"));
            using var form=new Form{ClientSize=new Size(1050,780),Text="録画済みUI試験"};using var view=new RecordedMediaView(()=>Path.GetDirectoryName(file)!,()=>root){Dock=DockStyle.Fill};form.Controls.Add(view);form.Show();view.ActivateLibrary();
            var watch=Stopwatch.StartNew();while(watch.Elapsed.TotalSeconds<8){Application.DoEvents();Thread.Sleep(20);}
            IEnumerable<Control> Desc(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Desc(x)));
            var controlFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(string field in new[]{"sort","direction","bitrate","encoder"}){var box=(ComboBox)typeof(RecordedMediaView).GetField(field,controlFlags)!.GetValue(view)!;if(box.SelectedIndex<0)throw new Exception("Missing selected value: "+field);File.AppendAllText(Path.Combine(root,"selector-values.txt"),field+"="+box.Text+Environment.NewLine);}
            var play=Desc(view).OfType<Button>().First(b=>b.Text=="▶");play.PerformClick();if(!Desc(play.Parent!.Parent!).OfType<PictureBox>().Single().Visible)throw new Exception("Thumbnail hidden before video ready");watch.Restart();while(watch.Elapsed.TotalSeconds<4){Application.DoEvents();Thread.Sleep(20);}
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var playback=(RecordedMediaPlayback)typeof(RecordedMediaView).GetField("playback",flags)!.GetValue(view)!;
            if(playback.Pid==null)throw new Exception("Embedded mpv did not stay alive");if(Desc(play.Parent!.Parent!).OfType<PictureBox>().Single().Visible)throw new Exception("Video never replaced thumbnail after ready");
            var queried=playback.Command("get_property","width");watch.Restart();while(!queried.IsCompleted&&watch.Elapsed.TotalSeconds<4){Application.DoEvents();Thread.Sleep(20);}if(!queried.IsCompleted||queried.Result is not {} reply||!reply.TryGetProperty("data",out var width)||width.GetInt32()!=320)throw new Exception("mpv failed to decode the preview");
            using var shot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(root,"preview.png"));
            view.StopPlayback();if(playback.Pid!=null)throw new Exception("Preview kept running after leaving tab");File.WriteAllText(Path.Combine(root,"result.txt"),"PASS recorded-file UI, thumbnail retained until video ready, 320px embedded mpv decoding with IPC, muted test, tab-leave cleanup");form.Close();return 0;
        }
        catch(Exception error){File.WriteAllText(Path.Combine(root,"result.txt"),error.ToString());return 1;}
    }
}
