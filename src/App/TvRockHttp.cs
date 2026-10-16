using TsRecommend;
namespace TsScheduler;

internal static class TvRockHttp
{
    static readonly SemaphoreSlim Gate=new(1,1);
    internal static async Task<string> Get(Uri uri,HttpMessageHandler? handler=null)
    {
        var query=Data.Query(uri.PathAndQuery);
        string page=uri.AbsolutePath.Split('/').Last();
        bool safeRead=(page=="reg"&&query.Keys.All(k=>k=="i"))
            ||(page=="day"&&query.Keys.All(k=>new[]{"t","j","k","v"}.Contains(k)))
            ||(page=="kwl"&&query.Keys.All(k=>k=="i"))
            ||(new[]{"list","now"}.Contains(page)&&query.Count==0);
        await Gate.WaitAsync();
        try {
            using var client=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false},handler==null){Timeout=TimeSpan.FromSeconds(8)};
            client.DefaultRequestHeaders.ConnectionClose=true;
            for(int attempt=0;;attempt++){
                try{using var response=await client.GetAsync(uri);response.EnsureSuccessStatusCode();return Data.Cp932.GetString(await response.Content.ReadAsByteArrayAsync()).TrimEnd('\0');}
                catch(Exception e)when(safeRead&&attempt<2&&(e is HttpRequestException or TaskCanceledException)){await Task.Delay(500*(attempt+1));}
            }
        }finally{Gate.Release();}
    }
}
