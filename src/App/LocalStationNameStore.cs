using System.Text.Json;
namespace TsScheduler;
internal static class LocalStationNameStore
{
    static readonly object gate=new();
    internal static Dictionary<string,string> Read(string path,Dictionary<string,string> fallback)
    {
        lock(gate){
            if(!File.Exists(path))return new(fallback);
            try{return Parse(path);}catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){
                if(File.Exists(path+".bak"))return Parse(path+".bak");
                throw new IOException("ローカル局名を読み込めません。保存済みの名前は上書きしません。",e);
            }
        }
    }
    static Dictionary<string,string> Parse(string path)=>JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))??throw new JsonException("局名データが空です。");
    internal static void Set(string path,string key,string value,Dictionary<string,string> legacy,Func<string,string> normalize)
    {
        lock(gate){
            var names=Read(path,legacy);
            foreach(string old in names.Keys.Where(k=>normalize(k)==key).ToArray())names.Remove(old);
            if(value.Length>0)names[key]=value;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                File.WriteAllText(temporary,JsonSerializer.Serialize(names));
                if(File.Exists(path)){
                    // Preserve a corrupt file without replacing the last good backup with it.
                    bool valid=true;try{Parse(path);}catch{valid=false;}
                    if(!valid)File.Copy(path,path+".unreadable-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));
                    File.Replace(temporary,path,valid?path+".bak":null);
                }else{File.Move(temporary,path);File.Copy(path,path+".bak");}
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
