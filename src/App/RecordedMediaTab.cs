using TsRecommend;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace TsScheduler;

internal sealed partial class MainForm
{
    internal static Func<string>? RecordedFolderOverride;
    void InitializeRecordedMedia()
    {
        var page=new TabPage("録画済み"){Padding=new Padding(3)};
        var view=new RecordedMediaView(()=>RecordedFolderOverride?.Invoke()??DefaultRecordedFolder(),()=>ConnectionProfiles.Current.Active.Home,RecordedFolderOverride==null){Dock=DockStyle.Fill};
        page.Controls.Add(view);_tabs.TabPages.Insert(Math.Min(3,_tabs.TabPages.Count),page);
        page.VisibleChanged+=(_,_)=>{if(page.Visible)view.ActivateLibrary();else view.StopPlayback();};
    }
    static string DefaultRecordedFolder()
    {
        string configured=Path.Combine(ConnectionProfiles.Current.Active.Home,"recorded-folder.txt");if(File.Exists(configured))return File.ReadAllText(configured).Trim();
        string ini=Path.Combine(RockFolder,"dtv.ini");
        if(File.Exists(ini))
        {
            var folders=Regex.Matches(File.ReadAllText(ini,TsRecommend.Data.Cp932),@"(?mi)^RECPATH=([^\r\n]+)").Select(m=>m.Groups[1].Value.Trim().Trim('"')).Where(s=>s.Length>0).Select(s=>Path.GetFullPath(Path.IsPathFullyQualified(s)?s:Path.Combine(RockFolder,s))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(folders.Length==1)return folders[0];
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    }
}
internal sealed record RecordedQueueItem(string Input,string Encoder,int Bitrate,string State,string Detail);
internal sealed class RecordedMediaView:UserControl
{
    readonly Func<string> folder,home;
    readonly Label pathLabel=new(){AutoSize=true},status=new(){AutoSize=true},progress=new(){AutoSize=true};
    readonly ComboBox sort=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=85},direction=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=75},bitrate=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=90},encoder=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=210};
    readonly FlowLayoutPanel list=new(){Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.White};
    readonly Button convert=Button("選択した録画を変換"),cancel=Button("変換を中止"),more=Button("さらに表示");
    readonly HashSet<string> selected=new(StringComparer.OrdinalIgnoreCase);
    readonly List<RecordedQueueItem> jobs=[];List<RecordedFile> files=[];
    readonly RecordedMediaPlayback playback=new();readonly System.Windows.Forms.Timer debounce=new(){Interval=900},poll=new(){Interval=10000};
    readonly SemaphoreSlim imageGate=new(2);readonly CancellationTokenSource lifetime=new();CancellationTokenSource? rendering,batch;
    FileSystemWatcher? watcher;string watched="",signature="";bool active,loading,reloading,again,encodersLoaded;int shown;Action? resetPreview;string playingFile="";
    internal RecordedMediaView(Func<string> folder,Func<string> home,bool allowChoose=true)
    {
        this.folder=folder;this.home=home;BackColor=Color.White;Font=new Font("Meiryo UI",10);
        foreach(var box in new[]{sort,direction,bitrate,encoder}){box.DrawMode=DrawMode.OwnerDrawFixed;box.DrawItem+=(_,e)=>{e.DrawBackground();string text=e.Index>=0?box.Items[e.Index]?.ToString()??"":box.Text;TextRenderer.DrawText(e.Graphics,text,box.Font,e.Bounds,box.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);e.DrawFocusRectangle();};}
        sort.Items.AddRange(["日付","名前","サイズ"]);sort.SelectedIndex=0;direction.Items.AddRange(["降順","昇順"]);direction.SelectedIndex=0;bitrate.Items.AddRange(["自動","2 Mbps","4 Mbps","6 Mbps","8 Mbps","12 Mbps","16 Mbps"]);bitrate.SelectedIndex=3;
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(10),WrapContents=true};Button selectAll=Button("全選択"),refresh=Button("一覧を更新"),choose=Button("フォルダーを選ぶ");
        choose.Visible=allowChoose;
        top.Controls.AddRange([pathLabel,choose,Label("並び替え"),sort,direction,selectAll,refresh,status]);
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(10),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        bottom.Controls.Add(Label("MP4（AV1）バッチ変換"));var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true,MaximumSize=new Size(1200,0)};actions.Controls.AddRange([Label("ビットレート（一括）"),bitrate,encoder,convert,cancel]);bottom.Controls.Add(actions);
        bottom.Controls.Add(Label("元のサイズ・FPSを維持 ／ 元TSは残す ／ 音声AAC・放送字幕とデータ放送は変換対象外"));bottom.Controls.Add(progress);cancel.Enabled=false;
        Controls.Add(list);Controls.Add(bottom);Controls.Add(top);
        SizeChanged+=(_,_)=>{actions.MaximumSize=new Size(Math.Max(300,ClientSize.Width-30),0);};
        sort.SelectedIndexChanged+=(_,_)=>Rebuild();direction.SelectedIndexChanged+=(_,_)=>Rebuild();selectAll.Click+=(_,_)=>{var eligible=files.Where(f=>!f.Recording&&f.Bytes>0&&Path.GetExtension(f.Path).Equals(".ts",StringComparison.OrdinalIgnoreCase)).ToArray();if(eligible.All(f=>selected.Contains(f.Path)))selected.Clear();else foreach(var f in eligible)selected.Add(f.Path);Rebuild();};
        refresh.Click+=async(_,_)=>await Reload(true);choose.Click+=async(_,_)=>{using var dialog=new FolderBrowserDialog{InitialDirectory=folder()};if(dialog.ShowDialog(this)!=DialogResult.OK)return;Directory.CreateDirectory(home());File.WriteAllText(Path.Combine(home(),"recorded-folder.txt"),dialog.SelectedPath);localFolder=dialog.SelectedPath;await Reload(true);};
        convert.Click+=async(_,_)=>await StartBatch();cancel.Click+=(_,_)=>batch?.Cancel();
        debounce.Tick+=async(_,_)=>{debounce.Stop();await Reload(false);};poll.Tick+=async(_,_)=>{if(active)await Reload(false);};poll.Start();
        list.Resize+=(_,_)=>{foreach(Control card in list.Controls)card.Width=Math.Max(320,list.ClientSize.Width-25);};list.Scroll+=(_,_)=>{if(shown<files.Count&&-list.AutoScrollPosition.Y+list.ClientSize.Height>=list.DisplayRectangle.Height-300)AppendPage();};more.Click+=(_,_)=>AppendPage();
        Disposed+=(_,_)=>{lifetime.Cancel();rendering?.Cancel();batch?.Cancel();watcher?.Dispose();debounce.Dispose();poll.Dispose();playback.Dispose();};
    }
    string? localFolder;
    static Button Button(string text)=>new(){Text=text,AutoSize=true,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(237,247,248),Padding=new Padding(5,2,5,2),Margin=new Padding(4)};
    static Label Label(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(4,8,4,4)};
    internal void ActivateLibrary(){if(IsDisposed)return;active=true;_ = Reload(true);if(!encodersLoaded){encodersLoaded=true;_ = LoadEncoders();}}
    internal void StopPlayback(){active=false;playback.Stop();resetPreview?.Invoke();resetPreview=null;playingFile="";}
    async Task LoadEncoders(){try{var encoders=await RecordedMediaEngine.Encoders(lifetime.Token);if(IsDisposed)return;encoder.Items.Clear();foreach(var item in encoders)encoder.Items.Add(item);encoder.SelectedIndex=0;SelectionStatus();}catch(Exception e)when(e is not OutOfMemoryException){if(!IsDisposed)progress.Text=e.Message;}}
    async Task Reload(bool force)
    {
        if(IsDisposed)return;if(reloading){again|=force;return;}reloading=true;
        try
        {
            string current=Path.GetFullPath(localFolder??folder());pathLabel.Text=current;
            if(watched!=current)
            {
                watcher?.Dispose();watcher=null;watched=current;
                if(Directory.Exists(current)){watcher=new FileSystemWatcher(current){NotifyFilter=NotifyFilters.FileName,SynchronizingObject=this};FileSystemEventHandler changed=(_,_)=>{if(IsDisposed||!active)return;debounce.Stop();debounce.Start();};watcher.Created+=changed;watcher.Deleted+=changed;watcher.Renamed+=(_,_)=>{if(!IsDisposed&&active){debounce.Stop();debounce.Start();}};watcher.Error+=(_,_)=>{if(!IsDisposed&&active){debounce.Stop();debounce.Start();}};watcher.EnableRaisingEvents=true;}
            }
            var read=await Task.Run(()=>RecordedMediaEngine.Read(current),lifetime.Token);if(IsDisposed)return;
            string next=string.Join('|',read.OrderBy(f=>f.Path).Select(f=>f.Path+":"+f.Recording));
            if(force||signature!=next){signature=next;files=read;selected.IntersectWith(files.Select(f=>f.Path));Rebuild();}
        }
        catch(Exception e)when(e is not OutOfMemoryException){if(!IsDisposed)status.Text="読み込み失敗："+e.Message;}
        finally{reloading=false;if(again&&!IsDisposed){again=false;_ = Reload(true);}}
    }
    void Rebuild()
    {
        if(loading||IsDisposed)return;loading=true;
        try
        {
            playback.Stop();resetPreview?.Invoke();resetPreview=null;playingFile="";rendering?.Cancel();rendering?.Dispose();rendering=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            foreach(Control child in list.Controls.Cast<Control>().ToArray())if(child!=more)child.Dispose();list.Controls.Clear();shown=0;
            files=RecordedMediaEngine.Sort(files,sort.Text,direction.Text=="降順");AppendPage();SelectionStatus();
        }
        finally{loading=false;}
    }
    void AppendPage()
    {
        list.SuspendLayout();list.Controls.Remove(more);
        foreach(var file in files.Skip(shown).Take(12)){list.Controls.Add(Card(file,rendering?.Token??lifetime.Token));shown++;}
        if(shown<files.Count)list.Controls.Add(more);list.ResumeLayout();
    }
    void SelectionStatus(){status.Text=$"{files.Count}件 ／ {selected.Count}件選択";convert.Enabled=batch==null&&selected.Count>0&&encoder.SelectedItem!=null;}
    Control Card(RecordedFile file,CancellationToken ct)
    {
        var card=new Panel{Width=Math.Max(320,list.ClientSize.Width-25),Height=300,Margin=new Padding(0,0,0,8),BackColor=selected.Contains(file.Path)?Color.FromArgb(237,247,248):Color.White};
        var check=new CheckBox{Location=new Point(9,8),Size=new Size(24,24),Checked=selected.Contains(file.Path),Enabled=!file.Recording&&file.Bytes>0&&Path.GetExtension(file.Path).Equals(".ts",StringComparison.OrdinalIgnoreCase)};
        var title=new Label{Text=(file.Station.Length>0?file.Station+"　":"")+file.Name,Location=new Point(40,8),Height=43,Width=card.Width-50,AutoEllipsis=true,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};card.Controls.AddRange([check,title]);
        var surface=new Panel{Location=new Point(40,54),Size=new Size(320,180),BackColor=Color.FromArgb(25,36,44)};var image=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=surface.BackColor};surface.Controls.Add(image);card.Controls.Add(surface);
        var details=new Label{AutoSize=false,Location=new Point(375,54),Size=new Size(Math.Max(150,card.Width-390),150),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Text=$"{file.Date:yyyy/MM/dd HH:mm}\n{file.Bytes/1073741824d:F2} GB\n{Path.GetExtension(file.Path).TrimStart('.').ToUpperInvariant()}"+(file.Recording?"\n● 録画中（変換対象外）":"")};card.Controls.Add(details);
        Button play=Button("再生"),stop=Button("停止");play.Location=new Point(37,235);stop.Location=new Point(100,235);play.Enabled=file.Bytes>0;
        var seek=new TrackBar{Location=new Point(165,235),Width=135,Height=25,Maximum=1000,TickStyle=TickStyle.None};var volume=new TrackBar{Location=new Point(300,235),Width=60,Height=25,Maximum=100,Value=70,TickStyle=TickStyle.None,AccessibleName="音量"};var transport=new FlowLayoutPanel{Location=new Point(40,237),Size=new Size(320,42),WrapContents=false,Margin=Padding.Empty};play.AutoSize=false;play.Size=new Size(110,38);stop.AutoSize=false;stop.Size=new Size(65,38);seek.Width=95;volume.Width=50;foreach(Control c in new Control[]{play,stop,seek,volume})c.Margin=Padding.Empty;transport.Controls.AddRange([play,stop,seek,volume]);card.Controls.Add(transport);double duration=0;
        play.Click+=async(_,_)=>{try{if(play.Tag is true&&playingFile==file.Path&&playback.Pid!=null){await playback.Command("cycle","pause");return;}resetPreview?.Invoke();playback.Start(file.Path,surface);playingFile=file.Path;resetPreview=()=>{if(!play.IsDisposed){play.Tag=false;play.Text="再生";image.Visible=true;}};play.Tag=true;play.Text="一時停止";image.Visible=false;await Task.Delay(700,ct);if(playback.Pid==null&&!play.IsDisposed){play.Tag=false;play.Text="再生";image.Visible=true;progress.Text="プレビューを開始できませんでした。";}}catch(Exception e)when(e is not OutOfMemoryException){if(!IsDisposed)progress.Text=e.Message;}};
        stop.Click+=(_,_)=>{playback.Stop();play.Tag=false;play.Text="再生";image.Visible=true;};seek.MouseUp+=async(_,_)=>{if(play.Tag is true&&duration>0)await playback.Command("seek",duration*seek.Value/1000,"absolute");};volume.ValueChanged+=async(_,_)=>{if(play.Tag is true)await playback.Command("set_property","volume",volume.Value);};
        check.CheckedChanged+=(_,_)=>{if(check.Checked)selected.Add(file.Path);else selected.Remove(file.Path);card.BackColor=check.Checked?Color.FromArgb(237,247,248):Color.White;SelectionStatus();};
        card.Resize+=(_,_)=>{bool compact=card.Width<570;surface.Top=54;details.Location=compact?new Point(40,290):new Point(375,54);details.Size=new Size(compact?Math.Max(260,card.Width-50):Math.Max(150,card.Width-390),compact?100:150);card.Height=compact?420:300;};
        card.Disposed+=(_,_)=>image.Image?.Dispose();
        _ = LoadImage();return card;
        async Task LoadImage()
        {
            bool entered=false;
            try
            {
                await imageGate.WaitAsync(ct);entered=true;ct.ThrowIfCancellationRequested();
                if(file.Recording)return;
                string? thumbnail=await RecordedMediaEngine.Thumbnail(file,Path.Combine(home(),"media-cache"),ct);var info=await RecordedMediaEngine.Probe(file.Path,ct);duration=info.Duration;
                if(card.IsDisposed||ct.IsCancellationRequested)return;
                if(thumbnail!=null){using var source=Image.FromFile(thumbnail);image.Image=new Bitmap(source);}
                details.Text=$"{file.Date:yyyy/MM/dd HH:mm}\n{TimeSpan.FromSeconds(Math.Round(info.Duration)):g} ／ {file.Bytes/1073741824d:F2} GB\n映像 {info.Width} × {info.Height}\n検索ワード：{(file.SearchWord.Length>0?file.SearchWord:"—")}\n{Path.GetExtension(file.Path).TrimStart('.').ToUpperInvariant()}";
            }
            catch(OperationCanceledException){}catch(Exception e)when(e is not OutOfMemoryException){if(!details.IsDisposed)details.Text+="\nプレビュー未取得";}
            finally{if(entered)imageGate.Release();}
        }
    }
    async Task StartBatch()
    {
        if(batch!=null||encoder.SelectedItem is not MediaEncoder chosen)return;
        var inputs=files.Where(f=>selected.Contains(f.Path)&&!f.Recording).Select(f=>f.Path).ToArray();if(inputs.Length==0)return;
        int rate=int.TryParse(bitrate.Text.Split(' ')[0],out var value)?value:0;
        if(MessageBox.Show(this,$"選択した{inputs.Length}件をMP4（AV1）に変換します。\nビットレート：{bitrate.Text}（全件共通）\n元TSは残します。放送字幕・データ放送は変換しません。","バッチ変換",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
        batch=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);convert.Enabled=false;cancel.Enabled=true;encoder.Enabled=bitrate.Enabled=false;playback.Stop();
        try
        {
            jobs.Clear();jobs.AddRange(inputs.Select(p=>new RecordedQueueItem(p,chosen.Codec,rate,"待機","")));SaveQueue();
            for(int i=0;i<inputs.Length;i++)
            {
                int index=i;batch.Token.ThrowIfCancellationRequested();jobs[i]=jobs[i] with{State="変換中"};SaveQueue();progress.Text=$"{i+1}/{inputs.Length}件：{Path.GetFileName(inputs[i])}";
                try
                {
                    if(RecordedMediaEngine.BeingWritten(inputs[i]))throw new IOException("録画中・使用中のファイルです。");
                    string output=await RecordedMediaEngine.Encode(inputs[i],chosen,rate,batch.Token,time=>{if(!IsDisposed)BeginInvoke((Action)(()=>{if(!IsDisposed)progress.Text=$"{index+1}/{inputs.Length}件：{time}";}));});
                    jobs[i]=jobs[i] with{State="完了",Detail=output};
                }
                catch(OperationCanceledException){jobs[i]=jobs[i] with{State="中止"};throw;}
                catch(Exception e)when(e is not OutOfMemoryException){jobs[i]=jobs[i] with{State="失敗",Detail=e.Message};}
                SaveQueue();
            }
            progress.Text=$"変換終了：成功{jobs.Count(j=>j.State=="完了")}件・失敗{jobs.Count(j=>j.State=="失敗")}件";
            var failed=jobs.FirstOrDefault(j=>j.State=="失敗");if(failed!=null)MessageBox.Show(this,failed.Detail,"変換できなかったファイルがあります");
        }
        catch(OperationCanceledException){if(!IsDisposed)progress.Text="変換を中止しました。元TSは残しています。";}
        finally{SaveQueue();batch.Dispose();batch=null;if(!IsDisposed){cancel.Enabled=false;encoder.Enabled=bitrate.Enabled=true;SelectionStatus();await Reload(false);}}
    }
    void SaveQueue(){Directory.CreateDirectory(home());string path=Path.Combine(home(),"media-conversion-queue.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(jobs,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true);}
}
