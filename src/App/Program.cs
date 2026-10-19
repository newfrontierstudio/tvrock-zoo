namespace TsScheduler;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var single=new Mutex(true,"Local\\"+ZooBridge.ZooActivation.PipeName,out bool first);
        if(!first){ZooBridge.ZooActivation.ShowExisting();return;}
        if (!SetupDialog.EnsureConfigured(args.Contains("--setup"))) return;
        using var form=new MainForm(args.Contains("--tray", StringComparer.OrdinalIgnoreCase));CancellationTokenSource? listener=null;form.Shown+=(_,_)=>listener=ZooBridge.ZooActivation.Listen(form,form.ActivateControlWindow);try{Application.Run(form);}finally{listener?.Cancel();listener?.Dispose();}
    }
}
