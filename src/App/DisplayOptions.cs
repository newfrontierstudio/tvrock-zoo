using System.Text.Json;
using TsRecommend;
namespace TsScheduler;
internal sealed class DisplayOptions
{
    public bool Zebra {get;set;}
    public int PaletteRevision {get;set;}
    public Dictionary<string,int> StationColors {get;set;}=[];
    internal static string? TestPath;
    static string PathName=>TestPath??Path.Combine(ConnectionProfiles.Current.Active.Home,"display-options.json");
    static DisplayOptions? current;
    internal static DisplayOptions Current=>current??=Read();
    static DisplayOptions Read(){try{var value=File.Exists(PathName)?JsonSerializer.Deserialize<DisplayOptions>(File.ReadAllText(PathName))??new():new();value.StationColors??=[];if(value.PaletteRevision<1){if(value.StationColors.TryGetValue("日本テレビ",out int old)){var c=Color.FromArgb(old);if(c.R>c.G&&c.R>c.B)value.StationColors.Remove("日本テレビ");}value.PaletteRevision=1;}return value;}catch{return new();}}
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
        var zebra=new CheckBox{Text="予約一覧のゼブラ背景",AutoSize=false,Width=300,Height=36,TextAlign=ContentAlignment.MiddleLeft,CheckAlign=ContentAlignment.MiddleLeft,Padding=Padding.Empty,Margin=new Padding(0,10,0,10),Checked=DisplayOptions.Current.Zebra};body.Controls.Add(zebra);
        zebra.CheckedChanged+=(_,_)=>{try{DisplayOptions.Current.Zebra=zebra.Checked;DisplayOptions.Current.Save();ApplyDisplayOptions();}catch(Exception e){MessageBox.Show(this,e.Message,"表示設定を保存できません");}};
        InitializeStationColorEditor(body);
        ApplyDisplayOptions();
    }
}
