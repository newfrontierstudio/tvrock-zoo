using System.Text.Json;
using System.Text.RegularExpressions;
using TsRecommend;
namespace TsScheduler;
internal static class PromoFilter
{
    internal static bool IsExcluded(string title)=>Regex.IsMatch(Data.Clean(title),@"NHKスペシャル\s*PR",RegexOptions.IgnoreCase);
    internal static async Task<int> Run()
    {
        var candidates=Data.Bookings(Path.Combine(MainForm.RockFolder,"tvrock.sch")).Where(b=>b.Enabled&&!MainForm.ReadPromoOverrides().Contains(b.Id)&&!b.Manual&&!b.Repeating&&b.Start>DateTimeOffset.Now.AddMinutes(15)&&IsExcluded(b.Title)).ToList();if(candidates.Count==0)return 0;
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TVRockZoo","pr-filter-history",DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(root);File.Copy(Path.Combine(MainForm.RockFolder,"tvrock.sch"),Path.Combine(root,"tvrock-before.sch"));int count=0;
        foreach(var target in candidates){var current=Data.Bookings(Path.Combine(MainForm.RockFolder,"tvrock.sch")).SingleOrDefault(b=>b.Id==target.Id);if(current==null||!current.Enabled||current.Manual||current.Start<DateTimeOffset.Now.AddMinutes(15)||!IsExcluded(current.Title))continue;
            string html=await MainForm.NativeGet("reg?i="+target.Id);File.WriteAllText(Path.Combine(root,target.Id+".html"),html);var fields=Data.Form(html,"予約を変更");if(fields.GetValueOrDefault("i")!=target.Id||Data.Norm(fields.GetValueOrDefault("title",""))!=Data.Norm(target.Title))throw new InvalidDataException("PR予約の照合に失敗しました。");fields.Remove("valid");
            await MainForm.NativeGet("regsch?"+string.Join("&",fields.Select(p=>Data.Encode(p.Key)+"="+Data.Encode(p.Value))));await Task.Delay(300);if(Data.Bookings(Path.Combine(MainForm.RockFolder,"tvrock.sch")).Any(b=>b.Id==target.Id&&b.Enabled))throw new IOException("PR予約の無効化を確認できません。");count++;File.AppendAllText(Path.Combine(root,"result.txt"),target.Id+" "+target.Title+Environment.NewLine);
        }return count;
    }
}
internal sealed partial class MainForm
{
    bool _prFilterBusy;DateTimeOffset _nextPrFilter;
    async Task FilterPromoReservationsAsync(){if(!Zoo.RuntimeConfiguration.Current.ExcludeNhkPromos||_changingReservation||_prFilterBusy||DateTimeOffset.Now<_nextPrFilter)return;_prFilterBusy=true;_nextPrFilter=DateTimeOffset.Now.AddMinutes(1);try{int count=await PromoFilter.Run();if(count>0){LoadNativeReservations();Log($"NHKスペシャルのPR予約 {count}件を除外しました。");}}catch(Exception e){_nextPrFilter=DateTimeOffset.Now.AddMinutes(5);Log("PR除外の再確認待ち: "+e.Message);}finally{_prFilterBusy=false;}}
}
