using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
namespace TsScheduler;

// mpv uses FFmpeg for decoding. Its video child is parented to the preview panel.
internal sealed class RecordedMediaPlayback:IDisposable
{
    Process? child;string pipe="";
    internal int? Pid=>child is {HasExited:false}?child.Id:null;
    internal void Start(string file,Control host)
    {
        Stop();pipe="ZooMedia-"+Guid.NewGuid().ToString("N");
        var start=new ProcessStartInfo(RecordedMediaEngine.Tool("mpv")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(RecordedMediaEngine.Tool("mpv"))!};
        foreach(string arg in new[]{"--no-config","--load-scripts=no","--input-default-bindings=no","--input-vo-keyboard=no","--osc=no","--osd-level=0","--keep-open=yes","--force-window=yes","--pause=yes","--hwdec=auto-safe",Environment.GetEnvironmentVariable("STUDIO_MEDIA_TEST_MUTE")=="1"?"--mute=yes":"--mute=no","--volume=70","--wid="+host.Handle.ToInt64(),@"--input-ipc-server=\\.\pipe\"+pipe,"--",file})start.ArgumentList.Add(arg);
        child=Process.Start(start)??throw new IOException("プレビューを開始できません。");
    }
    internal async Task<bool> WaitForVideo(int expectedPid,CancellationToken ct)
    {
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed.TotalSeconds<12&&Pid==expectedPid)
        {
            ct.ThrowIfCancellationRequested();
            var configured=await Command("get_property","vo-configured");
            var parameters=await Command("get_property","video-out-params");
            if(Pid!=expectedPid)return false;
            if(configured is {} c&&c.TryGetProperty("data",out var ready)&&ready.ValueKind==JsonValueKind.True&&parameters is {} p&&p.TryGetProperty("data",out var data)&&data.ValueKind==JsonValueKind.Object)return true;
            await Task.Delay(40,ct);
        }
        return false;
    }
    internal async Task<JsonElement?> Command(params object[] command)
    {
        if(Pid==null)return null;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            using var channel=new NamedPipeClientStream(".",pipe,PipeDirection.InOut,PipeOptions.Asynchronous);await channel.ConnectAsync(timeout.Token);
            using var writer=new StreamWriter(channel,new System.Text.UTF8Encoding(false),1024,true){AutoFlush=true};using var reader=new StreamReader(channel,System.Text.Encoding.UTF8,false,1024,true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new{command,request_id=7}));
            while(await reader.ReadLineAsync(timeout.Token) is {} line){using var doc=JsonDocument.Parse(line);if(doc.RootElement.TryGetProperty("request_id",out var id)&&id.GetInt32()==7)return doc.RootElement.Clone();}
        }
        catch(Exception error)when(error is IOException or OperationCanceledException or JsonException){}
        return null;
    }
    internal void Stop()
    {
        if(child==null)return;
        try{if(!child.HasExited)child.Kill(true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{child.Dispose();child=null;}
    }
    public void Dispose()=>Stop();
}
