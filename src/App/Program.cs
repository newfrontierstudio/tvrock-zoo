namespace TsScheduler;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        if (!SetupDialog.EnsureConfigured()) return;
        Application.Run(new MainForm(args.Contains("--tray", StringComparer.OrdinalIgnoreCase)));
    }
}
