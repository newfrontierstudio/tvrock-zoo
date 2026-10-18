using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    void InitializeConnections(FlowLayoutPanel toolbar)
    {
        var button=new Button{Text="接続先："+ConnectionProfiles.Current.Active.Name,AutoSize=true};_detailLanding.Controls.OfType<FlowLayoutPanel>().First().Controls.Add(button);Text=$"TVRock Zoo — {ConnectionProfiles.Current.Active.Name} — 予約・番組表 Ver.{Application.ProductVersion.Split('+')[0]}";StyleControls(button);
        button.Click+=(_,_)=>{
            using var dialog=new Form{Text="TVRockの接続先",ClientSize=new Size(650,350),Font=Font,StartPosition=FormStartPosition.CenterParent,MinimumSize=new Size(600,390)};
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=6};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));dialog.Controls.Add(panel);
            var settings=ConnectionProfiles.Read();var select=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};foreach(var p in settings.Profiles)select.Items.Add(p.Name);select.Items.Add("＋ 新しい接続先");var name=new TextBox{Dock=DockStyle.Fill};var url=new TextBox{Dock=DockStyle.Fill};var folder=new TextBox{Dock=DockStyle.Fill};
            void Row(string label,Control input,int row){panel.Controls.Add(new Label{Text=label,AutoSize=true},0,row);panel.Controls.Add(input,1,row);panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));}Row("保存済み",select,0);Row("名前",name,1);Row("TVRockのURL",url,2);Row("TVRockフォルダー",folder,3);
            select.SelectedIndexChanged+=(_,_)=>{var p=select.SelectedIndex<settings.Profiles.Count?settings.Profiles[select.SelectedIndex]:new ConnectionProfile{Name="録画サーバー",Folder=@"\\サーバー名\共有名",Url="http://サーバー名:8969/user/",Id=Guid.NewGuid().ToString("N")};name.Text=p.Name;url.Text=p.Url;folder.Text=p.Folder;};select.SelectedIndex=settings.Profiles.FindIndex(p=>p.Id==settings.ActiveId);
            var note=new Label{Text="URLと共有フォルダーは同じTVRockを指定してください。\n切り替え後はZooを再起動します。接続先ごとにおすすめ・補正設定を保存します。",AutoSize=true,MaximumSize=new Size(590,0)};panel.Controls.Add(note,0,4);panel.SetColumnSpan(note,2);
            var save=new Button{Text="保存して接続先を切り替える",AutoSize=true};panel.Controls.Add(save,1,5);StyleControls(save);
            save.Click+=(_,_)=>{try{var profile=new ConnectionProfile{Id=select.SelectedIndex<settings.Profiles.Count?settings.Profiles[select.SelectedIndex].Id:Guid.NewGuid().ToString("N"),Name=name.Text.Trim(),Folder=folder.Text.Trim(),Url=ConnectionProfile.Normalize(url.Text)};profile.Validate();if(_changingReservation||_manualBookingBusy||_timingBusy||!_recommend.Enabled)throw new InvalidOperationException("予約処理が終了してから切り替えてください。");if(select.SelectedIndex<settings.Profiles.Count){var previous=settings.Profiles[select.SelectedIndex];if(!previous.Url.Equals(profile.Url,StringComparison.OrdinalIgnoreCase)||!previous.Folder.Equals(profile.Folder,StringComparison.OrdinalIgnoreCase)){profile.Id=Guid.NewGuid().ToString("N");settings.Profiles.Add(profile);}else settings.Profiles[select.SelectedIndex]=profile;}else settings.Profiles.Add(profile);settings.ActiveId=profile.Id;settings.Save();_exitRequested=true;Application.Restart();}catch(Exception error){MessageBox.Show(dialog,error.Message,"接続先を保存できません");}};
            dialog.ShowDialog(this);
        };
    }
}
