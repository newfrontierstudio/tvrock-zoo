using TsRecommend;
namespace TsScheduler;
internal static class KeywordFilenameTests
{
    internal static void Run(string root)
    {
        Directory.CreateDirectory(root);
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        Check(!KeywordFileNames.Enabled(root),"Filename suffix defaults to OFF");
        KeywordFileNames.Save(root,true);Check(KeywordFileNames.Enabled(root),"Filename suffix ON persists");
        string path=Path.Combine(root,"番組_id.ts"),changed=KeywordFileNames.Append(path,"工場/見学:?");
        Check(Path.GetFileName(changed)=="番組_id_工場_見学__.ts"&&Path.GetDirectoryName(changed)==root,"Suffix is safe and precedes extension");
        Check(KeywordFileNames.Append(changed,"工場/見学:?")==changed&&KeywordFileNames.Append(path,"")==path,"Empty words and repeated application are harmless");
        Check(KeywordFileNames.Clean(new string('a',59)+"😀").Length==59,"Truncation must not split a surrogate pair");
        string source="[KEYWORD0]\r\nSEARCHID=10\r\nTWORD=工場見学\r\nFFORMEX=1\r\nFNFORM=@TT [@CH]\r\nLASTUD=123\r\n[KEYWORD1]\r\nSEARCHID=11\r\nTWORD=旅@番組\r\nFFORMEX=0\r\nFNFORM=inactive\r\n";
        var enabled=RockKeywordFileNames.Enable(source,"@TT_@YY",[]);
        Check(enabled.Text.Contains("FNFORM=@TT [@CH]_工場見学")&&enabled.Text.Contains("FNFORM=@TT_@YY_旅＠番組")&&enabled.Text.Contains("LASTUD=123"),"Existing format and unrelated fields survive; disabled override uses global format");
        Check(RockKeywordFileNames.Enable(enabled.Text,"different",enabled.Originals).Text==enabled.Text,"Repeated ON must not duplicate suffixes");
        Check(RockKeywordFileNames.Disable(enabled.Text,enabled.Originals)==source,"OFF restores original format and flag");
        Check(RockKeywordFileNames.Enable(source,"@TT_@YY",enabled.Originals).Text==enabled.Text,"ON after OFF must work without a duplicate suffix");
        string edited=enabled.Text.Replace("@TT [@CH]_工場見学","user-edited");Check(RockKeywordFileNames.Disable(edited,enabled.Originals).Contains("FNFORM=user-edited"),"OFF preserves subsequent external edits");
        bool rejected=false;try{RockKeywordFileNames.Enable(edited,"@TT",enabled.Originals);}catch(InvalidOperationException){rejected=true;}Check(rejected,"ON must reject conflicting external changes");
        KeywordFileNames.Save(root,false);Check(!KeywordFileNames.Enabled(root),"Filename suffix OFF persists");
    }
}
