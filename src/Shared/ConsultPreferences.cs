using System.Text.Json;
namespace TsRecommend;
internal sealed class ConsultPreferences
{
    public string[] Include { get; set; } = [];
    public string[] Exclude { get; set; } = [];
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive=true, WriteIndented=true, PropertyNamingPolicy=JsonNamingPolicy.CamelCase };
    public static ConsultPreferences Parse(string text)
    {
        text=text.Trim();if(text.StartsWith("```")){int first=text.IndexOf('\n');int last=text.LastIndexOf("```",StringComparison.Ordinal);if(first<0||last<=first)throw new FormatException("回答全体をコピーしてください。");text=text[(first+1)..last].Trim();}
        if(text.Length>16000)throw new FormatException("回答が長すぎます。");
        using var doc=JsonDocument.Parse(text);
        if(doc.RootElement.ValueKind!=JsonValueKind.Object||!doc.RootElement.TryGetProperty("include",out _)||!doc.RootElement.TryGetProperty("exclude",out _)||doc.RootElement.EnumerateObject().Any(p=>p.Name is not ("include" or "exclude")))throw new FormatException("Zoo用の回答ではありません。「好みを相談」からやり直してください。");
        var value=JsonSerializer.Deserialize<ConsultPreferences>(text,Options)??throw new FormatException("回答が空です。");
        string[] Clean(string[]? words){if(words==null||words.Length>30||words.Any(w=>w==null||w.Length>100||w.Any(char.IsControl)))throw new FormatException("条件は各30個・1項目100文字以内です。");return words.Select(w=>w.Trim()).Where(w=>w.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
        value.Include=Clean(value.Include);value.Exclude=Clean(value.Exclude);if(value.Include.Length==0)throw new FormatException("好きな内容が空です。");return value;
    }
    public static ConsultPreferences Load(string home){var path=Path.Combine(home,"consult-preferences.json");return File.Exists(path)?Parse(File.ReadAllText(path)):new();}
    public void Save(string home){Directory.CreateDirectory(home);string path=Path.Combine(home,"consult-preferences.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this,Options));File.Move(path+".tmp",path,true);}
    public bool Allows(Show show)=>!Exclude.Any(word=>(show.Title+" "+show.Description).Contains(word,StringComparison.OrdinalIgnoreCase));
    public List<Taste> AddTo(List<Taste> tastes)=>tastes.Concat(Include.Select(word=>new Taste(word,1.5))).ToList();
}
