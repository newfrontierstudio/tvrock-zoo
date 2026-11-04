using System.Text.Json;
namespace TsRecommend;
internal sealed class RecommendationOptions
{
 public int? Maximum{get;set;}=20;
 public bool PreventMarkedRepeats{get;set;}=true;
 internal static RecommendationOptions Load(string home){string path=Path.Combine(home,"recommend-options.json");var value=File.Exists(path)?JsonSerializer.Deserialize<RecommendationOptions>(File.ReadAllText(path))??throw new InvalidDataException("おすすめ設定を読めません。"):new();if(value.Maximum is <1 or >100000)throw new InvalidDataException("おすすめの上限設定を確認してください。");return value;}
 internal int Remaining(int alreadyAdded)=>Maximum is int limit?Math.Max(0,limit-Math.Max(0,alreadyAdded)):int.MaxValue;
 internal string Summary=>Maximum is int limit?$"3日ごとに最大{limit}件":"3日ごと・件数の上限なし";
 internal void Save(string home){if(Maximum is <1 or >100000)throw new InvalidDataException("上限は1～100000件、または上限なしを選んでください。");Directory.CreateDirectory(home);string path=Path.Combine(home,"recommend-options.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}
internal sealed record MarkedEpisode(string Path,string Title,string Station,string Description);
internal static class MarkedEpisodes
{
 internal static List<MarkedEpisode> Load(string home){string path=Path.Combine(home,"repeat-blocks.json");return File.Exists(path)?JsonSerializer.Deserialize<List<MarkedEpisode>>(File.ReadAllText(path))??throw new InvalidDataException("再録画防止の履歴を読めません。"):[];}
 internal static string CleanTitle(string title)=>System.Text.RegularExpressions.Regex.Replace(Data.Clean(title),@"\[(?:生|再|字|デ|解|二|多|新|終|無|無料|映|双|S|SS|手|5\.1)\]","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 internal static bool Blocks(IEnumerable<MarkedEpisode> rows,Show show)=>rows.Any(r=>r.Station.Length>0&&Data.StationKey(r.Station)==Data.StationKey(show.Station)&&Planner.SameEpisode(CleanTitle(r.Title),r.Description,CleanTitle(show.Title),show.Description));
 internal static bool Contains(string home,string path)=>Load(home).Any(r=>string.Equals(r.Path,System.IO.Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase));
 internal static void Set(string home,MarkedEpisode row,bool enabled){var rows=Load(home);string full=System.IO.Path.GetFullPath(row.Path);rows.RemoveAll(r=>string.Equals(r.Path,full,StringComparison.OrdinalIgnoreCase));if(enabled)rows.Add(row with{Path=full});Directory.CreateDirectory(home);string path=System.IO.Path.Combine(home,"repeat-blocks.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(rows));File.Move(path+".tmp",path,true);}
}
