using System.Text.Json;
using GojoQuickDraw.Reads;

namespace GojoQuickDraw.Desktop;

// Old archives expose validated geometry caches too. This fallback uses the
// same geometric observer as native readback; it does not rewrite an archive.
public static class JointPreview
{
    public static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true};
    private static JsonElement[] Rows(JsonElement parent,string key)=>parent.ValueKind==JsonValueKind.Object && parent.TryGetProperty(key,out var rows) && rows.ValueKind==JsonValueKind.Array ? rows.EnumerateArray().ToArray() : [];
    private static string Text(JsonElement p,string key,string fallback="")=>p.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.String ? v.GetString()! : fallback;
    private static double[] Point(JsonElement p)=>p.EnumerateArray().Select(n=>n.GetDouble()).ToArray();
    public static JointComparison Read(JsonElement packet)
    {
        if(packet.TryGetProperty("jointComparison",out var saved) && saved.ValueKind==JsonValueKind.Object)
            return JsonSerializer.Deserialize<JointComparison>(saved,Options) ?? throw new InvalidDataException("Saved joint comparison is unreadable.");
        return ReadLegacy(packet);
    }
    public static JointComparison ReadLegacy(JsonElement packet)
    {
        var observed=packet.GetProperty("observation");var spans=Rows(observed,"spans");
        var profiles=spans.Select((s,i)=>(Id:Text(s,"id",i.ToString()),Index:"p"+i)).ToDictionary(p=>p.Id,p=>p.Index);
        string[] Sources(JsonElement row)=>Rows(row,"spans").Select(s=>profiles.GetValueOrDefault(s.GetString()!,s.GetString()!)).Distinct().Order().ToArray();
        var source=packet.TryGetProperty("annotations",out var annotations) ? Rows(annotations,"joints").Select((j,i)=>new JointNode(
            "source-j"+i,JointGraphRead.Normalize(Text(j,"label","unresolved")),j.TryGetProperty("point",out var point) ? Point(point) : [0,0],
            Rows(j,"rays").Select(r=>r.GetProperty("angleDegrees").GetDouble()).ToArray(),
            Rows(j,"participants").Select(p=>new JointAt(profiles.GetValueOrDefault(Text(p,"spanId"),Text(p,"spanId")),p.GetProperty("t").GetDouble())).ToArray(),
            Sources(j),j.TryGetProperty("observedSharedSite",out var shared)&&shared.GetBoolean(),j.TryGetProperty("gap",out var gap) ? gap.GetDouble() : 0,
            "saved source annotation; proximity is not shared identity")).ToArray() : [];
        var edges=new List<JointEdge>();
        if(packet.TryGetProperty("cleanup",out var cleanup) && cleanup.ValueKind==JsonValueKind.Object)
        {
            var final=Rows(cleanup,"parts");var context=packet.TryGetProperty("context",out var ctx) ? Rows(ctx,"parts") : final;
            var parts=context.Where(p=>p.TryGetProperty("id",out _)).ToDictionary(p=>Text(p,"id"));
            foreach(var part in final)parts[Text(part,"id")]=part;
            string Site(string id,int index,HashSet<string>? visited=null)
            {
                var key=id+"/"+index;visited ??=[];if(!visited.Add(key))return key;
                if(parts.TryGetValue(id,out var p) && p.TryGetProperty("geometry",out var g))
                {
                    var aliases=Rows(g,"siteSources");
                    if(index<aliases.Length && aliases[index].ValueKind==JsonValueKind.Object)
                        return Site(Text(aliases[index],"part"),aliases[index].GetProperty("index").GetInt32(),visited);
                }
                return key;
            }
            var targets=packet.TryGetProperty("boundaryMapping",out var mapping) ? Rows(mapping,"targets") : [];
            var correspondences=Rows(cleanup,"correspondences");
            for(var pi=0;pi<final.Length;pi++)
            {
                var part=final[pi];var id=Text(part,"id");if(!part.TryGetProperty("geometry",out var geo))continue;
                var points=Rows(geo,"points").Select(Point).ToArray();var segments=Rows(geo,"segments");
                var shown=geo.TryGetProperty("displaySegments",out var chosen) ? chosen.EnumerateArray().Select(v=>v.GetInt32()).ToArray() : Enumerable.Range(0,segments.Length).ToArray();
                var correspondence=correspondences.FirstOrDefault(c=>Text(c,"target")==id);
                var pairs=Rows(correspondence,"points");
                var added=correspondence.ValueKind!=JsonValueKind.Object ||
                    (parts.TryGetValue(Text(correspondence,"source"),out var prior) && Rows(prior,"spans").Length==0 &&
                     !targets.Any(t=>Text(t,"part")==Text(correspondence,"source")&&Rows(t,"ink").Length>0));
                foreach(var ei in shown)
                {
                    var a=segments[ei][0].GetInt32();var b=segments[ei][1].GetInt32();var support=new HashSet<string>();
                    if(correspondence.ValueKind==JsonValueKind.Object)
                    {
                        var oldId=Text(correspondence,"source");var pa=pairs.FirstOrDefault(p=>p[1].GetInt32()==a);var pb=pairs.FirstOrDefault(p=>p[1].GetInt32()==b);
                        if(pa.ValueKind==JsonValueKind.Array && pb.ValueKind==JsonValueKind.Array && parts.TryGetValue(oldId,out var previous) && previous.TryGetProperty("geometry",out var pg))
                        {
                            var oldSegments=Rows(pg,"segments");var sa=Site(oldId,pa[0].GetInt32());var sb=Site(oldId,pb[0].GetInt32());
                            foreach(var target in targets.Where(t=>Text(t,"part")==oldId))
                            {
                                bool Selected(int k){var e=oldSegments[k];var l=Site(oldId,e[0].GetInt32());var r=Site(oldId,e[1].GetInt32());return l==sa&&r==sb || l==sb&&r==sa;}
                                if(!Rows(target,"segments").Any(v=>Selected(v.GetInt32())))continue;
                                foreach(var ink in Rows(target,"ink"))support.Add(profiles.GetValueOrDefault(Text(ink,"span"),Text(ink,"span")));
                            }
                        }
                    }
                    edges.Add(new($"f{pi}/e{ei}","f"+pi,points[a],points[b],Site(id,a),Site(id,b),support.Order().ToArray(),added));
                }
            }
        }
        return JointGraphRead.Compare(JointGraphRead.Graph(source),JointGraphRead.Final(edges.ToArray()),
            JointGraphRead.Version+"; read-only projection from older saved geometry; no native re-encoding",edges.ToArray());
    }
}
