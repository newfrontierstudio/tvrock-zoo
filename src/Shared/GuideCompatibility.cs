using HtmlAgilityPack;
using System.Runtime.CompilerServices;
namespace TsRecommend;
static partial class Data
{
    public static Dictionary<int,int> TunerDevices(string html)
    {
        var doc=new HtmlAgilityPack.HtmlDocument();doc.LoadHtml(html);var map=new Dictionary<int,int>();
        foreach(var option in doc.DocumentNode.SelectNodes("//select[@name='devno']/option")??new HtmlNodeCollection(null)){
            string label=Clean(Text(option)).Normalize(System.Text.NormalizationForm.FormKC);
            var match=System.Text.RegularExpressions.Regex.Match(label,@"^チューナー\s*(\d+)\s*$");
            if(!match.Success||!int.TryParse(match.Groups[1].Value,out int number)||!int.TryParse(option.GetAttributeValue("value",""),out int device))continue;
            if(map.TryGetValue(number,out int previous)&&previous!=device)throw new InvalidDataException("TVRockのチューナー番号が重複しています。");map[number]=device;
        }
        if(map.Values.Distinct().Count()!=map.Count)throw new InvalidDataException("TVRockの予約先IDが重複しています。");
        return map;
    }
    public static string ServiceWarnings(string path){string section="";var names=new List<string>();foreach(var raw in File.ReadLines(path,Cp932)){var line=raw.Trim();if(line.StartsWith('[')){section=line;continue;}int eq=line.IndexOf('=');if(section.EndsWith(" SERVICE]",StringComparison.OrdinalIgnoreCase)&&eq>0&&int.TryParse(line[(eq+1)..],out int id)&&id==0)names.Add(line[..eq]);}return names.Count==0?"":"サービスIDが0："+string.Join("、",names.Distinct())+"。TVRock側の設定を確認してください（局名は番組表の見出しから読み込みます）。";}
    static readonly ConditionalWeakTable<HtmlNode,Dictionary<HtmlNode,string>> ColumnMaps=new();
    static string? GuideColumnStations(HtmlNode cell)
    {
        var table=cell.Ancestors("table").FirstOrDefault(t=>t.SelectSingleNode("./tbody/tr[1]/td/a[contains(@href,'google')]|./tr[1]/td/a[contains(@href,'google')]")!=null);
        if(table==null)return null;
        var map=ColumnMaps.GetValue(table,t=>{
            var result=new Dictionary<HtmlNode,string>();var rows=t.SelectNodes("./tbody/tr|./tr");if(rows==null)return result;
            var headers=rows.FirstOrDefault(r=>r.SelectSingleNode("./td/a[contains(@href,'google')]")!=null);if(headers==null)return result;
            var names=(headers.SelectNodes("./td")??new HtmlNodeCollection(null)).Select(n=>n.SelectSingleNode("./a[contains(@href,'google')]")).Select(n=>n==null?"":Clean(Text(n))).ToArray();var spans=new int[names.Length+64];
            foreach(var row in rows.Skip(rows.IndexOf(headers)+1)){
                int col=0;foreach(var td in row.SelectNodes("./td")??new HtmlNodeCollection(null)){while(col<spans.Length&&spans[col]>0)col++;if(col>=spans.Length)break;int width=Math.Clamp(td.GetAttributeValue("colspan",1),1,spans.Length-col);if(col<names.Length&&names[col].Length>0)result[td]=names[col];for(int c=col;c<col+width;c++)spans[c]=Math.Max(1,td.GetAttributeValue("rowspan",1));col+=width;}for(int c=0;c<spans.Length;c++)spans[c]=Math.Max(0,spans[c]-1);
            }return result;
        });return map.GetValueOrDefault(cell);
    }
}
