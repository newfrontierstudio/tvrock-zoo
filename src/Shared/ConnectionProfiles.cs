using System.Text.Json;
namespace TsRecommend;
internal sealed class ConnectionProfile
{
    public string Name {get;set;}="このPC";
    public string Folder {get;set;}= "";
    public string Url {get;set;}="http://127.0.0.1:8969/user/";
    public string Id {get;set;}="local";
    internal static string? TestRoot;
    public static string Root=>TestRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TVRockZoo");
    public string Home=>Id=="local"?Root:Path.Combine(Root,"connections",Id);
    public Uri BaseUri=>new(Url);
    public bool Allows(Uri uri)=>uri.Scheme==BaseUri.Scheme&&uri.Authority==BaseUri.Authority&&uri.UserInfo.Length==0&&uri.AbsolutePath.StartsWith(BaseUri.AbsolutePath,StringComparison.Ordinal);
    public static string Normalize(string url){var uri=new Uri(url.Trim());var b=new UriBuilder(uri){Query="",Fragment=""};if(new[]{"list","day","now","kwl","kws","reg","cnt"}.Contains(b.Path.Split('/').Last()))b.Path=b.Path[..(b.Path.LastIndexOf('/')+1)];else if(!b.Path.EndsWith('/'))b.Path+="/";return b.Uri.AbsoluteUri;}
    public void Validate(){if(Name.Trim().Length==0)throw new InvalidDataException("接続先の名前を入力してください。");if(!Uri.TryCreate(Url,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||!uri.AbsolutePath.EndsWith('/'))throw new InvalidDataException("TVRockのURLを入力してください。");if(!uri.IsLoopback&&!Folder.StartsWith(@"\\"))throw new InvalidDataException("録画サーバーには共有フォルダー（\\サーバー名\\共有名）を指定してください。");if(!new[]{"dtv.ini","tvrock.sch","tvrock.key"}.All(f=>File.Exists(Path.Combine(Folder,f))))throw new InvalidDataException("dtv.ini・tvrock.sch・tvrock.keyがあるフォルダーを指定してください。");}
}
internal sealed class ConnectionProfiles
{
    public List<ConnectionProfile> Profiles {get;set;}=[new()];
    public string ActiveId {get;set;}="local";
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public static string FilePath=>Path.Combine(ConnectionProfile.Root,"connections.json");
    static ConnectionProfiles? current;
    public static ConnectionProfiles Current=>current??=Read();
    public ConnectionProfile Active=>Profiles.Single(p=>p.Id==ActiveId);
    public static ConnectionProfiles Read(){if(!File.Exists(FilePath)){var result=new ConnectionProfiles();var legacy=Path.Combine(ConnectionProfile.Root,"connection.json");if(File.Exists(legacy)){using var json=JsonDocument.Parse(File.ReadAllText(legacy));result.Profiles[0].Folder=json.RootElement.GetProperty("TvRockFolder").GetString()??"";result.Profiles[0].Url=json.RootElement.GetProperty("TvRockUrl").GetString()??"http://127.0.0.1:8969/user/";}return result;}var value=JsonSerializer.Deserialize<ConnectionProfiles>(File.ReadAllText(FilePath))??throw new InvalidDataException("接続先設定が空です。");if(value.Profiles.Count==0||value.Profiles.Select(p=>p.Id).Distinct().Count()!=value.Profiles.Count||!value.Profiles.Any(p=>p.Id==value.ActiveId)||value.Profiles.Any(p=>!System.Text.RegularExpressions.Regex.IsMatch(p.Id,@"^[a-zA-Z0-9-]{1,50}$")))throw new InvalidDataException("接続先設定を確認してください。");return value;}
    public void Save(){Active.Validate();Directory.CreateDirectory(Active.Home);if(!File.Exists(FilePath)||!Directory.Exists(Path.Combine(Active.Home,"recommend-history")))File.WriteAllText(Path.Combine(Active.Home,"recommend-disabled.txt"),"off");Directory.CreateDirectory(ConnectionProfile.Root);File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(this,Json));File.Move(FilePath+".tmp",FilePath,true);}
}
