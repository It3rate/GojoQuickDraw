using System.Text.Json;
using Gojo.Core;
using Gojo.Equations;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using NativeProgram=Gojo.Experiments.ConstructionMachines.Program;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>Replace selected independent bounds by one affine construction.
/// The equations, shared parameters, exposed ports and response maps are native.
/// This is parameterization, not a projection applied only by the renderer.</summary>
public static class ChurchBoundConstruction
{
    public sealed record Slot(string Part,int Coordinate);
    public sealed record Term(Slot Slot,double Coefficient);
    public sealed record Binding(Slot Target,Term[] Terms);
    public sealed record Recipe(string Id,Binding[] Bindings,JsonElement Input,bool Held);
    private static Slot ReadSlot(JsonElement x)=>new(x.GetProperty("part").GetString()!,x.GetProperty("coordinate").GetInt32());
    public static Binding[] ReadBindings(JsonElement r)=>r.GetProperty("bindings").EnumerateArray().Select(b=>new Binding(ReadSlot(b.GetProperty("target")),
        b.GetProperty("terms").EnumerateArray().Select(t=>new Term(ReadSlot(t),t.GetProperty("coefficient").GetDouble())).ToArray())).ToArray();

    public static (string[] Accepted,Relation Witness) Compose(ChurchScene scene,IReadOnlyList<Recipe> recipes)
    {
        var old=scene.Parts.ToArray();var ids=old.Select((p,i)=>(p.Id,i)).ToDictionary(p=>p.Id,p=>p.i);
        var values=old.SelectMany(p=>{var b=scene.Bounds(p);return new[]{b[0]*4+512,b[1]*4+512,(b[2]-b[0])*4,(b[3]-b[1])*4};}).ToArray();
        var chosen=new Dictionary<Slot,Binding>();var accepted=new List<string>();var declarations=new List<IElement>();
        var rounding=values.Select(v=>2*Math.Round(v/2)).ToArray();
        Dictionary<int,double> Form(Slot s,Dictionary<Slot,Binding> bindings,HashSet<Slot> active)
        {
            if(!ids.TryGetValue(s.Part,out var i)||s.Coordinate is <0 or >5)throw new InvalidDataException("Unknown construction input");
            if(!active.Add(s))throw new InvalidDataException("Cyclic construction binding");
            Dictionary<int,double> result=[];
            void Add(Dictionary<int,double> terms,double weight){foreach(var (k,v) in terms)result[k]=result.GetValueOrDefault(k)+weight*v;}
            if(bindings.TryGetValue(s,out var binding))foreach(var term in binding.Terms)Add(Form(term.Slot,bindings,active),term.Coefficient);
            else switch(s.Coordinate)
            {
                case 0:result[i*4]=1;break;case 1:result[i*4+1]=1;break;
                case 2:result[i*4]=1;result[i*4+2]=1;break;
                case 3:result[i*4+1]=1;result[i*4+3]=1;break;
                case 4:result[i*4+2]=1;break;case 5:result[i*4+3]=1;break;
            }
            active.Remove(s);return result.Where(p=>Math.Abs(p.Value)>1e-12).ToDictionary();
        }
        IElement Declared(Slot s)
        {
            var p=old[ids[s.Part]];
            return s.Coordinate<4?p.Machine.Publish(ChurchHypotheses.Bound(p,s.Coordinate)):
                G(p.Machine.Publish(ChurchHypotheses.Bound(p,s.Coordinate-4)),p.Machine.Publish(ChurchHypotheses.Bound(p,s.Coordinate-2)));
        }
        foreach(var recipe in recipes.OrderByDescending(r=>r.Held))
        {
            var native=G(recipe.Bindings.Select(b=>(IElement)G(Declared(b.Target),G(b.Terms.Select(t=>(IElement)G(Declared(t.Slot),
                Relation.Segment(new Carrier(),0,checked((long)Math.Round(t.Coefficient*1024))),Relation.Segment(new Carrier(),0,1024))).ToArray()))).ToArray());
            if(recipe.Input.ValueKind==JsonValueKind.Object&&recipe.Input.TryGetProperty("supportParts",out var supporting))
                native=G(native,G(supporting.EnumerateArray().Select(id=>(IElement)old[ids[id.GetString()!]].Geometry).ToArray()));
            var trial=new Dictionary<Slot,Binding>(chosen);var proposed=rounding.ToArray();
            try
            {
                foreach(var binding in recipe.Bindings)
                {
                    if(trial.ContainsKey(binding.Target))throw new InvalidDataException("A selected constructor already binds this role");
                    trial.Add(binding.Target,binding);
                }
                foreach(var part in old)
                {
                    var forms=Enumerable.Range(0,4).Select(k=>Form(new(part.Id,k),trial,[])).ToArray();
                    if(forms.Any(f=>f.Values.Any(c=>Math.Abs(2*c-Math.Round(2*c))>1e-10)))throw new InvalidDataException("Needs a finer construction lattice than the selected half-unit inputs");
                    var b=forms.Select(f=>f.Sum(p=>proposed[p.Key]*p.Value)).ToArray();
                    if(b[2]-b[0]<4||b[3]-b[1]<4)throw new InvalidDataException("Would collapse or reverse a proposed part");
                }
                chosen=trial;rounding=proposed;accepted.Add(recipe.Id);
                declarations.Add(ReadWitness.Create(new Carrier(),native,G(),G(native),G(),scene.Source.Surface));
            }
            catch(InvalidDataException)
            {
                declarations.Add(ReadWitness.Create(new Carrier(),native,G(),new Placeholder(),G(native),scene.Source.Surface));
            }
        }
        if(accepted.Count==0)return ([],G(declarations.ToArray()));
        var parameters=rounding.Select(v=>NativeProgram.Parameter(checked((long)Math.Max(0,v)))).ToArray();
        var formsByPart=old.ToDictionary(p=>p.Id,p=>Enumerable.Range(0,4).Select(k=>Form(new(p.Id,k),chosen,[])).ToArray());
        var cache=new Dictionary<string,IElement>();
        IElement Vector(Dictionary<int,double> f,int axis)
        {
            var key=axis+":"+string.Join(",",f.OrderBy(p=>p.Key).Select(p=>$"{p.Key}/{p.Value:R}"));
            if(cache.TryGetValue(key,out var retained))return retained;
            IElement result=scene.Space.Zero;
            foreach(var (k,c) in f)result=scene.Space.Plus(result,scene.Space.Times(
                scene.Space.Times(scene.Space.Point(axis==0?1:0,axis==1?1:0),parameters[k].ValueSlot,parameters[k]),checked((long)Math.Round(c*2)),2));
            return cache[key]=result;
        }
        var corners=new List<IElement>();
        foreach(var p in old)
        {
            var f=formsByPart[p.Id];
            foreach(var (x,y) in new[]{(0,1),(2,1),(2,3),(0,3)})corners.Add(scene.Space.Plus(Vector(f[x],0),Vector(f[y],1)));
        }
        var definition=TransformSurface.Create(new Carrier(),G(parameters.Cast<IElement>().ToArray()),G(corners.ToArray()),
            G(corners.Select(_=>(IElement)new Placeholder()).ToArray()),G(),G(scene.Space.Basis));
        // Admission/history remains connected in the world, outside the live
        // evaluator's private definition. Local previews need only its actual
        // parameter/affine construction, not every acquired source ancestor.
        scene.Imports.Add(G(definition,G(declarations.ToArray())));
        var host=new NegotiatingMachine(definition,retainHistory:!scene.IsLive);var parts=new List<ChurchScene.Part>();var portMap=new Dictionary<Relation,Relation>(ReferenceEqualityComparer.Instance);
        foreach(var (p,i) in old.Select((p,i)=>(p,i)))
        {
            var fs=formsByPart[p.Id];var offset=i*4;
            foreach(var k in Enumerable.Range(0,4))
            {
                var point=k switch{0=>offset,1=>offset,2=>offset+1,_=>offset+3};var axis=k%2;
                var port=host.Port(point,axis);portMap.Add(ChurchHypotheses.Bound(p,k),port);
                var norm=fs[k].Values.Sum(c=>4*c*c);
                host.AddCoupledResponses(port,fs[k].Select(t=>new NegotiatingMachine.ResponseTarget(parameters[t.Key],
                    checked((long)Math.Round(4*t.Value)),checked((long)Math.Round(norm)),t.Key%4<2?0:4,4096,8,2)).ToArray());
            }
            var outputs=p.Normalized.Select(uv=>(IElement)scene.Space.Plus(
                scene.Space.Plus(scene.Space.Times(Vector(fs[0],0),checked((long)Math.Round((1-uv[0])*1024)),1024),scene.Space.Times(Vector(fs[2],0),checked((long)Math.Round(uv[0]*1024)),1024)),
                scene.Space.Plus(scene.Space.Times(Vector(fs[1],1),checked((long)Math.Round((1-uv[1])*1024)),1024),scene.Space.Times(Vector(fs[3],1),checked((long)Math.Round(uv[1]*1024)),1024)))).ToArray();
            var geometry=TransformSurface.Create(new Carrier(),G(host.Surface),G(outputs),G(outputs.Select(_=>(IElement)new Placeholder()).ToArray()),G(),G(scene.Space.Basis,definition));
            parts.Add(p with {Machine=host,Geometry=geometry,OutputOffset=offset});
            var edges=G(p.Segments.Select(e=>(IElement)new Relation(outputs[e[0]],outputs[e[1]])).ToArray());
            scene.Imports.Add(ReadWitness.Create(new Carrier(),p.Geometry,G(outputs),G(geometry,edges),G(),definition));
        }
        var import=ReadWitness.Create(new Carrier(),G(old.Select(p=>(IElement)p.Machine.Surface).ToArray()),G(declarations.ToArray()),
            host.Surface,G(rounding.Select((v,i)=>(IElement)ProjectionResidue.Create(parameters[i],checked((long)Math.Round((values[i]-v)*1024)),1024)).ToArray()),
            G(portMap.Select(p=>(IElement)new Relation(p.Key,p.Value)).ToArray()));
        // Construction replacement is a witnessed import. Retain the public
        // displacement it introduced, independently of input rounding and of
        // subsequent material settlement. Equal coordinates do not assert site identity.
        scene.Imports.Add(G(old.SelectMany(p=>Enumerable.Range(0,4).Select(k=>
        {
            var a=ChurchHypotheses.Bound(p,k);var b=portMap[a];
            ((Relation)F(a)[1]).TryReadSegment(out var sourceAxis,out _,out _);
            ((Relation)F(b)[1]).TryReadSegment(out var targetAxis,out _,out _);
            var before=p.Machine.Publish(a);var after=host.Publish(b);
            var basis=Equation.Affine(sourceAxis,0,1,targetAxis,0,1);
            var projected=Equation.PredictWitness(new Carrier(),basis,F(before)[2],targetAxis);
            var difference=Equation.PredictWitness(new Carrier(),Equation.Difference(targetAxis,targetAxis,targetAxis),new Relation(F(projected)[2],F(after)[2]),targetAxis);
            return (IElement)ReadWitness.Create(new Carrier(),before,basis,after,F(difference)[2],G(projected,difference));
        })).ToArray()));
        scene.ReplaceParts(parts,host,portMap,import);
        return (accepted.ToArray(),G(G(declarations.ToArray()),import));
    }
}
