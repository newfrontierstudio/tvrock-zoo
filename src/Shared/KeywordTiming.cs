namespace TsRecommend;
internal static class KeywordTiming
{
 internal static (int Head,int Tail) Read(IReadOnlyDictionary<string,string>? fields){int Number(string key)=>int.TryParse(fields?.GetValueOrDefault(key),out int value)?Math.Clamp(value,-3600,3600):0;return(-Number(fields?.ContainsKey("REDAY")==true?"REDAY":"READY"),Number("TALE"));}
}
