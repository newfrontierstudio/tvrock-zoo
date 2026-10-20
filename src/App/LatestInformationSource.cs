namespace TsScheduler;
internal sealed partial class MainForm
{
    internal void RefreshStudio()=>_ = RefreshLatestInformation();
    Task RefreshInformationSourceAsync()=>Task.CompletedTask;
}
