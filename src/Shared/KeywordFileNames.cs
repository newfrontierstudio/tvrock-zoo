using System.Text.Json;
namespace TsRecommend;
internal static class KeywordFileNames
{
    internal static bool Enabled(string home)=>File.Exists(Path.Combine(home,"filename-search-word.on"));
    internal static void Save(string home,bool enabled){Directory.CreateDirectory(home);string path=Path.Combine(home,"filename-search-word.on");if(enabled)File.WriteAllText(path,"on");else if(File.Exists(path))File.Delete(path);}
    internal static string Clean(string word,int limit=60){string value=string.Concat(word.Trim().Select(c=>char.IsControl(c)||Path.GetInvalidFileNameChars().Contains(c)?'_':c)).Trim(' ','.');if(value.Length>limit)value=value[..limit];if(value.Length>0&&char.IsHighSurrogate(value[^1]))value=value[..^1];return value;}
    internal static string Append(string path,string word){string clean=Clean(word);if(clean.Length==0)return path;string ext=Path.GetExtension(path),stem=Path.GetFileNameWithoutExtension(path),suffix="_"+clean;if(stem.EndsWith(suffix,StringComparison.Ordinal))return path;return Path.Combine(Path.GetDirectoryName(path)??"",stem+suffix+ext);}
}
