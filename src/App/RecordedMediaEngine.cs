using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TsScheduler;

internal sealed record RecordedFile(string Path,string Name,DateTime Date,long Bytes,bool Recording){internal string Station{get;init;}="";internal string SearchWord{get;init;}="";}
internal sealed record MediaInfo(int Width,int Height,string Fps,string FieldOrder,double Duration);
internal sealed record MediaEncoder(string Label,string Codec){public override string ToString()=>Label;}
internal static class RecordedMediaEngine
{
    internal static string Tool(string name)
    {
        string path=System.IO.Path.Combine(AppContext.BaseDirectory,"MediaTools",name+".exe");
        if(!File.Exists(path))throw new FileNotFoundException("再生・変換用の部品がありません。インストーラーで上書きインストールしてください。",path);
        return path;
    }
    internal static bool BeingWritten(string path)
    {
        try{using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return false;}
        catch(IOException){return true;}catch(UnauthorizedAccessException){return true;}
    }
    internal static List<RecordedFile> Read(string folder)
    {
        if(!Directory.Exists(folder))return [];
        return Directory.EnumerateFiles(folder).Where(p=>Path.GetExtension(p).Equals(".ts",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(p).Equals(".mp4",StringComparison.OrdinalIgnoreCase))
            .Where(p=>!Path.GetFileName(p).StartsWith(".zoo-av1-",StringComparison.OrdinalIgnoreCase))
            .Select(p=>{try{var f=new FileInfo(p);string name=Path.GetFileNameWithoutExtension(f.Name);string normalized=name.Normalize(System.Text.NormalizationForm.FormKC);if(Path.GetExtension(p).Equals(".mp4",StringComparison.OrdinalIgnoreCase))normalized=Regex.Replace(normalized,@"_AV1(?:_\d+)?$","");var station=Regex.Match(normalized,@"_(?<station>NHK[^_]*|TBS[1-3]?|日テレ[1-3]?|テレビ朝日[1-3]?|テレ朝|テレビ東京[1-3]?|テレ東|フジテレビ[1-3]?|フジ|Eテレ[^_]*|MX[1-3]?)(?:_(?<word>.*))?$");return new RecordedFile(f.FullName,station.Success?normalized[..station.Index]:name,f.LastWriteTime,f.Length,BeingWritten(p)){Station=station.Groups["station"].Value,SearchWord=station.Groups["word"].Value};}catch(IOException){return null;}}).OfType<RecordedFile>().ToList();
    }
    internal static List<RecordedFile> Sort(IEnumerable<RecordedFile> files,string key,bool descending)
    {
        IOrderedEnumerable<RecordedFile> sorted=key switch{"名前"=>descending?files.OrderByDescending(x=>x.Name,StringComparer.CurrentCultureIgnoreCase):files.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase),"サイズ"=>descending?files.OrderByDescending(x=>x.Bytes):files.OrderBy(x=>x.Bytes),_=>descending?files.OrderByDescending(x=>x.Date):files.OrderBy(x=>x.Date)};
        return sorted.ThenBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToList();
    }
    internal static string CacheKey(RecordedFile file)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(file.Path+"|"+file.Bytes+"|"+file.Date.Ticks)));
    internal static async Task<(int Code,string Output,string Error)> Run(string tool,IEnumerable<string> args,CancellationToken ct,Action<string>? progress=null)
    {
        var info=new ProcessStartInfo(Tool(tool)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
        foreach(string arg in args)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)??throw new IOException("メディア処理を開始できません。");
        try{process.PriorityClass=ProcessPriorityClass.BelowNormal;}catch(InvalidOperationException){}
        using var registration=ct.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}});
        var errors=process.StandardError.ReadToEndAsync();var output=new System.Text.StringBuilder();
        while(await process.StandardOutput.ReadLineAsync() is {} line){if(progress!=null)progress(line);else output.AppendLine(line);}
        await process.WaitForExitAsync(CancellationToken.None);string error=await errors;ct.ThrowIfCancellationRequested();return(process.ExitCode,output.ToString(),error.Length>5000?error[^5000..]:error);
    }
    internal static async Task<MediaInfo> Probe(string input,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var result=await Run("ffprobe",["-v","error","-select_streams","v:0","-show_entries","stream=width,height,avg_frame_rate,r_frame_rate,field_order:format=duration","-of","json",input],timeout.Token);
        if(result.Code!=0)throw new IOException("映像情報を取得できません。"+result.Error);
        using var doc=JsonDocument.Parse(result.Output);var streams=doc.RootElement.GetProperty("streams");if(streams.GetArrayLength()==0)throw new IOException("映像がありません。");var video=streams[0];
        string Text(string key)=>video.TryGetProperty(key,out var value)?value.ToString():"";
        double seconds=doc.RootElement.TryGetProperty("format",out var f)&&f.TryGetProperty("duration",out var d)&&double.TryParse(d.ToString(),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out double duration)?duration:0;
        return new(video.GetProperty("width").GetInt32(),video.GetProperty("height").GetInt32(),Text("avg_frame_rate"),Text("field_order"),seconds);
    }
    internal static async Task<string?> Thumbnail(RecordedFile file,string cache,CancellationToken ct)
    {
        if(file.Recording||file.Bytes==0)return null;Directory.CreateDirectory(cache);string target=Path.Combine(cache,CacheKey(file)+".jpg");if(File.Exists(target))return target;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        string temp=target+".tmp.jpg";
        try{var result=await Run("ffmpeg",["-hide_banner","-loglevel","error","-nostdin","-y","-ss","1","-i",file.Path,"-map","0:v:0","-frames:v","1","-an","-vf","bwdif=mode=send_frame,scale=320:180:force_original_aspect_ratio=decrease","-update","1",temp],timeout.Token);if(result.Code!=0||!File.Exists(temp))return null;File.Move(temp,target,true);return target;}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static async Task<List<MediaEncoder>> Encoders(CancellationToken ct)
    {
        var result=new List<MediaEncoder>();string hardware="";
        try
        {
            var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"-NoProfile","-NonInteractive","-Command","Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name"})info.ArgumentList.Add(arg);
            using var p=Process.Start(info)!;var reading=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(10),ct);hardware=await reading;await error;
        }catch(Exception e)when(e is IOException or System.ComponentModel.Win32Exception or TimeoutException){}
        foreach(string name in hardware.Split('\n',StringSplitOptions.RemoveEmptyEntries))
        {
            if(name.Contains("NVIDIA",StringComparison.OrdinalIgnoreCase))result.Add(new(name.Contains("Blackwell",StringComparison.OrdinalIgnoreCase)||Regex.IsMatch(name,@"RTX\s*50\d\d",RegexOptions.IgnoreCase)?"NVENC BLACKWELL":"NVIDIA NVENC","av1_nvenc"));
            else if(name.Contains("AMD",StringComparison.OrdinalIgnoreCase)||name.Contains("Radeon",StringComparison.OrdinalIgnoreCase))result.Add(new(Regex.IsMatch(name,@"RX\s*9\d{3}",RegexOptions.IgnoreCase)?"AMD AMF RDNA 4":Regex.IsMatch(name,@"RX\s*7\d{3}",RegexOptions.IgnoreCase)?"AMD AMF RDNA 3":"AMD AMF","av1_amf"));
            else if(name.Contains("Intel",StringComparison.OrdinalIgnoreCase))result.Add(new(Regex.IsMatch(name,@"Arc.*B\d{3}",RegexOptions.IgnoreCase)?"Intel QSV Battlemage":Regex.IsMatch(name,@"Arc.*A\d{3}",RegexOptions.IgnoreCase)?"Intel QSV Alchemist":"Intel QSV","av1_qsv"));
        }
        var supported=new List<MediaEncoder>();
        foreach(var candidate in result.DistinctBy(e=>e.Codec))
        {
            using var checkTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct);checkTimeout.CancelAfter(TimeSpan.FromSeconds(8));
            try{var check=await Run("ffmpeg",["-v","error","-f","lavfi","-i","color=size=320x180:rate=1","-frames:v","1","-an","-c:v",candidate.Codec,"-f","null","-"],checkTimeout.Token);if(check.Code==0)supported.Add(candidate);}catch(OperationCanceledException)when(!ct.IsCancellationRequested){}
        }
        supported.Add(new("CPU（AV1）","libsvtav1"));return supported;
    }
    internal static string OutputPath(string input)
    {
        string stem=Path.Combine(Path.GetDirectoryName(input)!,Path.GetFileNameWithoutExtension(input)+"_AV1");string output=stem+".mp4";int n=1;while(File.Exists(output))output=stem+"_"+(n++)+".mp4";return output;
    }
    internal static List<string> EncodeArguments(string input,string temp,MediaInfo media,MediaEncoder encoder,int bitrate)
    {
        var args=new List<string>{"-hide_banner","-loglevel","warning","-nostdin","-n","-i",input,"-map","0:v:0","-map","0:a?","-map_metadata","0","-map_chapters","0","-sn","-dn"};
        if(media.FieldOrder is "tt" or "bb" or "tb" or "bt")args.AddRange(["-vf","bwdif=mode=send_frame:parity=auto:deint=all"]);
        args.AddRange(["-fps_mode","passthrough","-c:v",encoder.Codec]);
        if(bitrate>0)args.AddRange(["-b:v",bitrate+"M"]);
        else if(encoder.Codec=="av1_nvenc")args.AddRange(["-rc","vbr","-cq","32","-b:v","0"]);
        else if(encoder.Codec=="libsvtav1")args.AddRange(["-crf","32"]);
        else args.AddRange(["-b:v","6M"]);
        if(encoder.Codec=="libsvtav1")args.AddRange(["-preset","8","-threads","2"]);
        args.AddRange(["-c:a","aac","-b:a","192k","-movflags","+faststart","-progress","pipe:1","-nostats",temp]);return args;
    }
    internal static async Task<string> Encode(string input,MediaEncoder encoder,int bitrate,CancellationToken ct,Action<string> progress)
    {
        using var source=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read);
        var meta=await Probe(input,ct);if(meta.Width%2!=0||meta.Height%2!=0)throw new IOException("元サイズを維持するため、奇数サイズの映像は変換しません。");
        string output=OutputPath(input),temp=Path.Combine(Path.GetDirectoryName(input)!,".zoo-av1-"+Guid.NewGuid().ToString("N")+".mp4");
        try
        {
            var result=await Run("ffmpeg",EncodeArguments(input,temp,meta,encoder,bitrate),ct,line=>{if(line.StartsWith("out_time="))progress(line[9..]);});
            if(result.Code!=0)throw new IOException("AV1変換に失敗しました。対応GPU・ドライバーをご確認ください。"+result.Error);
            var check=await Probe(temp,ct);
            if(check.Width!=meta.Width||check.Height!=meta.Height||Math.Abs(Rate(check.Fps)-Rate(meta.Fps))>.02||meta.Duration>0&&Math.Abs(check.Duration-meta.Duration)>Math.Max(3,meta.Duration*.005))throw new IOException("変換後のサイズ・長さ・FPSが元データと一致しません。元TSは残しています。");
            File.Move(temp,output,false);return output;
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static double Rate(string value){var parts=value.Split('/');return double.TryParse(parts[0],System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out double n)&& (parts.Length==1||double.TryParse(parts[1],out double d)&&d!=0)?parts.Length==1?n:n/double.Parse(parts[1],System.Globalization.CultureInfo.InvariantCulture):0;}
}
