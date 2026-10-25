using System.Text.Json;
using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    bool _keywordDeleteSending;
    void InitializeKeywordConfirmation()
    {
        _webBrowser.CoreWebView2InitializationCompleted+=(_,e)=>{
            if(!e.IsSuccess)return;
            _webBrowser.CoreWebView2.WebMessageReceived+=async(_,message)=>{
                if(_keywordDeleteSending)return;
                try {
                    var origin=ConnectionProfiles.Current.Active.BaseUri;
                    if(!Uri.TryCreate(message.Source,UriKind.Absolute,out var source)||source.Authority!=origin.Authority||source.Scheme!=origin.Scheme||source.AbsolutePath!=origin.AbsolutePath+"kwl")return;
                    using var json=JsonDocument.Parse(message.WebMessageAsJson);
                    if(!json.RootElement.TryGetProperty("type",out var type)||type.GetString()!="zoo-keyword-delete")return;
                    var fields=json.RootElement.GetProperty("fields").Deserialize<Dictionary<string,string>>()??[];
                    if(!fields.TryGetValue("i",out var id)||!long.TryParse(id,out long parsedId)||fields.GetValueOrDefault("submit")?.Trim()!="はい"||!fields.ContainsKey("z"))throw new InvalidDataException("削除確認フォームを読み直してください。");
                    var beforeKeys=ReadKeywords(Path.Combine(RockFolder,"tvrock.key")); if(!beforeKeys.Any(k=>k.Id==id))throw new InvalidOperationException("この検索予約はすでに変更されています。一覧を読み直してください。");
                    _keywordDeleteSending=true;
                    string folder=Path.Combine(RecommendationHome,"keyword-changes",DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(folder);File.Copy(Path.Combine(RockFolder,"tvrock.key"),Path.Combine(folder,"before.key"));
                    Exception? transport=null;
                    try{await NativeGet("kwl?"+string.Join("&",fields.Select(p=>Data.Encode(p.Key)+"="+Data.Encode(p.Value))));}catch(Exception error)when(error is HttpRequestException or TaskCanceledException){transport=error;}
                    bool removed=false;for(int attempt=0;attempt<12;attempt++){await Task.Delay(250);var afterKeys=ReadKeywords(Path.Combine(RockFolder,"tvrock.key"));removed=afterKeys.Count==beforeKeys.Count-1&&!afterKeys.Any(k=>k.Id==id)&&beforeKeys.Where(k=>k.Id!=id).All(k=>afterKeys.Any(a=>a.Id==k.Id));if(removed)break;}
                    if(!removed)throw new IOException("削除の反映を確認できませんでした。二重送信はせず、一覧を読み直します。",transport);
                    int row=_nativeKeywords.FirstDisplayedScrollingRowIndex,horizontal=_nativeKeywords.HorizontalScrollingOffset;using(var redraw=((StableGrid)_nativeKeywords).Freeze()){LoadNativeData();_nativeKeywords.CurrentCell=null;_nativeKeywords.ClearSelection();if(row>=0&&_nativeKeywords.Rows.Count>0)_nativeKeywords.FirstDisplayedScrollingRowIndex=Math.Min(row,_nativeKeywords.Rows.Count-1);_nativeKeywords.HorizontalScrollingOffset=horizontal;}
                    _webBrowser.CoreWebView2.Navigate(new Uri(origin,"kwl").AbsoluteUri);
                }catch(Exception error){Log("自動検索予約の削除: "+error);MessageBox.Show(this,error.Message,"自動検索予約の削除");_webBrowser.CoreWebView2.Navigate(new Uri(ConnectionProfiles.Current.Active.BaseUri,"kwl").AbsoluteUri);}
                finally{_keywordDeleteSending=false;}
            };
        };
    }
}
