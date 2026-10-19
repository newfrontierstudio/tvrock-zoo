namespace TsScheduler;
internal static class DiagnosticLoggingTests
{
    internal static async Task Run(string root)
    {
        Directory.CreateDirectory(root);string detail=Path.Combine(root,"diagnostic.jsonl"),errors=Path.Combine(root,"errors.jsonl");
        if(DiagnosticLogging.Enabled(root))throw new Exception("Diagnostic logging must default to OFF");DiagnosticLogging.Write(root,"test.detail");if(File.Exists(detail))throw new Exception("OFF must not create a diagnostic log");
        DiagnosticLogging.Write(root,"test.error",true,"IOException");if(!File.Exists(errors))throw new Exception("Errors must remain when diagnostic logging is OFF");
        DiagnosticLogging.SetEnabled(root,true);if(!DiagnosticLogging.Enabled(root))throw new Exception("ON must persist");
        await DiagnosticLogging.Request(root,"regsch?title=private-title&path=C:/private",()=>Task.FromResult("private-response"));
        try{await DiagnosticLogging.Request(root,"reg?private=secret",()=>Task.FromException<string>(new IOException("private error text")));}catch(IOException){}
        DiagnosticLogging.Write(root,"private title/path",status:"private URL?query=secret");string all=File.ReadAllText(detail)+File.ReadAllText(errors);if(all.Contains("private")||all.Contains("secret")||all.Contains("response"))throw new Exception("Raw titles, paths, queries, responses and exception messages must not be logged");
        string before=File.ReadAllText(detail);DiagnosticLogging.SetEnabled(root,false);DiagnosticLogging.Write(root,"test.after-off");if(DiagnosticLogging.Enabled(root)||File.ReadAllText(detail)!=before)throw new Exception("OFF must persist and stop detailed logging without removing history");
        DiagnosticLogging.SetEnabled(root,true);File.WriteAllText(detail,new string('x',1024*1024));DiagnosticLogging.Write(root,"test.rotation");if(!File.Exists(detail+".previous")||new FileInfo(detail).Length>2048)throw new Exception("Log size must be bounded");
    }
}
