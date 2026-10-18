using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
namespace ZooBridge;
internal static class ZooActivation
{
    internal static string PipeName=>"TVRockZoo-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName+"\\"+Environment.UserName)))[..20];
    internal static bool ShowExisting(string? name=null)
    {
        try{using var pipe=new NamedPipeClientStream(".",name??PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);pipe.Connect(700);using var writer=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};using var reader=new StreamReader(pipe,leaveOpen:true);writer.WriteLine("show");return reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult()=="OK";}catch(Exception e)when(e is IOException or TimeoutException or UnauthorizedAccessException){return false;}
    }
    internal static CancellationTokenSource Listen(Form form,Action activate,string? name=null)
    {
        var stop=new CancellationTokenSource();
        _=Task.Run(async()=>{while(!stop.IsCancellationRequested){try{using var server=new NamedPipeServerStream(name??PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);await server.WaitForConnectionAsync(stop.Token);using var reader=new StreamReader(server,leaveOpen:true);using var writer=new StreamWriter(server,leaveOpen:true){AutoFlush=true};string? command=await reader.ReadLineAsync(stop.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2),stop.Token);if(command=="show"&&!form.IsDisposed){var shown=new TaskCompletionSource<bool>();form.BeginInvoke(new Action(()=>{try{activate();shown.TrySetResult(true);}catch{shown.TrySetResult(false);}}));bool ok=await shown.Task.WaitAsync(TimeSpan.FromSeconds(2),stop.Token);await writer.WriteLineAsync(ok?"OK":"FAILED");}}catch(OperationCanceledException){break;}catch(Exception e)when(e is IOException or TimeoutException or InvalidOperationException or UnauthorizedAccessException){await Task.Delay(100,stop.Token).ConfigureAwait(false);}}},stop.Token);
        return stop;
    }
}
