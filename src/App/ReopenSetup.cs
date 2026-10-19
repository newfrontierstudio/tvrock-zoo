namespace TsScheduler;
internal sealed partial class MainForm
{
    void ReopenSetup()
    {
        bool CanSave()=>!_changingReservation&&!_manualBookingBusy&&!_timingBusy&&_recommend.Enabled;
        if(!CanSave()){MessageBox.Show(this,"予約処理が終了してから設定をやり直してください。");return;}
        using var dialog=new SetupDialog(CanSave);if(dialog.ShowDialog(this)!=DialogResult.OK)return;_exitRequested=true;Application.Restart();
    }
}
