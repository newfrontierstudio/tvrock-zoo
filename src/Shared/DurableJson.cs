using System.Text;
using System.Text.Json;
namespace ZooStorage;
internal static class DurableJson {
 internal static string Read(string path){Recover(path);return File.ReadAllText(path);}
 static readonly object Gate=new();
 internal static bool Valid(string path){try{using var json=JsonDocument.Parse(File.ReadAllBytes(path));return json.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;}catch(IOException){return false;}catch(JsonException){return false;}}
 internal static bool Recover(string path){lock(Gate){if(!File.Exists(path)||Valid(path))return false;string archive=path+".corrupt-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N");File.Move(path,archive);if(Valid(path+".bak")){using var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);var bytes=File.ReadAllBytes(path+".bak");f.Write(bytes);f.Flush(true);}return true;}}
 internal static void Write(string path,string text){using(var doc=JsonDocument.Parse(text)){}lock(Gate){Directory.CreateDirectory(Path.GetDirectoryName(path)!);Recover(path);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.WriteThrough)){f.Write(Encoding.UTF8.GetBytes(text));f.Flush(true);}if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}}
 internal static void RecoverGenerated(string home){foreach(string name in new[]{"guide-cache-v3.json","epg-services.json","epg-full-services.json","epg-retry-state.json","keyword-auto-status.json"})Recover(Path.Combine(home,name));}
}