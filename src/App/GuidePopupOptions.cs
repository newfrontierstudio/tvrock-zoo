namespace TsScheduler;
internal sealed partial class MainForm
{
    void InitializeGuidePopupOptions(FlowLayoutPanel body)
    {
        var toggle=new CheckBox{Text="番組表の詳細ポップアップを表示",AutoSize=true,Checked=DisplayOptions.Current.GuideDetailsPopup};
        body.Controls.Add(toggle);
        bool restoring=false;
        toggle.CheckedChanged+=(_,_)=>
        {
            if(restoring)return;
            bool previous=DisplayOptions.Current.GuideDetailsPopup;
            try
            {
                DisplayOptions.Current.GuideDetailsPopup=toggle.Checked;
                DisplayOptions.Current.Save();
                if(!toggle.Checked)HideLargeTip();
            }
            catch(Exception error)
            {
                DisplayOptions.Current.GuideDetailsPopup=previous;
                restoring=true;toggle.Checked=previous;restoring=false;
                MessageBox.Show(this,error.Message,"表示設定を保存できません");
            }
        };
    }
}
