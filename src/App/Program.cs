namespace TsScheduler;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);ApplicationConfiguration.Initialize();if(args.Contains("--inferno-ui-test")){Environment.ExitCode=RecordedMediaTests.InfernoUi(args[Array.IndexOf(args,"--inferno-ui-test")+1]);return;}if(args.Contains("--install-media-pack")){int index=Array.IndexOf(args,"--install-media-pack");try{MediaToolsSupport.Install(args[index+1]);Environment.ExitCode=0;}catch{Environment.ExitCode=1;}return;}if(args.Contains("--media-self-test")){Environment.ExitCode=RecordedMediaTests.Run().GetAwaiter().GetResult();return;}if(args.Contains("--media-ui-self-test")){Environment.ExitCode=RecordedMediaTests.Ui();return;}if(args.Contains("--media-selection-self-test")){Environment.ExitCode=RecordedMediaTests.SelectionUi();return;}
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var single=new Mutex(true,"Local\\"+ZooBridge.ZooActivation.PipeName,out bool first);
        if(!first){ZooBridge.ZooActivation.ShowExisting();return;}
        if (!SetupDialog.EnsureConfigured(args.Contains("--setup"))) return;
        using var form=new MainForm(args.Contains("--tray", StringComparer.OrdinalIgnoreCase));CancellationTokenSource? listener=null;form.Shown+=(_,_)=>listener=ZooBridge.ZooActivation.Listen(form,form.ActivateControlWindow);try{Application.Run(form);}finally{listener?.Cancel();listener?.Dispose();}
    }
}
