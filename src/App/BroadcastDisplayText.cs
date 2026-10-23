namespace TsScheduler;
internal static class BroadcastDisplayText
{
    static readonly (string Symbol,string Text)[] Symbols=[("🈑","[字]"),("🈓","[デ]"),("🈒","[双]"),("🈞","[再]"),("🈟","[新]"),("🈡","[終]"),("🈢","[生]"),("🈕","[多]"),("🈖","[解]"),("🈚","[無]"),("🈶","[有]"),("🈥","[吹]")];
    internal static string Format(string text,bool enabled){if(!enabled)return text;foreach(var pair in Symbols)text=text.Replace(pair.Symbol,pair.Text,StringComparison.Ordinal);return text;}
    internal static string Format(string text)=>Format(text,DisplayOptions.Current.BroadcastSymbols);
}
internal sealed partial class MainForm
{
    void InitializeBroadcastSymbols(FlowLayoutPanel body)
    {
        var toggle=new CheckBox{Text="放送記号の表示を補正（ARIB外字）",AutoSize=true,Checked=DisplayOptions.Current.BroadcastSymbols};body.Controls.Add(toggle);
        body.Controls.Add(DetailNote("初期値はOFFです。番組表・予約一覧・詳細の「🈑」などを「[字]」に置き換えて表示します。検索条件と保存データは変更しません。"));
        bool restoring=false;toggle.CheckedChanged+=(_,_)=>{if(restoring)return;bool old=DisplayOptions.Current.BroadcastSymbols;try{DisplayOptions.Current.BroadcastSymbols=toggle.Checked;DisplayOptions.Current.Save();HideLargeTip();ApplyDisplayOptions();}catch(Exception e){DisplayOptions.Current.BroadcastSymbols=old;restoring=true;toggle.Checked=old;restoring=false;MessageBox.Show(this,e.Message,"表示設定を保存できません");}};
        foreach(var grid in new[]{_nativeReservations,_disabledReservations,_nativeGuide,_consultResults})grid.CellFormatting+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name=="title"&&e.Value is string text)e.Value=BroadcastDisplayText.Format(text);};
    }
}
