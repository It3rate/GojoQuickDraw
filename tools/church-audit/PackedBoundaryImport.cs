using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Encoding.Structures;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using R=Gojo.Core.GojoEncodedElementRef;
using K=Gojo.Core.GojoEncodedElementKind;

/// <summary>Compiles the same native boundary import into transport tables.
/// Original rows are an unchanged prefix. Temporary role proxies are eliminated
/// by binding their references to actual source-table entries before export.
/// No existing value, identity, attachment, relation order or tick is rewritten.</summary>
public static class PackedBoundaryImport
{
    public sealed class Reader(GojoEncodedSnapshot graph)
    {
        public GojoEncodedSnapshot Graph=>graph;
        public GojoEncodedRelation Relation(R r)=>r.Kind==K.Relation?graph.Relations[r.Index]:throw new InvalidDataException("Expected an ordered native relation.");
        public R[] Group(R root)
        {
            var group=Relation(root);if(group.Right.Kind!=K.Carrier)throw new InvalidDataException("Expected a native ordered group holder.");
            var holder=group.Right;if(Point(group.Left,holder,1))return [];
            var rows=new List<R>();var stack=new Stack<(R Ref,bool Leaving)>();var active=new HashSet<R>();stack.Push((group.Left,false));
            while(stack.TryPop(out var item))
            {
                if(item.Leaving){active.Remove(item.Ref);continue;}
                if(!active.Add(item.Ref))throw new InvalidDataException("A native group contains a cyclic branch.");
                stack.Push((item.Ref,true));
                var r=Relation(item.Ref);if(Point(r.Left,holder,0))rows.Add(r.Right);else{stack.Push((r.Right,false));stack.Push((r.Left,false));}
            }
            return rows.ToArray();
        }
        private bool Point(R point,R holder,long ticks)
        {
            if(point.Kind!=K.Relation)return false;var p=Relation(point);
            if(p.Right!=holder||p.Left.Kind!=K.CarrierLocation)return false;
            var location=graph.CarrierLocations[p.Left.Index];return location.CarrierIndex==holder.Index&&graph.TickRefs[location.TickRefIndex].Ticks==ticks;
        }
    }

    public static bool IsSealed(GojoEncodedSnapshot graph)
    {
        var read=new Reader(graph);var f=read.Group(graph.Root);
        return f.Length==2&&read.Group(f[1]).Length==5&&read.Group(f[1])[0]==f[0];
    }

    public static (GojoEncodedSnapshot Graph,int Edges) Create(GojoEncodedSnapshot source,JsonElement run,
        IReadOnlyDictionary<string,JsonElement> declared,string beforeHash)
    {
        if(source.Roots.Length!=1)throw new InvalidDataException("A numerical church archive has one source root.");
        var reader=new Reader(source);var fields=reader.Group(source.Root);if(fields.Length!=5)throw new InvalidDataException("Expected a retained numerical church run.");
        var stages=run.GetProperty("stages").EnumerateArray().ToArray();var additions=new List<IElement>();var edgeCount=0;
        var proxies=new Dictionary<R,Placeholder>();
        Placeholder Proxy(R r){if(!proxies.TryGetValue(r,out var p))proxies.Add(r,p=new Placeholder());return p;}
        var rounds=reader.Group(fields[2]);
        foreach(var (pass,index) in rounds.Select((p,i)=>(p,i+1)))
        {
            var row=reader.Group(pass);if(row.Length!=5)continue;
            var parts=reader.Group(reader.Group(row[1])[3]);var expected=stages[index].GetProperty("parts").EnumerateArray().ToArray();
            if(parts.Length!=expected.Length)throw new InvalidDataException("Review/native scaffold counts differ.");
            var imported=new List<IElement>();
            foreach(var (part,i) in parts.Select((p,i)=>(reader.Group(p),i)))
            {
                var producer=part[1];var outputs=reader.Group(producer)[2];var roles=reader.Group(outputs);
                var seed=declared[expected[i].GetProperty("id").GetString()!];var topology=seed.GetProperty("geometry").GetProperty("segments").Deserialize<int[][]>()!;
                var cached=expected[i].GetProperty("segments").Deserialize<int[][]>()!;
                if(seed.GetProperty("geometry").GetProperty("points").GetArrayLength()!=roles.Length||
                    topology.Any(e=>e.Length!=2||e.Any(v=>v<0||v>=roles.Length))||topology.Length!=cached.Length||
                    !topology.SelectMany(e=>e).SequenceEqual(cached.SelectMany(e=>e)))throw new InvalidDataException("Declared boundary topology does not match its native producer.");
                var edges=topology.Select(e=>(IElement)new Relation(Proxy(roles[e[0]]),Proxy(roles[e[1]]))).ToArray();edgeCount+=edges.Length;
                imported.Add(ReadWitness.Create(new Carrier(),Proxy(producer),Proxy(outputs),G(edges),G(),
                    EncodedTextSurface.Create(new Carrier(),"Authored source-scaffold topology import; not acquired ink. Input SHA256 "+
                        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed.GetRawText()))).ToLowerInvariant())));
            }
            additions.Add(new Relation(Proxy(pass),G(imported.ToArray())));
        }
        var proof=ReadWitness.Create(new Carrier(),Proxy(source.Root),G(),G(additions.ToArray()),G(),
            EncodedTextSurface.Create(new Carrier(),"Original compressed native SHA256 "+beforeHash));
        var wrapper=G(Proxy(source.Root),proof);
        var bindings=proxies.ToArray();var overlay=GojoEncodedSnapshot.CaptureGraph([..bindings.Select(p=>(IElement)p.Value),wrapper]);
        var bound=bindings.Select((pair,i)=>(overlay.Roots[i],pair.Key)).ToDictionary(p=>p.Item1,p=>p.Key);
        var merged=Append(source,overlay,overlay.Roots[^1],bound);Validate(merged);
        return (merged,edgeCount);
    }

    public static void Validate(GojoEncodedSnapshot graph)
    {
        var r=new Reader(graph);var wrapper=r.Group(graph.Root);
        if(wrapper.Length!=2)throw new InvalidDataException("Native boundary wrapper lost its numerical run.");
        var original=r.Group(wrapper[0]);var receipt=r.Group(wrapper[1]);
        if(original.Length!=5||receipt.Length!=5||receipt[0]!=wrapper[0])throw new InvalidDataException("Native boundary import lost its source witness.");
        var passes=r.Group(original[2]);var imports=r.Group(receipt[2]);
        if(imports.Length!=passes.Count(p=>r.Group(p).Length==5))throw new InvalidDataException("Native topology omitted a scaffold pass.");
        foreach(var stage in imports)
        {
            var pair=r.Relation(stage);if(!passes.Contains(pair.Left))throw new InvalidDataException("A native topology references a foreign pass.");
            var pass=r.Group(pair.Left);var parts=r.Group(r.Group(pass[1])[3]);var boundaries=r.Group(pair.Right);
            if(parts.Length!=boundaries.Length)throw new InvalidDataException("Native topology omitted a scaffold.");
            for(var i=0;i<parts.Length;i++)
            {
                var geometry=r.Group(parts[i])[1];var rolesSurface=r.Group(geometry)[2];var roles=r.Group(rolesSurface);var read=r.Group(boundaries[i]);
                if(read.Length!=5||read[0]!=geometry||read[1]!=rolesSurface)throw new InvalidDataException("Native boundary roles bypass their actual producer.");
                foreach(var e in r.Group(read[2])){var edge=r.Relation(e);if(!roles.Contains(edge.Left)||!roles.Contains(edge.Right))throw new InvalidDataException("A boundary uses a foreign output role.");}
            }
        }
    }

    private static GojoEncodedSnapshot Append(GojoEncodedSnapshot source,GojoEncodedSnapshot overlay,R root,Dictionary<R,R> bindings)
    {
        if(overlay.Carriers.Any(c=>c.IsUnknown))throw new InvalidDataException("This import grammar does not introduce an unknown carrier.");
        var maps=new Dictionary<R,R>(bindings);
        void Table(K kind,int count,int offset){var next=offset;for(var i=0;i<count;i++){var key=new R(kind,i);if(!maps.ContainsKey(key))maps.Add(key,new(kind,next++));}}
        Table(K.Carrier,overlay.Carriers.Length,source.Carriers.Length);Table(K.TickRef,overlay.TickRefs.Length,source.TickRefs.Length);
        Table(K.CarrierLocation,overlay.CarrierLocations.Length,source.CarrierLocations.Length);Table(K.Relation,overlay.Relations.Length,source.Relations.Length);
        Table(K.UnitShape,overlay.UnitShapes.Length,source.UnitShapes.Length);Table(K.Placeholder,overlay.Placeholders.Length,source.Placeholders.Length);
        R Ref(R r)=>r.IsNone?r:maps[r];int Index(K k,int n)=>n<0?n:Ref(new(k,n)).Index;
        var carrierId=source.Carriers.Max(c=>c.Id);var tickId=source.TickRefs.Max(t=>t.Id);var relationId=source.Relations.Max(r=>r.Id);var unitId=source.UnitShapes.Max(u=>u.Id);
        var slot=source.Placeholders.Select(p=>p.SlotIndex).DefaultIfEmpty(-1).Max()+1;
        var carriers=overlay.Carriers.Select(c=>c with{Index=Index(K.Carrier,c.Index),Id=checked(c.Id+carrierId+1),
            UnitShapeIndex=Index(K.UnitShape,c.UnitShapeIndex),InverseCarrierIndex=Index(K.Carrier,c.InverseCarrierIndex),
            ValueTickRefIndex=Index(K.TickRef,c.ValueTickRefIndex),TensionTickRefIndex=Index(K.TickRef,c.TensionTickRefIndex),
            AttachedRelationIndexes=c.AttachedRelationIndexes!.Select(i=>Index(K.Relation,i)).ToArray()}).ToArray();
        var ticks=overlay.TickRefs.Select(t=>t with{Index=Index(K.TickRef,t.Index),Id=checked(t.Id+tickId+1)}).ToArray();
        var locations=overlay.CarrierLocations.Select(l=>l with{Index=Index(K.CarrierLocation,l.Index),CarrierIndex=Index(K.Carrier,l.CarrierIndex),TickRefIndex=Index(K.TickRef,l.TickRefIndex)}).ToArray();
        var relations=overlay.Relations.Select(r=>r with{Index=Index(K.Relation,r.Index),Id=checked(r.Id+relationId+1),Left=Ref(r.Left),Right=Ref(r.Right)}).ToArray();
        var units=overlay.UnitShapes.Select(u=>u with{Index=Index(K.UnitShape,u.Index),Id=checked(u.Id+unitId+1),Surface=Ref(u.Surface),Descriptors=u.Descriptors.Select(Ref).ToArray()}).ToArray();
        var placeholders=overlay.Placeholders.Where(p=>!bindings.ContainsKey(new(K.Placeholder,p.Index))).Select(p=>p with{Index=Index(K.Placeholder,p.Index),SlotIndex=checked(slot+p.SlotIndex)}).ToArray();
        var result=new GojoEncodedSnapshot([Ref(root)],[..source.Carriers,..carriers],[..source.TickRefs,..ticks],[..source.CarrierLocations,..locations],
            [..source.Relations,..relations],[..source.UnitShapes,..units]){Placeholders=[..source.Placeholders,..placeholders],IsCompleteGraph=true};
        // Stronger than comparing endpoint renders: every original table row,
        // including live tick identity, attachments, order and pressure, survives.
        if(!result.Carriers.AsSpan(0,source.Carriers.Length).SequenceEqual(source.Carriers)||!result.TickRefs.AsSpan(0,source.TickRefs.Length).SequenceEqual(source.TickRefs)||
            !result.CarrierLocations.AsSpan(0,source.CarrierLocations.Length).SequenceEqual(source.CarrierLocations)||!result.Relations.AsSpan(0,source.Relations.Length).SequenceEqual(source.Relations)||
            !result.UnitShapes.AsSpan(0,source.UnitShapes.Length).SequenceEqual(source.UnitShapes)||!result.Placeholders.AsSpan(0,source.Placeholders.Length).SequenceEqual(source.Placeholders))
            throw new InvalidDataException("Structural import rewrote existing native participation.");
        return result;
    }
}
