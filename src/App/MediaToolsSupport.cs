using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace TsScheduler;

// Media executables are installed once, separately from either application.
internal static class MediaToolsSupport
{
    static readonly string[] Executables=["ffmpeg.exe","ffprobe.exe","mpv.exe","d3dcompiler_43.dll"];
    internal static string? TestHome;
    static string Home=>TestHome??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZooMediaTools");
    static string Pointer=>Path.Combine(Home,"active.txt");
    static string? Folder
    {
        get{
            var bundled=Path.Combine(AppContext.BaseDirectory,"MediaTools");
            if(Executables.All(n=>File.Exists(Path.Combine(bundled,n))))return bundled;
            try{if(File.Exists(Pointer)){var folder=File.ReadAllText(Pointer).Trim();if(Path.IsPathFullyQualified(folder)&&Executables.All(n=>File.Exists(Path.Combine(folder,n))))return folder;}}catch(IOException){}catch(UnauthorizedAccessException){}
            return null;
        }
    }
    internal static bool Ready=>Folder!=null;
    internal static string Tool(string name)
    {
        if(name is not ("ffmpeg" or "ffprobe" or "mpv"))throw new ArgumentException("Unknown media tool",nameof(name));
        var folder=Folder??throw new FileNotFoundException("動画追加パックがありません。録画済みの「再生・変換の部品を追加」から設定してください。");
        return Path.Combine(folder,name+".exe");
    }
    internal static string Install(string source)
    {
        source=Path.GetFullPath(source);
        var manifest=Path.Combine(source,"SHA256.csv");
        if(!File.Exists(manifest)||!File.Exists(Path.Combine(source,"SOURCES.txt"))||!Directory.Exists(Path.Combine(source,"licenses")))throw new InvalidDataException("Toolsフォルダーを選んでください。検証情報またはライセンスがありません。");
        var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in File.ReadLines(manifest)){
            var match=Regex.Match(line.TrimStart('\uFEFF'),"^\"([^\"]+)\",\"([A-Fa-f0-9]{64})\"$");
            if(match.Success)hashes.Add(match.Groups[1].Value,match.Groups[2].Value);
        }
        var names=Executables.Append("SOURCES.txt").ToArray();
        foreach(var name in names){
            var file=Path.Combine(source,name);
            if(!hashes.TryGetValue(name,out var expected)||!File.Exists(file))throw new InvalidDataException("追加パックのファイルが不足しています："+name);
            using var stream=File.OpenRead(file);
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("追加パックの検証に失敗しました："+name);
        }
        foreach(var name in new[]{"FFmpeg-GPLv3.txt","FFprobe-GPLv3.txt","mpv-GPL.txt"})if(!File.Exists(Path.Combine(source,"licenses",name)))throw new InvalidDataException("ライセンスが不足しています："+name);
        var identity=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest)))[..16];
        var destination=Path.Combine(Home,"packs",identity);
        Directory.CreateDirectory(destination);
        foreach(var name in names.Append("SHA256.csv")){
            var target=Path.Combine(destination,name);
            if(string.Equals(Path.GetFullPath(target),Path.GetFullPath(Path.Combine(source,name)),StringComparison.OrdinalIgnoreCase))continue;
            if(File.Exists(target)&&hashes.TryGetValue(name,out var expected)){
                using var stream=File.OpenRead(target);
                if(Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase))continue;
            }
            File.Copy(Path.Combine(source,name),target,true);
        }
        Directory.CreateDirectory(Path.Combine(destination,"licenses"));
        foreach(var file in Directory.EnumerateFiles(Path.Combine(source,"licenses"),"*.txt")){var target=Path.Combine(destination,"licenses",Path.GetFileName(file));if(!string.Equals(Path.GetFullPath(file),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))File.Copy(file,target,true);}
        var temporary=Pointer+"."+Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary,destination);
        File.Move(temporary,Pointer,true);
        return destination;
    }
}
