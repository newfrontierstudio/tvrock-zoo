using System.Diagnostics;
using System.Text.Json;
namespace TsScheduler;
internal sealed partial class MainForm
{
 internal static string RecommendationExecutable(){string shared=Path.Combine(AppContext.BaseDirectory,"TsRecommend.exe");return File.Exists(shared)?shared:Path.Combine(AppContext.BaseDirectory,"Recommend","TsRecommend.exe");}
}
internal static class PackageLayoutTests
{
 internal static int Run(){string root=AppContext.BaseDirectory;try{
  foreach(string file in new[]{"TVRockZoo.exe","TVRockZoo.dll","TVRockZoo.runtimeconfig.json","TsRecommend.exe","TsRecommend.dll","TsRecommend.runtimeconfig.json","System.Private.CoreLib.dll","System.Windows.Forms.dll","hostfxr.dll","quantum-core-rs.exe","quantum-engine.txt"})if(!File.Exists(Path.Combine(root,file)))throw new FileNotFoundException("Missing package file",file);
  if(Directory.GetFiles(root,"inferno*",SearchOption.AllDirectories).Length>0||Directory.GetFiles(root,"*Docking*",SearchOption.AllDirectories).Length>0)throw new Exception("Unwanted bundled application");
  if(MainForm.RecommendationExecutable()!=Path.Combine(root,"TsRecommend.exe"))throw new Exception("Wrong recommendation worker");
  foreach(var assembly in new[]{typeof(object).Assembly,typeof(Form).Assembly})if(!assembly.Location.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new Exception("Runtime outside package");
  var info=new ProcessStartInfo(MainForm.RecommendationExecutable()){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add("--runtime-self-test");info.Environment["DOTNET_ROOT"]=Path.Combine(root,"no-global-dotnet");info.Environment["DOTNET_MULTILEVEL_LOOKUP"]="0";
  using var child=Process.Start(info)??throw new Exception("Worker did not start");if(!child.WaitForExit(10000)){child.Kill(true);throw new TimeoutException("Worker");}if(child.ExitCode!=0)throw new Exception(child.StandardError.ReadToEnd());
  File.WriteAllText(Path.Combine(root,"package-validation.json"),JsonSerializer.Serialize(new{passed=true,runtime=typeof(object).Assembly.Location,worker=MainForm.RecommendationExecutable(),child=child.StandardOutput.ReadToEnd(),inferno=false,docking=false,reservationsChanged=false},new JsonSerializerOptions{WriteIndented=true}));return 0;
 }catch(Exception error){File.WriteAllText(Path.Combine(root,"package-validation.json"),JsonSerializer.Serialize(new{passed=false,error=error.ToString()}));return 1;}}
}
