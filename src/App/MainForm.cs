using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text;

namespace TsScheduler;

internal sealed partial class MainForm : Form
{
    private readonly DataGridView _tuners = CreateGrid();
    private readonly DataGridView _reservations = CreateGrid();
    private readonly DataGridView _reservationList = CreateGrid();
    private readonly ListBox _activity = new() { Dock = DockStyle.Fill };
    private readonly Button _import = new() { Text = "TVRockデータを読み込む", AutoSize = true };
    private readonly Button _scheduler = new() { Text = "スケジューラ開始", AutoSize = true, Enabled = false };
    private readonly TextBox _webAddress = new() { Text = new Uri(Zoo.RuntimeConfiguration.BaseUri,"list").AbsoluteUri, Width = 300 };
    private readonly Button _openWeb = new() { Text = "TVRock Webを開く", AutoSize = true };
    private readonly Button _editReservation = new() { Text = "選択した予約を編集", AutoSize = true, Enabled = false };
    private readonly Label _engineStatus = new() { AutoSize = true, Text = "録画エンジン: 未接続（予約の確認のみ）" };
    private readonly WebView2 _webBrowser = new() { Dock = DockStyle.Fill };
    private readonly WebView2 _listBrowser = new() { Dock = DockStyle.Fill };
    private readonly WebView2 _guideBrowser = new() { Dock = DockStyle.Fill };
    private readonly TabControl _tabs = new FlatTabControl() { Dock = DockStyle.Fill };
    private readonly Icon _appIcon = CreateApplicationIcon();
    private readonly NotifyIcon _trayIcon = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5_000 };
    private IReadOnlyList<Reservation> _loadedReservations = [];
    private readonly HashSet<Reservation> _announcedReservations = [];
    private bool _webStyleInstalled;
    private bool _exitRequested;
    private readonly bool _startInTray;
    private readonly Button _recommend = new() { Text = "おすすめ更新", AutoSize = true };
    private readonly CheckBox _recommendToggle = new() { Appearance = Appearance.Button, AutoSize = true, Cursor = Cursors.Hand, AccessibleName = "自動おすすめ予約の切り替え" };
    private readonly ToolTip _recommendTip = new();
    private readonly System.Windows.Forms.Timer _recommendTimer = new() { Interval = 30000 };
    private string _lastRecommendationStamp = "";
    private DateTimeOffset _nextRecommendationAttempt;

    public MainForm(bool startInTray)
    {
        _startInTray = startInTray;
        Text = "TVRock Zoo — 予約・番組表 Ver.0.1.12";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 700);
        MinimumSize = new Size(780, 520);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, MinimumSize = new Size(0,48), Padding = new Padding(10, 8, 10, 8), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        toolbar.Controls.AddRange([_openWeb, _webAddress, _import, _editReservation, _scheduler, _recommend, _recommendToggle, _engineStatus]);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.Controls.Add(CreateGroup("チューナー", _tuners), 0, 0);
        layout.Controls.Add(CreateGroup("予約", _reservations), 0, 1);
        layout.Controls.Add(CreateGroup("アクティビティ", _activity), 0, 2);
        var webTab = new TabPage("予約一覧") { Padding = new Padding(3) };
        webTab.Controls.Add(_webBrowser);
        var reservationsTab = new TabPage("自動検索予約") { Padding = new Padding(3) };
        reservationsTab.Controls.Add(_listBrowser);
        var monitorTab = new TabPage("番組表") { Padding = new Padding(3) };
        monitorTab.Controls.Add(_guideBrowser);
        _tabs.TabPages.AddRange([webTab, monitorTab, reservationsTab]);
        Controls.Add(_tabs);
        Controls.Add(toolbar);
        Icon = _appIcon;
        InitializeKeywordConfirmation();
        InitializeTrayIcon();
        InitializeRecommendations();

        _import.Click += (_, _) => Import();
        _scheduler.Click += (_, _) => ToggleScheduler();
        _timer.Tick += (_, _) => CheckReservations();
        _openWeb.Click += async (_, _) => await OpenWebAsync();
        _editReservation.Click += async (_, _) => await OpenSelectedReservationAsync();
        _reservationList.SelectionChanged += (_, _) => _editReservation.Enabled = _reservationList.SelectedRows.Count > 0;
        _reservationList.CellDoubleClick += async (_, _) => await OpenSelectedReservationAsync();
        _webBrowser.NavigationCompleted += (_, e) => WebNavigationCompleted(_webBrowser, e);
        _listBrowser.NavigationCompleted += (_, e) => WebNavigationCompleted(_listBrowser, e);
        _guideBrowser.NavigationCompleted += (_, e) => WebNavigationCompleted(_guideBrowser, e);
        _tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (_tabs.SelectedTab?.Controls.Contains(_guideBrowser) == true && _guideBrowser.Source is null) await OpenTabAsync(_guideBrowser, "now");
            if (_tabs.SelectedTab?.Controls.Contains(_listBrowser) == true && _listBrowser.Source is null) await OpenTabAsync(_listBrowser, "kwl");
        };
        FormClosing += (_, e) => HideToTrayWhenClosing(e);
        InitializeNativeViews(toolbar);
        InitializeReservationDeselection();
        InitializeHelpPage();
        ApplyFlatSurfaces(this);
        InitializeWindowPlacement();
        Load += (_, _) => LoadNativeData();
        Shown += (_, _) => StartInTrayIfRequested();
    }

    private static string RecommendationHome => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVRockZoo");
    private void InitializeRecommendations()
    {
        var menu = new ContextMenuStrip();
        var enabled = new ToolStripMenuItem("3日ごとの自動おすすめ予約") { CheckOnClick = true, Checked = !File.Exists(Path.Combine(RecommendationHome, "recommend-disabled.txt")) };
        _recommendToggle.Checked = enabled.Checked;
        void ShowMode() { _recommendToggle.Text = "自動おすすめ予約 " + (enabled.Checked ? "ON" : "OFF"); _recommendToggle.BackColor = enabled.Checked ? Color.FromArgb(218,238,228) : Color.FromArgb(231,238,247); _recommendTip.SetToolTip(_recommendToggle,"検索条件・予約履歴からPC内で選定し、3日ごとに最大20件を予約。OFFでも既存の予約は残ります。ChatGPTの設定ではありません。"); }
        bool restoringMode = false;
        enabled.CheckedChanged += (_, _) => { if(restoringMode) return; try { RecommendationSwitch.Save(RecommendationHome,enabled.Checked); } catch(Exception error) { MessageBox.Show(this,error.Message,"おすすめ設定を保存できません"); restoringMode = true; enabled.Checked = !enabled.Checked; _recommendToggle.Checked = enabled.Checked; restoringMode = false; return; } _recommendToggle.Checked = enabled.Checked; ShowMode(); };
        _recommendToggle.CheckedChanged += (_, _) => { if(enabled.Checked != _recommendToggle.Checked) enabled.Checked = _recommendToggle.Checked; };
        Shown += (_, _) => ShowMode(); ShowMode();
        menu.Items.Add(enabled);
        menu.Items.Add("接続設定", null, (_, _) => { using var dialog = new SetupDialog(); if(dialog.ShowDialog(this)==DialogResult.OK) MessageBox.Show(this,"設定を保存しました。アプリを終了して起動し直してください。","接続設定"); });
        menu.Items.Add("おすすめの履歴", null, (_, _) => { Directory.CreateDirectory(Path.Combine(RecommendationHome,"recommend-history")); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(RecommendationHome,"recommend-history")){UseShellExecute=true}); });
        _recommend.ContextMenuStrip = menu;
        _recommend.Click += async (_, _) => await RunRecommendationsAsync();
        _recommend.MouseEnter += (_, _) => RefreshRecommendationStatus();
        _recommendTimer.Tick += async (_, _) => { RefreshRecommendationStatus(); await RunDueRecommendationsAsync(); };
        _recommendTimer.Start();
        Shown += async (_, _) => await RunDueRecommendationsAsync();
        RefreshRecommendationStatus();
    }
    private void RefreshRecommendationStatus()
    {
        try
        {
            var file = Path.Combine(RecommendationHome,"recommend-status.json");
            if (!File.Exists(file)) { _recommendTip.SetToolTip(_recommend,"検索条件・予約履歴からPC内で選定。3日ごとに最大20件。右クリックで自動予約を停止できます。"); return; }
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            var message = root.GetProperty("message").GetString() ?? "";
            var stamp = root.GetProperty("time").GetString() ?? "";
            _recommendTip.SetToolTip(_recommend,message+"\n3日ごとに最大20件。右クリックで履歴と自動実行の設定。");
            if(stamp!=_lastRecommendationStamp && root.GetProperty("state").GetString()=="complete")
            { if(_webBrowser.Source?.AbsolutePath==new Uri(Zoo.RuntimeConfiguration.BaseUri,"list").AbsolutePath) _webBrowser.CoreWebView2?.Reload(); }
            if(stamp!=_lastRecommendationStamp) LoadNativeReservations();
            _lastRecommendationStamp=stamp;
        }
        catch(IOException) { }
        catch(System.Text.Json.JsonException) { }
    }
    private async Task RunDueRecommendationsAsync()
    {
        if(!_recommend.Enabled || DateTimeOffset.Now<_nextRecommendationAttempt || File.Exists(Path.Combine(RecommendationHome,"recommend-disabled.txt"))) return;
        try { var file=Path.Combine(RecommendationHome,"recommend-state.json"); if(File.Exists(file)){using var doc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));if(doc.RootElement.TryGetProperty("nextDue",out var due)&&due.ValueKind==System.Text.Json.JsonValueKind.String&&due.GetDateTimeOffset()>DateTimeOffset.Now)return;} _nextRecommendationAttempt=DateTimeOffset.Now.AddMinutes(30); await RunRecommendationsAsync(true); }
        catch(Exception error){_nextRecommendationAttempt=DateTimeOffset.Now.AddMinutes(30);Log("おすすめ自動実行を延期: "+error.Message);}
    }
    private async Task RunRecommendationsAsync(bool scheduled=false)
    {
        var executable=Path.Combine(AppContext.BaseDirectory,"Recommend","TsRecommend.exe");
        if(!File.Exists(executable)){MessageBox.Show(this,"おすすめ処理の実行ファイルが見つかりません。");return;}
        _recommend.Enabled=false;_recommend.Text="おすすめ選定中…";
        try
        {
            using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable,scheduled?"--apply --scheduled":"--apply"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(executable)})!;
            await process.WaitForExitAsync();RefreshRecommendationStatus();
            if(!scheduled && process.ExitCode!=0)MessageBox.Show(this,"おすすめ処理を完了できませんでした。ボタンにマウスを置くと詳細を確認できます。","おすすめ更新");
        }
        catch(Exception error){if(scheduled)Log("おすすめ自動実行に失敗: "+error.Message);else MessageBox.Show(this,error.Message,"おすすめ更新");}
        finally{_recommend.Text="おすすめ更新";_recommend.Enabled=true;}
    }
    private void Import()
    {
        using var dialog = new FolderBrowserDialog { Description = "tvrock.sch と dtv.ini が入ったフォルダを選択してください" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var imported = TvRockImport.Load(dialog.SelectedPath);
            _tuners.DataSource = imported.Tuners.Select(tuner => new { tuner.Number, tuner.ApplicationPath, tuner.ApplicationName, tuner.Arguments, tuner.RecordingPath }).ToList();
            _reservations.DataSource = imported.Reservations.Select(reservation => new { Start = reservation.Start.DateTime, End = reservation.End.DateTime, reservation.Station, reservation.Title, reservation.ServiceId }).ToList();
            _loadedReservations = imported.Reservations;
            _reservationList.DataSource = imported.Reservations.Select(reservation => new
            {
                日時 = reservation.Start.ToString("yyyy/MM/dd HH:mm"),
                終了 = reservation.End.ToString("HH:mm"),
                チャンネル = reservation.Station,
                タイトル = reservation.Title
            }).ToList();
            foreach (DataGridViewColumn column in _reservationList.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _announcedReservations.Clear();
            _scheduler.Enabled = true;
            Log($"読み込み完了: チューナー {imported.Tuners.Count}台、予約 {imported.Reservations.Count}件");
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "読み込みに失敗しました", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OpenWebAsync()
    {
        if (!Uri.TryCreate(_webAddress.Text.Trim(), UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "http:// または https:// で始まるURLを入力してください。", "URLが正しくありません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            _tabs.SelectedTab = _tabs.TabPages.Cast<TabPage>().First(p => p.Controls.Contains(_webBrowser));
            await _webBrowser.EnsureCoreWebView2Async();
            if (!_webStyleInstalled)
            {
                await _webBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("""
                    window.addEventListener('DOMContentLoaded', () => {
                      window.__applyTsSchedulerStyle?.();
                    });
                    """.Replace("/user/",Zoo.RuntimeConfiguration.BaseUri.AbsolutePath));
                _webStyleInstalled = true;
            }
            _webBrowser.CoreWebView2.Navigate(address.AbsoluteUri);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(this, "Microsoft Edge WebView2 Runtime が必要です。Microsoft Edge を最新化してから、もう一度起動してください。", "WebView2 Runtime が見つかりません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task OpenTabAsync(WebView2 browser, string page)
    {
        if (!Uri.TryCreate(_webAddress.Text.Trim(), UriKind.Absolute, out var address)) return;
        try
        {
            await browser.EnsureCoreWebView2Async();
            browser.CoreWebView2.Navigate(new Uri(Zoo.RuntimeConfiguration.BaseUri, page).AbsoluteUri);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "画面を開けませんでした", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void WebNavigationCompleted(WebView2 browser, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess && browser.CoreWebView2 is not null)
            await browser.CoreWebView2.ExecuteScriptAsync("""
                (() => {
                  window.__applyTsSchedulerStyle = () => {
                    document.getElementById('ts-scheduler-tvrock-style')?.remove();
                    const style = document.createElement('style');
                    style.id = 'ts-scheduler-tvrock-style';
                    style.textContent = '';
                    const isList = location.pathname === '/user/list'
                      || location.pathname === '/user/kwl'
                      || location.pathname === '/user/kws';
                    if (isList) {
                      style.textContent += `
                        table { width: 100% !important; }
                        html { overflow-x: hidden !important; }
                        body {
                          box-sizing: border-box !important;
                          margin: 8px 26px 8px 8px !important;
                          max-width: calc(100% - 26px) !important;
                        }
                        body > table { max-width: 100% !important; }
                        body, table, tr, td, th, td[align], th[align], a, small, font {
                          text-align: left !important;
                        }
                        tr[bgcolor="#80a4e0"] { display: none !important; }
                        br { display: none !important; }
                        table { border-collapse: collapse; border-spacing: 0; }
                        * { font-family: "Meiryo UI", sans-serif; font-size: 14px; }
                        td { white-space: nowrap; }
                        table:has(> tbody > tr > td[width="25%"])
                          > tbody > tr > td:nth-child(1),
                        table:has(> tbody > tr > td[width="25%"])
                          > tbody > tr > td:nth-child(n + 3):nth-child(-n + 7) { display: none !important; }
                        table:has(> tbody > tr > td[width="25%"])
                          { table-layout: auto !important; }
                        .ts-date-separator > td { border-top: 3px solid #7c89a8 !important; }
                        .ts-search-column {
                          width: 10em !important;
                          max-width: 10em !important;
                          overflow: hidden !important;
                          text-overflow: ellipsis !important;
                          white-space: nowrap !important;
                        }
                        .ts-search-column a {
                          display: block !important;
                          overflow: hidden !important;
                          text-overflow: ellipsis !important;
                          white-space: nowrap !important;
                        }
                      `;
                    }
                    if (location.pathname === '/user/kwl' || location.pathname === '/user/kws') style.textContent += 'td { white-space: pre-line; }';
                    if (location.pathname === '/user/now') {
                      style.textContent += `
                        .ts-hour-marker {
                          text-align: left !important;
                          padding-left: 6px !important;
                          padding-right: 0 !important;
                          font-weight: 400 !important;
                        }
                        .ts-hour-marker a,
                        .ts-hour-marker small,
                        .ts-hour-marker font,
                        .ts-hour-marker b {
                          display: block !important;
                          width: 100% !important;
                          box-sizing: border-box !important;
                          text-align: left !important;
                          font-weight: 400 !important;
                        }
                      `;
                    }
                    if (location.pathname === '/user/reg') {
                      style.textContent += `
                        html, body { max-width: 100% !important; overflow-x: hidden !important; }
                        body > table { width: 100% !important; table-layout: fixed !important; }
                        body > table > tbody > tr:last-child > td,
                        body > table > tbody > tr:last-child > td small {
                          white-space: normal !important;
                          overflow-wrap: anywhere !important;
                          word-break: break-word !important;
                        }
                        .ts-offset-control { display: inline-flex; align-items: center; gap: 4px; }
                        .ts-offset-control input { width: 4.5em; }
                      `;
                    }
                    (document.head || document.documentElement).appendChild(style);

                    if (location.pathname === '/user/now') {
                      document.querySelectorAll('td[rowspan][align="center"]').forEach(cell => {
                        const label = cell.textContent.replace(/\s+/g, '');
                        if (/^(?:[01]?\d|2[0-3])時$/.test(label)) cell.classList.add('ts-hour-marker');
                      });
                    }

                    if (location.pathname === '/user/list') {
                      const reservationTable = document.querySelector('table:has(> tbody > tr > td[width="25%"])');
                      let previousDate = '';
                      reservationTable?.querySelectorAll(':scope > tbody > tr').forEach(row => {
                        const dateCell = row.querySelector('td[width="25%"]');
                        const match = dateCell?.textContent.match(/\d+月\d+日/);
                        if (!match) return;
                        if (previousDate && previousDate !== match[0]) row.classList.add('ts-date-separator');
                        previousDate = match[0];
                      });
                      void fetch(new URL('kwl', location.href))
                        .then(response => response.arrayBuffer())
                        .then(buffer => new TextDecoder('shift-jis').decode(buffer))
                        .then(html => {
                          const keywordDocument = new DOMParser().parseFromString(html, 'text/html');
                          const keywords = new Map();
                          keywordDocument.querySelectorAll('a[href*="kwl?i="]').forEach(link => {
                            const id = new URL(link.getAttribute('href'), location.href).searchParams.get('i');
                            const details = link.closest('tr')?.querySelector('td[width="34%"]')?.textContent.replace(/\s+/g, ' ').trim();
                            if (id && details) keywords.set(id, details);
                          });

                          reservationTable?.querySelectorAll(':scope > tbody > tr').forEach(row => {
                            const dateCell = row.querySelector('td[width="25%"]');
                            const channelCell = dateCell?.nextElementSibling;
                            const searchCell = row.querySelector('td[width="2%"]');
                            if (!channelCell || !searchCell) return;
                            searchCell.classList.add('ts-search-column');
                            channelCell.after(searchCell);
                            const link = searchCell.querySelector('a[href*="kwl?i="]');
                            if (!link) {
                              searchCell.textContent = '検索';
                              return;
                            }
                            const id = new URL(link.getAttribute('href'), location.href).searchParams.get('i');
                            const keyword = id ? keywords.get(id) : null;
                            const displayKeyword = keyword && [...keyword].length > 10
                              ? [...keyword].slice(0, 10).join('') + '…'
                              : keyword || '検索';
                            link.textContent = displayKeyword;
                            link.title = keyword || '自動検索予約';
                          });
                        })
                        .catch(() => { });
                    }


                    if (location.pathname === '/user/now' || location.pathname === '/user/day') {
                      style.textContent += `
                        td small { font-size: 14px !important; line-height: 1.42 !important; }
                        td small a { font-size: 15px !important; line-height: 1.3 !important; font-weight: 600; }
                      `;
                      // Hide only the navigation tables, never their enclosing programme grid.
                      document.querySelectorAll('table').forEach(table => {
                        if (table.querySelector('table')) return;
                        const text = table.textContent.replace(/\s+/g, '');
                        const isDateNavigation = text.includes('今の時間') && /\d{1,2}時/.test(text);
                        const isGenreNavigation = text.includes('ニュース・報道') && text.includes('スポーツ') && text.includes('その他');
                        if (isDateNavigation || isGenreNavigation) table.style.setProperty('display', 'none', 'important');
                      });
                      const rows = [...document.querySelectorAll('tr')].filter(row => ![...row.cells].some(cell => cell.querySelector('table')));
                      rows.forEach(row => {
                        const text = row.textContent.replace(/\s+/g, '');
                        if (text.includes('ニュース・報道') && text.includes('スポーツ') && text.includes('その他')) {
                          row.style.display = 'none';
                          const secondRow = row.nextElementSibling;
                          if (secondRow?.textContent.includes('アニメ') && secondRow.textContent.includes('趣味・教養'))
                            secondRow.style.display = 'none';
                        }
                      });
                      const displayControls = rows.find(row => {
                        const text = row.textContent.replace(/\s+/g, '');
                        return text.includes('表形式') && text.includes('リスト形式') && text.includes('優先デバイス予約');
                      });
                      if (displayControls) {
                        displayControls.previousElementSibling?.style.setProperty('display', 'none', 'important');
                        displayControls.style.setProperty('display', 'none', 'important');
                        displayControls.nextElementSibling?.style.setProperty('display', 'none', 'important');
                      }
                      rows.forEach(row => {
                        const text = row.textContent.replace(/\s+/g, '');
                        if ((text.includes('曜日') && text.includes('2:00'))
                          || (text.includes('2時') && text.includes('3時') && text.includes('24時')))
                          row.style.setProperty('display', 'none', 'important');
                      });
                    }
                  };
                  window.__applyTsSchedulerStyle();
                  if (location.pathname === '/user/kwl') {
                    for (const table of document.querySelectorAll('table')) {
                      const header = [...table.rows].find(row => {
                        const labels = [...row.cells].map(c => c.textContent.replace(/\s/g, ''));
                        return labels.includes('コンピュータ') && labels.includes('タイトル・詳細');
                      });
                      if (!header) continue;
                      const hidden = new Set(['コンピュータ', 'デバイス', 'アプリ終了', '予定', '次回', '時間', '期限']);
                      const indices = [...header.cells].flatMap((c, i) => hidden.has(c.textContent.replace(/\s/g, '')) ? [i] : []);
                      for (const row of table.rows) {
                        if (row.cells.length !== header.cells.length) continue;
                        for (const i of indices) row.cells[i].style.display = 'none';
                      }
                    }
                  }
                })();
                """);
        if (e.IsSuccess && browser.CoreWebView2 is not null)
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("TsScheduler.ReservationUi.js");
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                await browser.CoreWebView2.ExecuteScriptAsync((await reader.ReadToEndAsync()).Replace("/user/",Zoo.RuntimeConfiguration.BaseUri.AbsolutePath));
            }
        }
        if (e.IsSuccess && browser.CoreWebView2 is not null && browser.Source?.AbsolutePath == new Uri(Zoo.RuntimeConfiguration.BaseUri,"list").AbsolutePath)
        {
            try
            {
                var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVRockZoo", "ai-recommendations.json");
                var entries = File.Exists(file) ? System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(await File.ReadAllTextAsync(file)) : System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("[]");
                if (entries.ValueKind != System.Text.Json.JsonValueKind.Array) throw new InvalidDataException("AIおすすめの記録形式が正しくありません。");
                await browser.CoreWebView2.ExecuteScriptAsync("window.__tsAiRecommendations = " + entries.GetRawText() + ";");
                var duplicateFile=Path.Combine(RecommendationHome,"duplicate-suppressions.json");
                var suppressed=File.Exists(duplicateFile)?System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(await File.ReadAllTextAsync(duplicateFile)):System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("[]");
                if(suppressed.ValueKind==System.Text.Json.JsonValueKind.Array)
                    await browser.CoreWebView2.ExecuteScriptAsync("window.__tsDuplicateSuppressions = "+suppressed.GetRawText()+";");
                using var resource = typeof(MainForm).Assembly.GetManifestResourceStream("TsScheduler.AiRecommendationUi.js");
                if (resource is not null) { using var reader = new StreamReader(resource); await browser.CoreWebView2.ExecuteScriptAsync((await reader.ReadToEndAsync()).Replace("/user/",Zoo.RuntimeConfiguration.BaseUri.AbsolutePath)); }
            }
            catch (Exception error) { Log("AIおすすめ表示の読み込みに失敗: " + error.Message); }
        }
        Log(e.IsSuccess ? $"TVRock Webを表示: {browser.Source}" : $"TVRock Webの表示に失敗しました: {e.WebErrorStatus}");
    }

    private async Task OpenSelectedReservationAsync()
    {
        if (_reservationList.SelectedRows.Count == 0) return;
        var rowIndex = _reservationList.SelectedRows[0].Index;
        if (rowIndex < 0 || rowIndex >= _loadedReservations.Count) return;
        if (!Uri.TryCreate(_webAddress.Text.Trim(), UriKind.Absolute, out var listAddress)) return;

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(listAddress);
            var html = Encoding.GetEncoding(932).GetString(bytes);
            var reservation = _loadedReservations[rowIndex];
            var editLink = FindReservationEditLink(html, reservation);
            if (editLink is null)
            {
                MessageBox.Show(this, "TVRock Web上で一致する予約を見つけられませんでした。TVRockデータを読み込み直してから、もう一度お試しください。", "予約が見つかりません", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _tabs.SelectedTab = _tabs.TabPages.Cast<TabPage>().First(p => p.Controls.Contains(_webBrowser));
            await _webBrowser.EnsureCoreWebView2Async();
            _tabs.SelectedTab = _tabs.TabPages.Cast<TabPage>().First(p => p.Controls.Contains(_webBrowser));
            _webBrowser.CoreWebView2.Navigate(new Uri(listAddress, editLink).AbsoluteUri);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "予約を開けません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string? FindReservationEditLink(string html, Reservation reservation)
    {
        foreach (System.Text.RegularExpressions.Match row in System.Text.RegularExpressions.Regex.Matches(html, "<tr>(?<row>.*?)</tr>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var rowHtml = row.Groups["row"].Value;
            if (!rowHtml.Contains("href=\"reg?i=", StringComparison.OrdinalIgnoreCase)) continue;
            var plainText = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(rowHtml, "<.*?>", "")).Replace("\r", "").Replace("\n", "");
            if (!plainText.Contains(reservation.Title, StringComparison.Ordinal)) continue;
            var link = System.Text.RegularExpressions.Regex.Match(rowHtml, "href=\"(?<link>reg\\?i=[^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (link.Success) return link.Groups["link"].Value;
        }
        return null;
    }

    private void ToggleScheduler()
    {
        _timer.Enabled = !_timer.Enabled;
        _scheduler.Text = _timer.Enabled ? "スケジューラ停止" : "スケジューラ開始";
        Log(_timer.Enabled ? "予約の監視を開始しました。" : "予約の監視を停止しました。");
    }

    private void CheckReservations()
    {
        var now = DateTimeOffset.Now;
        foreach (var reservation in _loadedReservations.Where(item => item.Start <= now && item.End > now && _announcedReservations.Add(item)))
            Log($"録画開始予定: {reservation.Station} — {reservation.Title}");
    }

    private void Log(string message) => _activity.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private static GroupBox CreateGroup(string title, Control content)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill };
        group.Controls.Add(content);
        return group;
    }

    private void InitializeTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("表示", null, (_, _) => ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitFromTray());
        _trayIcon.Icon = _appIcon;
        _trayIcon.Text = "TVRock Zoo Ver.0.1.12";
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
        _trayIcon.Visible = true;
    }

    private void StartInTrayIfRequested()
    {
        if (!_startInTray) return;
        Hide();
        _trayIcon.ShowBalloonTip(1500, "TVRock Zoo Ver.0.1.12", "常駐を開始しました。通知領域のアイコンをクリックすると表示します。", ToolTipIcon.Info);
    }

    private void HideToTrayWhenClosing(FormClosingEventArgs e)
    {
        if (_exitRequested || e.CloseReason == CloseReason.WindowsShutDown) return;
        e.Cancel = true;
        Hide();
        _trayIcon.ShowBalloonTip(1500, "TVRock Zoo Ver.0.1.12", "通知領域で常駐しています。右クリックから終了できます。", ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        if(WindowState==FormWindowState.Minimized)WindowState=_lastNonMinimizedState;
        Activate();
    }

    private void ExitFromTray()
    {
        _exitRequested = true;
        _trayIcon.Visible = false;
        Close();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
    private static Icon CreateApplicationIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var body = new SolidBrush(Color.FromArgb(30, 92, 172));
        using var screen = new SolidBrush(Color.FromArgb(104, 190, 255));
        using var recording = new SolidBrush(Color.FromArgb(232, 72, 72));
        using var pen = new Pen(Color.White, 2);
        graphics.FillRectangle(body, 3, 5, 26, 20);
        graphics.FillRectangle(screen, 6, 8, 20, 13);
        graphics.FillEllipse(recording, 22, 8, 5, 5);
        graphics.DrawLine(pen, 11, 27, 21, 27);
        graphics.DrawLine(pen, 16, 24, 16, 27);
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _saveLayoutDelay.Dispose();
            _nativeTimer.Dispose();
            DisposeReadableDesign();
            _nativeItalic?.Dispose();
            _recommendTimer.Dispose();
            _recommendTip.Dispose();
            _trayIcon.Dispose();
            _appIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}








