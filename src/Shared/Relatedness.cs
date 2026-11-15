using System.Reflection;
using System.Text.Json;
using TsRecommend;
namespace TsRecommend;

// Bundled relatedness seed: what is near what, decided at generation time,
// never at run time (the app makes no network calls for this). Places are
// near when they share attribute tags (南国・島・あったかい…); people are
// near when they share a group (Wikidata membership, CC0). Satlite widens
// a taste to its neighbours so a high-priority theme pulls them in with
// it, and keyword rules carry their file-order priority into Satlite as
// a weight gradient.
internal static class Relatedness
{
    internal sealed class Place{public string Name{get;set;}="";public List<string> Tags{get;set;}=[];}
    internal sealed class Seed{public List<Place> Places{get;set;}=[];public Dictionary<string,List<string>> Groups{get;set;}=[];}
    static readonly Seed SeedData=Load();
    static Seed Load()
    {
        try{using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ZOOstudio.related-seed.json");if(stream==null)return new();using var reader=new StreamReader(stream);return JsonSerializer.Deserialize<Seed>(reader.ReadToEnd(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new();}
        catch{return new();}
    }
    internal static int PlaceCount=>SeedData.Places.Count;
    internal static int PeopleCount=>SeedData.Groups.Values.Sum(g=>g.Count);

    internal static List<string> RelatedTerms(string text)
    {
        var result=new List<string>();if(string.IsNullOrWhiteSpace(text))return result;
        string norm=Data.Norm(text);
        foreach(var place in SeedData.Places)
        {
            if(place.Name.Length==0||!norm.Contains(Data.Norm(place.Name)))continue;
            foreach(var other in SeedData.Places.Where(p=>p.Name!=place.Name).Select(p=>(p.Name,Shared:place.Tags.Intersect(p.Tags).Count())).Where(x=>x.Shared>=3).OrderByDescending(x=>x.Shared).ThenBy(x=>x.Name,StringComparer.Ordinal).Take(6))
                if(!result.Contains(other.Name))result.Add(other.Name);
        }
        foreach(var members in SeedData.Groups.Values)
            foreach(string member in members)
            {
                if(member.Length<2||!text.Contains(member))continue;
                foreach(string peer in members.Where(m=>m!=member).Take(8))
                    if(!result.Contains(peer))result.Add(peer);
            }
        return result;
    }

    internal static List<Taste> Expand(IReadOnlyList<Taste> tastes)
    {
        var result=tastes.ToList();var seen=result.Select(t=>Data.Norm(t.Text)).ToHashSet();
        foreach(var taste in tastes)
        {
            if(result.Count>=tastes.Count+24)break;
            foreach(string term in RelatedTerms(taste.Text))
                if(result.Count<tastes.Count+24&&seen.Add(Data.Norm(term)))result.Add(new Taste(term,Math.Round(taste.Weight*0.85,3)));
        }
        return result;
    }

    internal static double PriorityWeight(int index,int count)=>count<=1?1.5:1.5-0.3*index/(count-1);
}
