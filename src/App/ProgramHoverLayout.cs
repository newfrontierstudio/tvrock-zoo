using System.Text.RegularExpressions;
namespace TsScheduler;
internal sealed partial class MainForm
{
    // Only format the popup: original EPG descriptions and saved metadata stay intact.
    internal static string CompactProgramHoverText(string text)
    {
        var lines=text.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
        bool Boundary(string line)=>Regex.IsMatch(line,@"^(番組内容|出演者|公式ページ|おことわり|関連情報|関連番組|スタッフ|その他)\s*[0-9０-９]*$")
            ||Regex.IsMatch(line,@"^(【[^】]+】|\[[^\]]+\])$")
            ||Regex.IsMatch(line,@"^(検索ワード|録画しない理由|録画時間|割当先|使用チューナー)[：:]")
            ||line.StartsWith("http",StringComparison.OrdinalIgnoreCase)
            ||line.StartsWith('@')||line.IndexOfAny(['▽','▼','◇','★'])==0
            ||line.EndsWith('：')||line.EndsWith(':');
        bool Short(string line)=>line.Length is >0 and <=42&&!Boundary(line);
        if(lines.Length<8||lines.Count(l=>Short(l.Trim()))<4)return text;
        var output=new List<string>();var run=new List<string>();
        void Flush()
        {
            if(run.Count>=3)
            {
                string joined=run[0];
                for(int i=1;i<run.Count;i++)joined+=(Regex.IsMatch(run[i-1],@"[。！？!?]$")?" ":" ／ ")+run[i];
                output.Add(joined);
            }
            else output.AddRange(run);
            run.Clear();
        }
        foreach(string raw in lines)
        {
            string line=raw.Trim();
            if(line is "・" or "･" or "•" or "." or "．")continue;
            if(Short(line)){run.Add(line);continue;}
            Flush();
            if(line.Length>0||output.Count>0&&output[^1].Length>0)output.Add(line);
        }
        Flush();return string.Join("\n",output).TrimEnd();
    }
}
