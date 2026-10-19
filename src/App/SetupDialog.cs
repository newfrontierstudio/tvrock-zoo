namespace TsScheduler;

internal sealed class SetupDialog : Form
{
    readonly TextBox folder = new() { Dock = DockStyle.Fill };
    readonly TextBox address = new() { Dock = DockStyle.Fill };
    internal SetupDialog(Func<bool>? canSave=null)
    {
        Text = "TVRock Zoo — 接続設定";
        Font = new Font("Meiryo UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(700, 330);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        var config = ReadForSetup().Active;
        folder.Text = config.Folder; address.Text = config.Url;
        if (folder.Text.Length == 0) {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("TVRock"))
                using (process) try {
                    var candidate = Path.GetDirectoryName(process.MainModule?.FileName);
                    if (candidate != null && File.Exists(Path.Combine(candidate,"tvrock.sch"))) { folder.Text = candidate; break; }
                } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        layout.Controls.Add(new Label { Text = "TVRockフォルダー", AutoSize = true }, 0, 0); layout.Controls.Add(folder, 1, 0);
        var browse = new Button { Text = "参照", AutoSize = true };
        browse.Click += (_, _) => { using var picker = new FolderBrowserDialog(); if (picker.ShowDialog(this) == DialogResult.OK) folder.Text = picker.SelectedPath; };
        layout.Controls.Add(browse, 2, 0);
        layout.Controls.Add(new Label { Text = "TVRock Web URL", AutoSize = true }, 0, 1); layout.Controls.Add(address, 1, 1); layout.SetColumnSpan(address, 2);
        var note = new Label { AutoSize = true, MaximumSize = new Size(620,0), Text = "①「参照」でTVRockのフォルダーを選びます（起動中なら自動検出）。\n②いつもTVRockを開くURLを貼り付けます。末尾の /list などは自動で整えます。\n③「保存して続ける」で完了。設定のやり直しでも、録画ファイルとTVRockの予約は変更しません。\nZooの設定保存先：%LOCALAPPDATA%\\TVRockZoo" };
        layout.Controls.Add(note, 0, 3); layout.SetColumnSpan(note, 3);
        var save = new Button { Text = "保存して続ける", AutoSize = true };
        save.Click += (_, _) => {
                        TsRecommend.ConnectionProfiles profiles;TsRecommend.ConnectionProfile updated;
            try{if(canSave!=null&&!canSave())throw new InvalidOperationException("予約処理が終了してから設定を保存してください。");profiles=ReadForSetup();updated=UpdateProfile(profiles,folder.Text.Trim(),address.Text);}catch(Exception error){MessageBox.Show(this,error.Message,"接続設定");return;}
            try{updated.Validate();}catch(Exception error){MessageBox.Show(this,error.Message,"接続設定");return;}
            try { if(File.Exists(TsRecommend.ConnectionProfiles.FilePath))File.Copy(TsRecommend.ConnectionProfiles.FilePath,TsRecommend.ConnectionProfiles.FilePath+".before-setup",true);profiles.Save(); DialogResult = DialogResult.OK; Close(); } catch (Exception e) { MessageBox.Show(this, e.Message); }
        };
        layout.Controls.Add(save, 1, 4); var cancel=new Button{Text="キャンセル",AutoSize=true,DialogResult=DialogResult.Cancel};layout.Controls.Add(cancel,2,4);CancelButton=cancel;Controls.Add(layout); AcceptButton = save;
        foreach(var button in new[]{browse,save}){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.BackColor=Color.FromArgb(231,238,247);RoundedControls.Apply(button);}
    }
        internal static TsRecommend.ConnectionProfiles ReadForSetup(){try{return TsRecommend.ConnectionProfiles.Read();}catch(Exception e)when(e is System.Text.Json.JsonException or InvalidDataException or IOException or UnauthorizedAccessException){return new();}}
    internal static TsRecommend.ConnectionProfile UpdateProfile(TsRecommend.ConnectionProfiles profiles,string folder,string address)
    {
        var previous=profiles.Active;var updated=new TsRecommend.ConnectionProfile{Id=previous.Id,Name=previous.Name,Folder=folder,Url=TsRecommend.ConnectionProfile.Normalize(address)};updated.Validate();
        if(previous.Folder.Length>0&&(!previous.Folder.Equals(updated.Folder,StringComparison.OrdinalIgnoreCase)||!previous.Url.Equals(updated.Url,StringComparison.OrdinalIgnoreCase))){updated.Id=Guid.NewGuid().ToString("N");profiles.Profiles.Add(updated);}else profiles.Profiles[profiles.Profiles.FindIndex(p=>p.Id==previous.Id)]=updated;profiles.ActiveId=updated.Id;return updated;
    }
    internal static bool EnsureConfigured(bool force=false)
    {
        if(!force)try{TsRecommend.ConnectionProfiles.Current.Active.Validate();return true;}catch(Exception error) when(error is System.Text.Json.JsonException or InvalidDataException or IOException or UnauthorizedAccessException or UriFormatException){}
        using var dialog = new SetupDialog();
        if (dialog.ShowDialog() != DialogResult.OK) return false;
        // Reload the saved configuration before creating the main window.
        Application.Restart();
        return false;
    }
}
