using System.Text.Json;
using TsRecommend;
namespace TsScheduler;
internal sealed class DisplayOptions
{
    public bool Zebra {get;set;}
    public bool ManualTunerSelection {get;set;}
    public bool ReservationDetailsPopup {get;set;}=true;
    public bool BroadcastSymbols {get;set;}
    public bool UpdateNotifications {get;set;}
    public bool GuideDetailsPopup {get;set;}=true;
    public int PaletteRevision {get;set;}
    public Dictionary<string,int> StationColors {get;set;}=[];
    public HashSet<string> ExactStationColors {get;set;}=[];
    internal static string? TestPath;
    static string PathName=>TestPath??Path.Combine(ConnectionProfiles.Current.Active.Home,"display-options.json");
    static DisplayOptions? current;
    internal static DisplayOptions Current=>current??=Read();
    static DisplayOptions Read(){try{var value=File.Exists(PathName)?JsonSerializer.Deserialize<DisplayOptions>(File.ReadAllText(PathName))??new():new();value.StationColors??=[];value.ExactStationColors??=[];if(value.PaletteRevision<1){if(value.StationColors.TryGetValue("日本テレビ",out int old)){var c=Color.FromArgb(old);if(c.R>c.G&&c.R>c.B)value.StationColors.Remove("日本テレビ");}value.PaletteRevision=1;}return value;}catch{return new();}}
    internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(this));File.Move(PathName+".tmp",PathName,true);}
}
internal sealed partial class MainForm
{
    void ApplyDisplayOptions()
    {
        foreach(var grid in new[]{_nativeReservations,_disabledReservations,_nativeGuide,_nativeKeywords,_consultResults}){
            grid.AlternatingRowsDefaultCellStyle.BackColor=DisplayOptions.Current.Zebra?Color.FromArgb(245,248,252):Color.White;
            if(grid==_nativeReservations||grid==_disabledReservations)grid.CellBorderStyle=DataGridViewCellBorderStyle.None;
            grid.Invalidate();
        }_schedule.Invalidate();
    }
    void InitializeDisplayOptions()
    {
        _detailLanding.AutoScroll=true;var body=_detailLanding.Controls.OfType<FlowLayoutPanel>().First();body.AutoSize=true;body.WrapContents=false;
        if(detailIndex==null){detailIndex=new FlowLayoutPanel{AutoSize=true,WrapContents=true,Margin=new Padding(0,4,0,16)};body.Controls.Add(detailIndex);body.Controls.SetChildIndex(detailIndex,2);}
        var guide=DetailSection(body,"番組表・詳細表示","番組表の取得と録画はTVRockが担当します。");InitializeGuidePopupOptions(guide);InitializeBroadcastSymbols(guide);
        var appearance=DetailSection(body,"画面・操作","一覧と局の表示、マウス操作を設定します。");
        var zebra=new CheckBox{Text="予約一覧のゼブラ背景",AutoSize=false,Width=300,Height=36,TextAlign=ContentAlignment.MiddleLeft,CheckAlign=ContentAlignment.MiddleLeft,Padding=Padding.Empty,Margin=new Padding(0,10,0,10),Checked=DisplayOptions.Current.Zebra};appearance.Controls.Add(zebra);
        zebra.CheckedChanged+=(_,_)=>{try{DisplayOptions.Current.Zebra=zebra.Checked;DisplayOptions.Current.Save();ApplyDisplayOptions();}catch(Exception e){MessageBox.Show(this,e.Message,"表示設定を保存できません");}};

        var booking=DetailSection(body,"予約の操作","空きチューナーを自動選択します。既存予約の再配置には確認を表示します。");var manual=new CheckBox{Text="予約時にチューナーを手動選択",AutoSize=true,Checked=DisplayOptions.Current.ManualTunerSelection};booking.Controls.Add(manual);manual.CheckedChanged+=(_,_)=>{DisplayOptions.Current.ManualTunerSelection=manual.Checked;DisplayOptions.Current.Save();};
        InitializeKeywordFilenameOptions(DetailSection(body,"録画ファイル名","TVRockの録画ファイル名に付ける検索ワードを設定します。"));
        InitializeTsRenameSupport(DetailSection(body,"TsRename連携","録画後のファイル名変更に使う外部ツールの設定を確認します。"));
        InitializeDiagnosticLogging(DetailSection(body,"診断","問題を調べるためのログ設定です。"));
        InitializeStationColorEditor(appearance);InitializeGestureOptions(appearance);InitializeUpdateNotifications(DetailSection(body,"アップデート","更新通知は初期OFFです。"));StyleControls(body);_detailLanding.SizeChanged+=(_,_)=>FitDetailIndex();FitDetailIndex();BuildModernDetails(body);
        ApplyDisplayOptions();
    }
}
