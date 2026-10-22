using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static string BookingSearchWord(Booking booking,AiEntry? ai,IReadOnlyList<KeywordEntry> keywords)
    {
        if(!string.IsNullOrWhiteSpace(ai?.SearchWord))return ai.SearchWord;
        string saved=booking.Fields.GetValueOrDefault("SEARCHWORD","");
        if(!string.IsNullOrWhiteSpace(saved))return saved;
        string key=booking.Fields.GetValueOrDefault("SEARCHID","");
        var keyword=key.Length==0?null:keywords.FirstOrDefault(k=>k.Id==key);
        if(keyword!=null)return string.IsNullOrWhiteSpace(keyword.Title)?keyword.Detail:keyword.Title;
        return key.Length>0&&key!="0"?"不明（元の検索条件なし）":"なし（"+booking.Fields.GetValueOrDefault("BOOKINGORIGIN","手動予約")+"）";
    }
    string GuideSearchWord(Show show)
    {
        var words=new List<string>();
        foreach(var booking in _nativeBookings.Where(b=>b.Enabled&&Planner.SameBroadcast(show,b)))
        {
            var ai=_nativeAi.FirstOrDefault(a=>IsAiBooking(booking,a));
            string word=BookingSearchWord(booking,ai,_nativeKeys);
            if(!string.IsNullOrWhiteSpace(word))words.Add(word.Trim());
        }
        return words.Count==0?"":"検索ワード："+string.Join(" ／ ",words.Distinct(StringComparer.OrdinalIgnoreCase));
    }
    string GuideSearchWordHover(Show show){string text=GuideSearchWord(show);return text.Length==0?"":"\n\n"+text;}
    string GuidePopupWithSearchWord(Show show){string text=GuideSearchWord(show);return text.Length==0?GuidePopupText(show):text+"\n\n"+GuidePopupText(show);}
}
