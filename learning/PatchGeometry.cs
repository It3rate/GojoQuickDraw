namespace GojoQuickDraw.Learning;

// Finite observer calculations. The metric samples and selected coarse basis
// are retained by PatchEvidence; these DTOs do not establish source contact.
public static class PatchGeometry
{
    public const int Samples = 20;
    public static readonly string[] FeatureNames = ["a-before", "a-after", "b-before", "b-after", "a-gap", "b-gap",
        "tangent-angle", "a-bend", "b-bend", "a-turn", "b-turn", "a-turn-locality", "b-turn-locality", "a-hook", "b-hook", "length-ratio"];
    public static readonly double[][] Thresholds = [
        [0.004, .06, .18, .40, .72], [0.004, .06, .18, .40, .72], [0.004, .06, .18, .40, .72], [0.004, .06, .18, .40, .72],
        [.003, .03, .10, .25, .6], [.003, .03, .10, .25, .6],
        [8, 22, 45, 68, 82], [.008, .03, .08, .18, .35], [.008, .03, .08, .18, .35],
        [8, 25, 60, 120, 220], [8, 25, 60, 120, 220], [.12, .25, .45, .65, .85], [.12, .25, .45, .65, .85],
        [.003, .015, .04, .08, .16], [.003, .015, .04, .08, .16], [.15, .3, .5, .7, .9]];
    public sealed record Run(double[][] Points, double Length, double Bend, double Turn, double Locality, double Hook);
    public sealed record Read(double[] Metrics, double[] Parameters, double[] Fractions, double[] Encounter, double[][][] Runs, double[] Anchor, double[] Orientation);
    public static double Distance(double[] a, double[] b) => Math.Sqrt((a[0]-b[0])*(a[0]-b[0])+(a[1]-b[1])*(a[1]-b[1]));
    private static double[] Sub(double[] a, double[] b) => [a[0]-b[0], a[1]-b[1]];
    private static double Dot(double[] a, double[] b) => a[0]*b[0]+a[1]*b[1];
    private static double[] Add(double[] a, double[] b, double t) => [a[0]+t*b[0], a[1]+t*b[1]];
    public static double[] Bezier(double[][] controls, double t)
    {
        var p=controls.Select(a=>a.ToArray()).ToArray();
        for(var n=p.Length-1;n>0;n--)for(var i=0;i<n;i++)p[i]=[(1-t)*p[i][0]+t*p[i+1][0],(1-t)*p[i][1]+t*p[i+1][1]];
        return p[0];
    }
    public static Run Measure(double[][] controls, bool polyline=false)
    {
        var points=polyline ? controls : Enumerable.Range(0,Samples+1).Select(i=>Bezier(controls,(double)i/Samples)).ToArray();
        if(points.Length<2 || points.Any(p=>p.Length!=2 || p.Any(v=>!double.IsFinite(v))))throw new InvalidDataException("A patch run needs finite ordered points.");
        var lengths=Enumerable.Range(1,points.Length-1).Select(i=>Distance(points[i-1],points[i])).ToArray();var length=lengths.Sum();
        if(length<1e-7)throw new InvalidDataException("Zero-length run has no ratio basis.");
        var chord=Sub(points[^1],points[0]);var chordLength=Math.Sqrt(Dot(chord,chord));
        var bend=points.Max(p=>chordLength>1e-8 ? Math.Abs((p[0]-points[0][0])*chord[1]-(p[1]-points[0][1])*chord[0])/chordLength : Distance(p,points[0]))/length;
        var changes=new List<(double Turn,double At)>();double travelled=0;
        for(var i=1;i<points.Length-1;i++)
        {
            travelled+=lengths[i-1];var a=Sub(points[i],points[i-1]);var b=Sub(points[i+1],points[i]);var denom=lengths[i-1]*lengths[i];
            if(denom>1e-10)changes.Add((Math.Acos(Math.Clamp(Dot(a,b)/denom,-1,1))*180/Math.PI,travelled/length));
        }
        var turn=changes.Sum(p=>p.Turn);var maximum=changes.Select(p=>p.Turn).DefaultIfEmpty().Max();
        // A short terminal hook remains a read, not deletion of the acquired tail.
        var hook=changes.Where(p=>(p.At<.10 || p.At>.90)&&p.Turn>25).Select(p=>Math.Min(p.At,1-p.At)).DefaultIfEmpty().Max();
        return new(points,length,bend,turn,maximum/Math.Max(1,turn),hook);
    }
    private static (double A,double B,double[] P,double[] Q) Closest(double[] a,double[] b,double[] c,double[] d)
    {
        var u=Sub(b,a);var v=Sub(d,c);var cross=u[0]*v[1]-u[1]*v[0];
        if(Math.Abs(cross)>1e-10)
        {
            var w=Sub(c,a);var s=(w[0]*v[1]-w[1]*v[0])/cross;var t=(w[0]*u[1]-w[1]*u[0])/cross;
            if(s>=0&&s<=1&&t>=0&&t<=1)return(s,t,Add(a,u,s),Add(c,v,t));
        }
        var candidates=new List<(double A,double B,double[] P,double[] Q)>();
        foreach(var (p,s) in new[]{(a,0.0),(b,1.0)}){var t=Math.Clamp(Dot(Sub(p,c),v)/Math.Max(1e-14,Dot(v,v)),0,1);candidates.Add((s,t,p,Add(c,v,t)));}
        foreach(var (q,t) in new[]{(c,0.0),(d,1.0)}){var s=Math.Clamp(Dot(Sub(q,a),u)/Math.Max(1e-14,Dot(u,u)),0,1);candidates.Add((s,t,Add(a,u,s),q));}
        return candidates.MinBy(p=>Distance(p.P,p.Q));
    }
    public static Read ReadPair(double[][] first, double[][] second, bool polyline=false)
    {
        var a=Measure(first,polyline);var b=Measure(second,polyline);
        var best=double.PositiveInfinity;double fa=0,fb=0,ta=0,tb=0;double[] pa=a.Points[0],pb=b.Points[0],da=[1,0],db=[1,0];
        double beforeA=0;
        for(var i=1;i<a.Points.Length;i++)
        {
            var la=Distance(a.Points[i-1],a.Points[i]);double beforeB=0;
            for(var j=1;j<b.Points.Length;j++)
            {
                var lb=Distance(b.Points[j-1],b.Points[j]);var near=Closest(a.Points[i-1],a.Points[i],b.Points[j-1],b.Points[j]);var gap=Distance(near.P,near.Q);
                if(gap<best-1e-8){best=gap;fa=(beforeA+near.A*la)/a.Length;fb=(beforeB+near.B*lb)/b.Length;
                    ta=(i-1+near.A)/(a.Points.Length-1);tb=(j-1+near.B)/(b.Points.Length-1);pa=near.P;pb=near.Q;
                    da=Sub(a.Points[i],a.Points[i-1]);db=Sub(b.Points[j],b.Points[j-1]);}
                beforeB+=lb;
            }
            beforeA+=la;
        }
        var denominator=Math.Sqrt(Dot(da,da)*Dot(db,db));var angle=Math.Acos(Math.Clamp(Math.Abs(Dot(da,db))/Math.Max(1e-10,denominator),0,1))*180/Math.PI;
        double[] metrics=[fa,1-fa,fb,1-fb,best/a.Length,best/b.Length,angle,a.Bend,b.Bend,a.Turn,b.Turn,a.Locality,b.Locality,a.Hook,b.Hook,Math.Min(a.Length,b.Length)/Math.Max(a.Length,b.Length)];
        var chord=Sub(a.Points[^1],a.Points[0]);var bearing=Math.Atan2(chord[1],chord[0]);
        return new(metrics,[ta,tb],[fa,fb],[(pa[0]+pb[0])/2,(pa[1]+pb[1])/2],[a.Points,b.Points],chord,[Math.Cos(bearing),Math.Sin(bearing)]);
    }
    public static int Band(double value,int feature) => Thresholds[feature].Count(t=>value>t);
}
