using System.Text.Json;
namespace TsScheduler;
internal static class DiagnosticLogging
{
    static readonly object Gate=new();
    internal static (string Product,string Version) AppIdentity()
    {
        var assembly=System.Reflection.Assembly.GetEntryAssembly()??typeof(DiagnosticLogging).Assembly;
        string product=assembly.GetName().Name=="ZOOstudio"?"ZOOstudio Pre":"TVRock Zoo";
        string version=assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0]??assembly.GetName().Version?.ToString()??"0.0.0";
        return(product,version);
    }
    internal static bool Enabled(string home){try{return File.Exists(Path.Combine(home,"diagnostic-logging.txt"))&&File.ReadAllText(Path.Combine(home,"diagnostic-logging.txt")).Trim()=="on";}catch{return false;}}
    internal static void SetEnabled(string home,bool enabled)
    {
        Directory.CreateDirectory(home);string path=Path.Combine(home,"diagnostic-logging.txt");File.WriteAllText(path+".tmp",enabled?"on":"off");File.Move(path+".tmp",path,true);
        Write(home,"logging.changed",false,enabled?"on":"off");
    }
    internal static void Write(string home,string operation,bool error=false,string status="",int count=0,long milliseconds=0)
    {
        // Accept only short event identifiers and status codes, never raw titles, URLs, paths or exception messages.
        string Safe(string value)=>value.Length<=80&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '_' or '-')?value:"redacted";
        try
        {
            if(!error&&!Enabled(home))return;
            lock(Gate){Directory.CreateDirectory(home);string path=Path.Combine(home,error?"errors.jsonl":"diagnostic.jsonl");if(File.Exists(path)&&new FileInfo(path).Length>=1024*1024)File.Move(path,path+".previous",true);var identity=AppIdentity();File.AppendAllText(path,JsonSerializer.Serialize(new{Product=identity.Product,Version=identity.Version,Time=DateTimeOffset.Now,Event=Safe(operation),Status=Safe(status),Count=count,Milliseconds=milliseconds})+Environment.NewLine);}
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
    internal static async Task<string> Request(string home,string relative,Func<Task<string>> send)
    {
        string route=relative.Split('?')[0];if(!new[]{"list","day","now","kwl","kws","reg","regsch","cnt"}.Contains(route))route="other";
        var watch=System.Diagnostics.Stopwatch.StartNew();try{string result=await send();Write(home,"tvrock."+route,status:"ok",count:result.Length,milliseconds:watch.ElapsedMilliseconds);return result;}catch(Exception error){Write(home,"tvrock."+route,true,error.GetType().Name,milliseconds:watch.ElapsedMilliseconds);throw;}
    }
}
internal sealed partial class MainForm
{
    void InitializeDiagnosticLogging(FlowLayoutPanel body)
    {
        var toggle=new CheckBox{Text="診断ログ OFF",AutoSize=true,Checked=DiagnosticLogging.Enabled(RecommendationHome),Margin=new Padding(0,8,0,4)};toggle.Text=toggle.Checked?"診断ログ ON":"診断ログ OFF";body.Controls.Add(toggle);
        body.Controls.Add(new Label{Text="不具合調査用。OFFでもエラーと録画結果は残します。",AutoSize=true,Margin=new Padding(0,0,0,10)});

        InitializeProblemReporting(body.Controls);
        bool reverting=false;toggle.CheckedChanged+=(_,_)=>{if(reverting)return;try{DiagnosticLogging.SetEnabled(RecommendationHome,toggle.Checked);toggle.Text=toggle.Checked?"診断ログ ON":"診断ログ OFF";}catch(Exception e){reverting=true;toggle.Checked=!toggle.Checked;reverting=false;MessageBox.Show(this,e.Message,"ログ設定を保存できません");}};
    }
}
