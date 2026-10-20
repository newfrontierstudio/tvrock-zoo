using System.Text.RegularExpressions;
namespace TsRecommend;
internal static class RerunPreference
{
    internal static bool IsRerun(string title)=>Regex.IsMatch(Data.Clean(title),@"\[(?:再|再放送)\]|【(?:再|再放送)】|(?:^|\s)再放送(?:\s|$|[:：])");
    internal static (double Score,string Word,string Reason) Apply(Show show,double score,string word,string reason)
    {
        if(!IsRerun(show.Title))return(score,word,reason);
        string preference=Data.Norm(word);bool explicitMatch=preference.Length>=4&&Data.Norm(show.Title).Contains(preference,StringComparison.OrdinalIgnoreCase);
        bool strong=score>=.65||(explicitMatch&&score>=.45);
        if(!strong)return(0,word,reason+" 再放送は好みとの強い一致がないため見送り。");
        return(score*.85,word,reason+" 再放送だが好みとの一致が強いため候補に残しました。初回放送を優先するため評価を調整。");
    }
}
