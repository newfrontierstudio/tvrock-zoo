using System.Text.RegularExpressions;
using TsRecommend;

namespace TsRecommend;
// Bounded, explainable local scoring. Automatic suggestions are not votes for themselves.
internal sealed class CognitionLearning
{
 readonly List<CognitionBooking> history;readonly DateTimeOffset now;readonly Dictionary<string,(double Factor,string Reason)> evaluated=new();readonly Dictionary<string,string> seriesCache=new();
 string Key(string title){if(!seriesCache.TryGetValue(title,out var key))seriesCache[title]=key=Series(title);return key;}
 internal CognitionLearning(IEnumerable<CognitionBooking> bookings,DateTimeOffset now){this.now=now;history=bookings.Select(b=>b with{}).GroupBy(b=>b.ChannelKey+"|"+b.Start.AddSeconds(-b.StartAdjustment).ToString("O")).Select(g=>g.LastOrDefault(b=>!Cancelled(b)&&b.State is not("Disabled" or "Failed")&&Intentional(b))??g.Last()).ToList();}
 internal static bool CountsAgainstLimit(CognitionBooking b,DateTimeOffset now)=>b.RecommendationKey.Length>0&&!b.MinimumDurationSuppressed&&!b.UserCancelled&&b.State is not("Cancelled" or "Failed" or "Disabled")&&b.RecommendationCreatedAt>now.AddDays(-3);
 internal static string SeriesText(string title){var text=Regex.Replace(title,@"\[[^\]]*\]|［[^］]*］","");text=Regex.Replace(text,@"(?:第\s*\d+\s*[話回]|[#＃]\s*\d+).*$","");return CognitionHistory.TimingSeries(text);}
 internal static string Series(string title)=>Data.Norm(SeriesText(title));
 double Decay(CognitionBooking b,bool negative=false){var stamp=negative?(b.UserCancelledAt??b.ObservedAt??b.RecommendationCreatedAt??b.Start):b.Start;double days=Math.Max(0,(now-stamp).TotalDays);return days>180?0:Math.Pow(.5,days/45);}
 static bool Cancelled(CognitionBooking b)=>b.UserCancelled||b.State=="Cancelled";
 static bool Intentional(CognitionBooking b)=>b.UserSelected||(b.RecommendationKey.Length==0&&(b.RegistrationOrigin=="手動予約"||b.ImportSourceKey.Length>0||b.SearchRuleId.Length>0));
 double Positive(CognitionBooking b)=>!Cancelled(b)&&b.State is not("Disabled" or "Failed")&&(b.UserSelected||b.RecommendationKey.Length==0)?Decay(b)*(Intentional(b)?1:0.65):0;
 double Negative(CognitionBooking b){
  if(b.CancellationContext=="stop-recording")return 0; // Finishing a recording early is not a dislike.
  bool competing=history.Any(other=>other.Id!=b.Id&&Intentional(other)&&Positive(other)>0&&CognitionHistory.Overlap(b.Start,b.End,other.Start,other.End));
  return Decay(b,true)*(competing?.35:1);
 }
 internal List<Taste> Tastes()=>history.Where(b=>Positive(b)>0&&Key(b.Title).Length>=2).GroupBy(b=>Key(b.Title)).Select(g=>new Taste(SeriesText(g.First().Title),Math.Min(2.1,1.05+.25*Math.Log2(1+g.Sum(Positive))))).ToList();
 internal (double Factor,string Reason) Evaluate(Show show){string series=Key(show.Title);if(evaluated.TryGetValue(series,out var cached))return cached;var same=history.Where(b=>series.Length>=2&&Key(b.Title)==series).ToArray();double yes=same.Sum(Positive),no=same.Where(Cancelled).Sum(Negative);
  // Prior support 3/4 makes one cancellation a soft reduction, never a blacklist.
  double support=(3+yes)/(4+yes+no),confidence=Math.Min(1,(yes+no)/5),factor=Math.Clamp((1-confidence)+confidence*(support/.75),.45,1.3);
  if(same.Length==0)return evaluated[series]=(1,"");return evaluated[series]=(factor,$" 同番組の予約傾向 {yes:0.0}・取消 {no:0.0}（直近重視）、履歴係数 {factor:0.00}。");
 }
}
