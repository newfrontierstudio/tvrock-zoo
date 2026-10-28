using System.Text.Json;
using System.Text.RegularExpressions;
namespace TsScheduler;
internal static class BroadcastFilenameOptions
{
 internal static bool Enabled(string home){string p=Path.Combine(home,"filename-remove-broadcast-marks.json");return !File.Exists(p)||JsonSerializer.Deserialize<bool>(File.ReadAllText(p));}
 internal static void Save(string home,bool enabled){Directory.CreateDirectory(home);string p=Path.Combine(home,"filename-remove-broadcast-marks.json");File.WriteAllText(p+".tmp",JsonSerializer.Serialize(enabled));File.Move(p+".tmp",p,true);}
 internal static string Strip(string text)=>Regex.Replace(Regex.Replace(text,@"[\[［【](?:字|字幕|解|手|再|再放送|新|終|デ|二|多|双|S|SS|B|N|映|無|無料|有|吹|生|初|5\.1)[\]］】]", "",RegexOptions.IgnoreCase),"(?:🈑|🈖|🈐|🈞|🈟|🈡|🈓|🈔|🈕|🈚|🈒|🈢)", "").Trim();
 internal static string Apply(string home,string text)=>Enabled(home)?Strip(text):text;
}
