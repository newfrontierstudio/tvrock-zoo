namespace TsScheduler;

internal sealed class SetupDialog : Form
{
    readonly TextBox folder = new() { Dock = DockStyle.Fill };
    readonly TextBox address = new() { Dock = DockStyle.Fill };
    readonly CheckBox promos = new() { AutoSize = true, Text = "NHKスペシャルのPR予約を自動で無効にする" };
    internal SetupDialog()
    {
        Text = "TVRock Zoo — 接続設定";
        Font = new Font("Meiryo UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(660, 250);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        var config = Zoo.RuntimeConfiguration.Current;
        folder.Text = config.TvRockFolder; address.Text = config.TvRockUrl; promos.Checked = config.ExcludeNhkPromos;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        layout.Controls.Add(new Label { Text = "TVRockフォルダー", AutoSize = true }, 0, 0); layout.Controls.Add(folder, 1, 0);
        var browse = new Button { Text = "参照", AutoSize = true };
        browse.Click += (_, _) => { using var picker = new FolderBrowserDialog(); if (picker.ShowDialog(this) == DialogResult.OK) folder.Text = picker.SelectedPath; };
        layout.Controls.Add(browse, 2, 0);
        layout.Controls.Add(new Label { Text = "TVRock Web URL", AutoSize = true }, 0, 1); layout.Controls.Add(address, 1, 1); layout.SetColumnSpan(address, 2);
        layout.Controls.Add(promos, 0, 2); layout.SetColumnSpan(promos, 3);
        var note = new Label { AutoSize = true, Text = "TVRock本体は別途必要です。自動おすすめ予約は初期状態でオフです。\nGPT不使用・PC内処理・外部送信なし・API料金なし。" };
        layout.Controls.Add(note, 0, 3); layout.SetColumnSpan(note, 3);
        var save = new Button { Text = "保存して続ける", AutoSize = true };
        save.Click += (_, _) => {
            var updated = new Zoo.RuntimeConfiguration { TvRockFolder = folder.Text.Trim(), TvRockUrl = address.Text.Trim(), ExcludeNhkPromos = promos.Checked };
            if (updated.Validate() is string error) { MessageBox.Show(this, error, "接続設定"); return; }
            try { updated.Save(); DialogResult = DialogResult.OK; Close(); } catch (Exception e) { MessageBox.Show(this, e.Message); }
        };
        layout.Controls.Add(save, 1, 4); Controls.Add(layout); AcceptButton = save;
    }
    internal static bool EnsureConfigured()
    {
        if (Zoo.RuntimeConfiguration.Current.Validate() == null) return true;
        using var dialog = new SetupDialog();
        if (dialog.ShowDialog() != DialogResult.OK) return false;
        // Reload the saved configuration before creating the main window.
        Application.Restart();
        return false;
    }
}
