using System.Text.RegularExpressions;
namespace TsRecommend;

// TVRock remains the owner of explicit search reservations. Satlite only fills
// the remainder; it never rewrites TVRock's schedule or search configuration.
internal static class UserRulePriority
{
 internal static bool HasSearchOrigin(Booking booking)=>long.TryParse(booking.Fields.GetValueOrDefault("SEARCHID"),out var id)&&id!=0;
 internal static string Plain(string text)=>Data.Clean(text).Replace('〜','~');
 internal sealed record Rule(int Order,Dictionary<string,string> Fields)
 {
  internal string Title=>Fields.GetValueOrDefault("TWORD","");
  internal bool Matches(Show show)
  {
   if(Fields.GetValueOrDefault("VALIDATE")!="1"||Fields.GetValueOrDefault("ONESEG")=="1")return false;
   var stations=Fields.Where(p=>Regex.IsMatch(p.Key,@"^STATION\d+$")&&p.Value.Length>0).Select(p=>p.Value).ToArray();
   if(stations.Length>0&&!stations.Any(s=>Data.SameStation(s,show.Station)))return false;
   var local=show.Start.LocalDateTime;int.TryParse(Fields.GetValueOrDefault("WEEK"),out int week);
   if(week!=0&&(week&(1<<(int)local.DayOfWeek))==0)return false;
   if(Fields.GetValueOrDefault("VALIDST")=="1"){
    int.TryParse(Fields.GetValueOrDefault("STHOUR"),out int hour);int.TryParse(Fields.GetValueOrDefault("DRHOUR"),out int duration);
    double offset=(local.TimeOfDay.TotalHours-hour+24)%24;if(duration<=0||offset>=duration)return false;
   }
   // The guide adapter has no reliable genre/age/expiry data. Do not claim
   // that a constrained TVRock rule owns a show when those cannot be checked.
   if(Fields.GetValueOrDefault("VALIDDW")=="1"||Fields.GetValueOrDefault("EXPIRE") is not (null or "0"))return false;
   if(Fields.Any(p=>Regex.IsMatch(p.Key,@"^(JUNLE|BC)\d+$")&&p.Value=="0"))return false;
   try{return Query(show.Title,Title,Fields)&&Query(show.Description,Fields.GetValueOrDefault("DWORD",""),Fields);}
   catch(ArgumentException){return false;}catch(RegexMatchTimeoutException){return false;}
  }
 }
 internal static bool Query(string text,string query,IReadOnlyDictionary<string,string> fields)
 {
  if(string.IsNullOrWhiteSpace(query))return true;
  // Preserve regular-expression syntax; normalize only literal search terms.
  if(fields.GetValueOrDefault("FUZZY")=="0")return Regex.IsMatch(Data.Clean(text),query,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(50));
  var parts=Regex.Split(query,@"\s+not\s+",RegexOptions.IgnoreCase);var terms=Plain(parts[0]).Split(' ',StringSplitOptions.RemoveEmptyEntries);
  string haystack=Plain(text);bool positive=fields.GetValueOrDefault("MATCHOR")=="1"?terms.Any(t=>haystack.Contains(t,StringComparison.OrdinalIgnoreCase)):terms.All(t=>haystack.Contains(t,StringComparison.OrdinalIgnoreCase));
  return positive&&(parts.Length<2||!Regex.IsMatch(haystack,parts[1],RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(50)));
 }
 internal static List<Rule> Read(string path)
 {
  var result=new List<Rule>();if(!File.Exists(path))return result;
  Dictionary<string,string> fields=new(StringComparer.OrdinalIgnoreCase);int order=0;
  void Add(){if(fields.ContainsKey("SEARCHID")&&fields.GetValueOrDefault("VALIDATE")=="1")result.Add(new(order,fields));}
  foreach(string raw in File.ReadLines(path,Data.Cp932)){
   string line=raw.TrimEnd('\r');if(Regex.IsMatch(line,@"^\[KEYWORD\d+\]$")){Add();fields=new(StringComparer.OrdinalIgnoreCase);order++;}
   else{int at=line.IndexOf('=');if(at>0)fields[line[..at]]=line[(at+1)..];}
  }Add();return result;
 }
}
