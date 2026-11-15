using System.Text.Json;
using ZooStorage;
namespace TsRecommend;

// Explicit rejection of a Satlite pick is a decision, not a taste sample.
// When the user cancels a queued recommendation booking, or marks it
// "録画しない", before it starts, that series is not offered again until
// the user books the same series themselves. Taste learning
// (RecommendationLearning / quantum cognition) is deliberately untouched:
// this store only gates re-offering, and RecommendAsync syncs it from the
// booking history on every run, so rejections made while the picker was
// not running are picked up too.
internal sealed class SatliteRejections
{
    internal sealed class Entry
    {
        public string Series{get;set;}="";
        public string Title{get;set;}="";
        public DateTimeOffset RejectedAt{get;set;}
        public DateTimeOffset? ReleasedAt{get;set;}
        public Dictionary<string,DateTimeOffset> Seen{get;set;}=[];
    }

    readonly string path;readonly List<Entry> entries;
    internal SatliteRejections(string home){path=Path.Combine(home,"satlite-rejections.json");entries=Load();}
    internal int Count=>entries.Count(e=>e.ReleasedAt==null||e.RejectedAt>e.ReleasedAt);
    List<Entry> Load(){try{return File.Exists(path)?JsonSerializer.Deserialize<List<Entry>>(DurableJson.Read(path))??[]:[];}catch{return[];}}
    void Save(){try{DurableJson.Write(path,JsonSerializer.Serialize(entries));}catch{}}

    // A rejection is: a Satlite booking (carries a recommendation key) that
    // the user cancelled while it was still queued, or disabled through
    // "録画しない". Stopping a recording already in progress is about that
    // broadcast, not the series, and never counts here; the minimum-duration
    // system suppression is not a user act either.
    internal static bool IsRejection(CognitionBooking b)
    {
        if(b.RecommendationKey.Length==0||!b.UserCancelled||b.CancellationContext=="stop-recording")return false;
        if(b.State=="Cancelled")return b.UserCancelledAt is {} action&&action<b.Start;
        return b.State=="Disabled"&&!b.MinimumDurationSuppressed;
    }

    internal void Sync(IReadOnlyList<CognitionBooking> bookings,DateTimeOffset now,bool persist=true)
    {
        bool changed=false;
        foreach(var b in bookings)
        {
            if(!IsRejection(b))continue;
            string series=CognitionLearning.Series(b.Title);
            if(series.Length<2)continue;
            var entry=entries.FirstOrDefault(e=>e.Series==series);
            if(entry==null){entry=new(){Series=series,Title=b.Title,RejectedAt=b.UserCancelledAt??now};entries.Add(entry);changed=true;}
            string eventKey=b.Id+":"+(b.UserCancelledAt?.ToString("O")??"legacy");
            if(entry.Seen.ContainsKey(eventKey))continue;
            var stamp=b.UserCancelledAt??now;entry.Seen[eventKey]=stamp;if(stamp>entry.RejectedAt)entry.RejectedAt=stamp;changed=true;
        }
        // The user booking the same series themselves is the release: an
        // intentional, non-recommendation booking made after the rejection.
        foreach(var b in bookings)
        {
            if(b.RecommendationKey.Length>0||b.UserCancelled||b.State is "Cancelled" or "Disabled" or "Failed")continue;
            if(!b.UserSelected&&b.RegistrationOrigin!="手動予約")continue;
            string series=CognitionLearning.Series(b.Title);
            var entry=entries.FirstOrDefault(e=>e.Series==series);
            if(entry!=null&&b.UserActionAt is {} action&&action>entry.RejectedAt&&(entry.ReleasedAt==null||action>entry.ReleasedAt)){entry.ReleasedAt=action;changed=true;}
        }
        if(changed&&persist)Save();
    }

    internal bool Suppresses(string title)=>entries.Any(e=>e.Series==CognitionLearning.Series(title)&&(e.ReleasedAt==null||e.RejectedAt>e.ReleasedAt));
}
