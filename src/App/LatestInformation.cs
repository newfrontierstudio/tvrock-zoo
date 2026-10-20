namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly Button _latestInformation=new(){Text="最新の情報（F5）",AutoSize=true};
    readonly Label _latestInformationState=new(){Text="",AutoSize=true,Margin=new Padding(4,10,8,0)};
    bool latestInformationBusy;
    async Task RefreshLatestInformation()
    {
        if(latestInformationBusy||IsDisposed)return;latestInformationBusy=true;_latestInformation.Enabled=false;_latestInformationState.Text="更新中…";
        try
        {
            LoadNativeData();
            await RefreshInformationSourceAsync();
            if(IsDisposed)return;LoadNativeData();
            _latestInformationState.Text="更新完了 "+DateTime.Now.ToString("HH:mm:ss");
        }
        catch(Exception error){if(!IsDisposed)_latestInformationState.Text="更新できません："+error.Message;}
        finally{latestInformationBusy=false;if(!IsDisposed)_latestInformation.Enabled=true;}
    }
}
