using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TsRecommend;
namespace TsScheduler;

// TVRock owns this file while running. Only edit its filename fields while it is stopped.
internal static class RockKeywordFileNames
{
    internal sealed record Original(string Id,string Word,string Format,string Flag,string Applied);
    internal static (string Text,List<Original> Originals) Enable(string text,string defaultFormat,List<Original> previous)
    {
        var originals=new List<Original>();
        string result=Regex.Replace(text,@"(?m)^\[KEYWORD\d+\][\s\S]*?(?=^\[KEYWORD\d+\]|\z)",m=>{
            string section=m.Value;
            string Get(string name)=>Regex.Match(section,@"(?m)^"+name+@"=([^\r\n]*)").Groups[1].Value;
            string id=Get("SEARCHID"),word=Get("TWORD"),format=Get("FNFORM"),flag=Get("FFORMEX");
            if(id.Length==0||word.Trim().Length==0)return section;
            if(!Regex.IsMatch(section,@"(?m)^FNFORM=")||!Regex.IsMatch(section,@"(?m)^FFORMEX="))throw new InvalidDataException("TVRockのファイル名設定を確認できない検索条件があります。");
            var old=previous.SingleOrDefault(x=>x.Id==id&&x.Word==word);
            if(old!=null&&format==old.Applied&&flag=="1"){originals.Add(old);return section;}
            if(old!=null&&(format!=old.Format||flag!=old.Flag))throw new InvalidOperationException("TVRock側でファイル名設定が変更されています。設定を確認してください。");
            string suffix=KeywordFileNames.Clean(word).Replace('@','＠');if(suffix.Length==0)return section;
            string basis=flag=="1"&&format.Length>0?format:defaultFormat;
            string applied=basis+"_"+suffix;
            if(applied.Length>160)throw new InvalidOperationException("検索ワード追加後のファイル名書式が160文字を超えます。");
            originals.Add(new(id,word,format,flag,applied));
            section=Regex.Replace(section,@"(?m)^FNFORM=[^\r\n]*",_=>"FNFORM="+applied);
            return Regex.Replace(section,@"(?m)^FFORMEX=[^\r\n]*",_=>"FFORMEX=1");
        });
        if(originals.GroupBy(x=>x.Id).Any(g=>g.Count()>1))throw new InvalidDataException("検索条件のIDが重複しています。");
        return(result,originals);
    }
    internal static string Disable(string text,List<Original> originals)=>Regex.Replace(text,@"(?m)^\[KEYWORD\d+\][\s\S]*?(?=^\[KEYWORD\d+\]|\z)",m=>{
        string section=m.Value;string Get(string n)=>Regex.Match(section,@"(?m)^"+n+@"=([^\r\n]*)").Groups[1].Value;
        var old=originals.SingleOrDefault(x=>x.Id==Get("SEARCHID")&&x.Word==Get("TWORD"));if(old==null)return section;
        // An external edit is preserved rather than overwritten when turning OFF.
        if(Get("FNFORM")!=old.Applied||Get("FFORMEX")!="1")return section;
        section=Regex.Replace(section,@"(?m)^FNFORM=[^\r\n]*",_=>"FNFORM="+old.Format);
        return Regex.Replace(section,@"(?m)^FFORMEX=[^\r\n]*",_=>"FFORMEX="+old.Flag);
    });
    static void RequireStopped(){foreach(var p in Process.GetProcessesByName("tvrock"))using(p){if(!p.HasExited)throw new InvalidOperationException("ZooではTVRockを終了してから変更してください。次回起動後に作られる検索予約から適用します。");}}
    internal static int Apply(string home,string folder,bool enabled)
    {
        if(folder.StartsWith(@"\\"))throw new InvalidOperationException("別PCのTVRockのファイル名設定は、そのPCで変更してください。");
        RequireStopped();string path=Path.Combine(folder,"tvrock.key"),backup=Path.Combine(home,"filename-search-word-backup.json");
        var previous=File.Exists(backup)?JsonSerializer.Deserialize<List<Original>>(File.ReadAllText(backup))??throw new InvalidDataException("ファイル名設定のバックアップを読めません。"):new List<Original>();
        byte[] before=File.ReadAllBytes(path);string text=Data.Cp932.GetString(before);if(!Data.Cp932.GetBytes(text).SequenceEqual(before))throw new InvalidDataException("検索条件の文字コードを確認できないため変更しません。");string format;
        using(var registry=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\TvRock"))format=registry?.GetValue("FNFORM") as string??"@TT@NB@SB";
        var plan=enabled?Enable(text,format,previous):(Text:Disable(text,previous),Originals:previous);
        Directory.CreateDirectory(home);
        // Save recovery information before changing the source; repeated ON remains idempotent.
        if(enabled){File.WriteAllText(backup+".tmp",JsonSerializer.Serialize(plan.Originals));File.Move(backup+".tmp",backup,true);}
        RequireStopped();if(!File.ReadAllBytes(path).SequenceEqual(before))throw new IOException("検索条件が更新されました。もう一度確認してください。");
        bool wasEnabled=KeywordFileNames.Enabled(home);KeywordFileNames.Save(home,enabled);
        try{byte[] after=Data.Cp932.GetBytes(plan.Text);if(!after.SequenceEqual(before)){
            string temp=path+".zoo-filename.tmp";try{File.WriteAllBytes(temp,after);RequireStopped();if(!File.ReadAllBytes(path).SequenceEqual(before))throw new IOException("検索条件が更新されました。");File.Replace(temp,path,path+".zoo-filename.bak");}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        }catch{KeywordFileNames.Save(home,wasEnabled);throw;}return plan.Originals.Count;
    }
}
internal sealed partial class MainForm
{
    void InitializeKeywordFilenameOptions(FlowLayoutPanel body)
    {
        var toggle=new CheckBox{Text="検索ワードをファイル名末尾に付ける",AutoSize=true,Checked=KeywordFileNames.Enabled(RecommendationHome),Margin=new Padding(0,10,0,4)};
        body.Controls.Add(toggle);
        body.Controls.Add(new Label{Text="例：番組名_検索ワード.ts。TVRock終了中に設定し、次回起動後の検索予約に適用します。既存予約・録画は変更しません。",AutoSize=true,MaximumSize=new Size(650,0),Margin=new Padding(0,0,0,8)});
        bool restoring=false;toggle.CheckedChanged+=(_,_)=>{if(restoring)return;try{RockKeywordFileNames.Apply(RecommendationHome,RockFolder,toggle.Checked);}catch(Exception error){restoring=true;toggle.Checked=KeywordFileNames.Enabled(RecommendationHome);restoring=false;MessageBox.Show(this,error.Message,"ファイル名設定を変更できません");}};
        {var apply=new Button{Text="新しい検索条件にも適用",AutoSize=true};apply.Click+=(_,_)=>{try{if(!KeywordFileNames.Enabled(RecommendationHome))throw new InvalidOperationException("先に「検索ワードをファイル名末尾に付ける」をONにしてください。");int count=RockKeywordFileNames.Apply(RecommendationHome,RockFolder,true);MessageBox.Show(this,$"検索条件 {count}件に適用しました。TVRockを起動してください。");}catch(Exception error){MessageBox.Show(this,error.Message,"ファイル名設定");}};body.Controls.Add(apply);}
    }
}
