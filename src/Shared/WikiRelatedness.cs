using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using TsRecommend;
using ZooStorage;
namespace TsRecommend;

// Remote results are optional evidence, never instructions or executable content.
internal sealed record WikiTerm(string Origin,string Term,string Source,string Relation);
internal sealed class WikiRelatedness
{
 internal sealed class Cached {public DateTimeOffset Fetched{get;set;} public bool Success{get;set;} public List<WikiTerm> Terms{get;set;}=[];public string Error{get;set;}="";}
 static readonly HttpClient Client=CreateClient();
 static HttpClient CreateClient(){var c=new HttpClient{Timeout=TimeSpan.FromSeconds(4),MaxResponseContentBufferSize=2*1024*1024};c.DefaultRequestHeaders.UserAgent.ParseAdd("TVRockZoo/1.3.0 (+https://github.com/newfrontierstudio/zoostudio)");return c;}
 static string Query(string s)=>Regex.Replace(s,@"\s+not\s+.+$","",RegexOptions.IgnoreCase).Trim();
 static bool Useful(string term)=>term.Length>=2&&term.Length<=40&&!Regex.IsMatch(term,@"^[\d０-９.(（]|^[A-Za-z]{1,3}$|^一覧|一覧$|^Category:")&&term is not ("日本" or "世界" or "音楽" or "テレビ" or "人物" or "ウィキペディア" or "漫画" or "映画" or "アニメ" or "新聞" or "出版");
 static async Task<JsonDocument> Get(string url,CancellationToken ct){using var response=await Client.GetAsync(url,ct).ConfigureAwait(false);response.EnsureSuccessStatusCode();return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));}
 internal static async Task<List<WikiTerm>> LoadAsync(string home,IReadOnlyList<string> rules)
 {
  string path=Path.Combine(home,"wiki-related-cache.json");Dictionary<string,Cached> cache;
  try{cache=File.Exists(path)?JsonSerializer.Deserialize<Dictionary<string,Cached>>(DurableJson.Read(path))??new():new();}catch{cache=new();}
  var result=new List<WikiTerm>();int fetched=0;using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(20));
  foreach(string original in rules.Distinct()){
   string query=Query(original);if(query.Length<2||query.Length>80)continue;
   cache.TryGetValue(query,out var entry);bool due=entry==null||DateTimeOffset.UtcNow-entry.Fetched>TimeSpan.FromHours(entry.Success?168:6);
   if(due&&fetched<3&&!budget.IsCancellationRequested){fetched++;try{var terms=await Fetch(query,budget.Token).ConfigureAwait(false);entry=new(){Fetched=DateTimeOffset.UtcNow,Success=true,Terms=terms};cache[query]=entry;}catch(Exception e){if(entry==null||!entry.Success){entry=new(){Fetched=DateTimeOffset.UtcNow,Error=e.GetType().Name};cache[query]=entry;}}}
   if(entry!=null)result.AddRange(entry.Terms.Select(t=>t with{Origin=original}));
  }
  if(fetched>0)try{DurableJson.Write(path,JsonSerializer.Serialize(cache));}catch{}
  return result;
 }
 static async Task<List<WikiTerm>> Fetch(string query,CancellationToken ct,int depth=0)
 {
  var result=new List<WikiTerm>();string entity="";string wp="https://ja.wikipedia.org/w/api.php?action=query&format=json&redirects=1&prop=links|pageprops&plnamespace=0&pllimit=200&titles="+Uri.EscapeDataString(query);
  try{using var doc=await Get(wp,ct).ConfigureAwait(false);if(doc.RootElement.TryGetProperty("query",out var q)&&q.TryGetProperty("pages",out var pages))foreach(var page in pages.EnumerateObject()){
   var value=page.Value;if(value.TryGetProperty("missing",out _))continue;
   if(value.TryGetProperty("pageprops",out var disProps)&&disProps.TryGetProperty("disambiguation",out _)){
    if(depth==0&&value.TryGetProperty("links",out var options))foreach(string suffix in new[]{"県","市","島"})foreach(var option in options.EnumerateArray()){
     string name=option.GetProperty("title").GetString()??"";if(name==query+suffix)return await Fetch(name,ct,1).ConfigureAwait(false);
    }
    return [];
   }
   string title=value.GetProperty("title").GetString()??query;string source="https://ja.wikipedia.org/wiki/"+Uri.EscapeDataString(title);
   if(value.TryGetProperty("pageprops",out var props)&&props.TryGetProperty("wikibase_item",out var id))entity=id.GetString()??"";
   if(value.TryGetProperty("links",out var links))foreach(var link in links.EnumerateArray()){string term=link.GetProperty("title").GetString()??"";if(Useful(term))result.Add(new(query,term,source,"Wikipedia関連項目"));}
  }}catch(HttpRequestException){}
  if(entity.Length==0){using var search=await Get("https://www.wikidata.org/w/api.php?action=wbsearchentities&format=json&language=ja&limit=3&search="+Uri.EscapeDataString(query),ct).ConfigureAwait(false);
   if(search.RootElement.TryGetProperty("search",out var matches))foreach(var match in matches.EnumerateArray())if(match.TryGetProperty("label",out var label)&&Data.Norm(label.GetString()??"")==Data.Norm(query)){entity=match.GetProperty("id").GetString()??"";break;}}
  if(entity.Length>0){using var item=await Get("https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=claims&ids="+entity,ct).ConfigureAwait(false);
   if(item.RootElement.GetProperty("entities").GetProperty(entity).TryGetProperty("claims",out var claims)){
    var related=new Dictionary<string,string>();foreach(var (property,relation) in new[]{("P463","所属"),("P361","構成先"),("P131","地域"),("P17","国"),("P19","出身地"),("P150","行政区画"),("P527","構成要素・メンバー")})if(claims.TryGetProperty(property,out var values))foreach(var claim in values.EnumerateArray()){
     if(claim.GetProperty("mainsnak").TryGetProperty("datavalue",out var dv)&&dv.GetProperty("value").TryGetProperty("id",out var id))related.TryAdd(id.GetString()!,relation);}
    if(claims.TryGetProperty("P463",out var memberships))foreach(var membership in memberships.EnumerateArray().Take(2)){
     if(!membership.GetProperty("mainsnak").TryGetProperty("datavalue",out var dv)||!dv.GetProperty("value").TryGetProperty("id",out var groupId))continue;
     string group=groupId.GetString()!;using var groupDoc=await Get("https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=claims&ids="+group,ct).ConfigureAwait(false);
     var groupEntity=groupDoc.RootElement.GetProperty("entities").GetProperty(group);
     if(groupEntity.TryGetProperty("claims",out var gc)&&gc.TryGetProperty("P527",out var members))foreach(var member in members.EnumerateArray().Take(12))if(member.GetProperty("mainsnak").TryGetProperty("datavalue",out var md)&&md.GetProperty("value").TryGetProperty("id",out var mid)&&mid.GetString()!=entity)related.TryAdd(mid.GetString()!,"所属グループの構成員");
    }
    if(related.Count>0){using var labels=await Get("https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=labels&languages=ja&ids="+string.Join("|",related.Keys.Take(24)),ct).ConfigureAwait(false);
     foreach(var node in labels.RootElement.GetProperty("entities").EnumerateObject())if(node.Value.TryGetProperty("labels",out var ls)&&ls.TryGetProperty("ja",out var ja)){string term=ja.GetProperty("value").GetString()??"";if(Useful(term))result.Insert(0,new(query,term,"https://www.wikidata.org/wiki/"+entity,related[node.Name]));}}
   }}
  return result.DistinctBy(t=>Data.Norm(t.Term)).Take(24).ToList();
 }
}
internal sealed class SatlitePriority
{
 readonly List<(Ranker Ranker,int Order,string Rule)> direct=[];
 readonly List<(WikiTerm Term,int Order)> related=[];
 internal SatlitePriority(IReadOnlyList<string> rules,IReadOnlyList<WikiTerm> wiki){for(int i=0;i<rules.Count;i++){direct.Add((new Ranker([new Taste(rules[i],1.2)]),i,rules[i]));related.AddRange(wiki.Where(w=>w.Origin==rules[i]).Select(w=>(w,i)));related.AddRange(Relatedness.RelatedTerms(rules[i]).Select(t=>(new WikiTerm(rules[i],t,"内蔵関連表","関連"),i)));}}
 internal (int Order,string Reason) Match(Show show){int order=int.MaxValue;string reason="";
  foreach(var rule in direct)if(rule.Order<order&&rule.Ranker.Score(show).Score>=.22){order=rule.Order;reason=$" 優先条件 {order+1}「{rule.Rule}」。";}
  string text=Data.Norm(show.Title+" "+show.Description);
  foreach(var (term,i) in related)if(i<order&&text.Contains(Data.Norm(term.Term))){order=i;reason=$" 優先条件 {i+1}「{term.Origin}」→{term.Term}（{term.Relation}、{term.Source}）。";}
  return(order,reason);
 }
}
