using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gojo.Core;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void NumericArchiveRetainsEveryDrawingStageAndItsSourceCorrespondence()
    {
        var root=Import(StepsFixture());
        var legacy=NativeRecord.Restore(NativeRecord.Capture("stages",root));
        var bytes=GojoNumericFormat.Encode(root);
        var restored=NativeRecord.RestoreArchiveContent(bytes);
        Assert.Equal(legacy.SourceOriginalJson,restored.SourceOriginalJson);
        Assert.Equal(JsonSerializer.Serialize(NativeRecord.Summarize(legacy)),JsonSerializer.Serialize(NativeRecord.Summarize(restored)));
        Assert.Equal(JsonSerializer.Serialize(JointReadback.Json(legacy)),JsonSerializer.Serialize(JointReadback.Json(restored)));
        var steps=restored.DrawingSteps!;
        Assert.Equal(4,steps.Construction.Rungs.Length);
        Assert.NotSame(restored.Acquisition.Samples[0].Event,steps.Acquisition.Samples[0].Event);
        for(var i=0;i<steps.Construction.Rungs.Length;i++)
        {
            var before=legacy.DrawingSteps!.Construction.Rungs[i].Visible;
            var after=steps.Construction.Rungs[i].Visible;
            Assert.Equal(before.SelectMany(e=>e.Controls.Select(c=>c.Position)),after.SelectMany(e=>e.Controls.Select(c=>c.Position)));
            Assert.Equal(legacy.DrawingSteps.Passes[i].Links.Length,steps.Passes[i].Links.Length);
            Assert.Equal(legacy.DrawingSteps.Passes[i].Unmatched.Length,steps.Passes[i].Unmatched.Length);
        }
        var profile=restored.Annotation!.Profiles[0];
        Assert.Same(restored.Acquisition.Samples[0].Event,profile.Curve.From);
    }

    [Fact]
    public void ArchiveReaderDetectsBothCodecsEvenWithoutAnExtension()
    {
        var directory=Path.Combine(Path.GetTempPath(),"gojo-native-codec-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var root=Import(Fixture());
            var legacy=Encoding.UTF8.GetBytes("\ufeff \r\n"+NativeRecord.Capture("fixture",root));
            foreach(var content in new[]{legacy,GojoNumericFormat.Encode(root)})
            {
                var path=Path.Combine(directory,"stored.tmp");
                using(var output=new GZipStream(File.Create(path),CompressionLevel.Fastest))output.Write(content);
                var inspect=new StringWriter();var errors=new StringWriter();
                Assert.Equal(0,Worker.Run(["inspect","--input",path,"--source"],TextReader.Null,inspect,errors));
                Assert.Equal(NativeRecord.RestoreArchiveContent(content).SourceOriginalJson,
                    JsonNode.Parse(inspect.ToString())!["sourceOriginalJson"]!.GetValue<string>());
            }
        }
        finally {Directory.Delete(directory,recursive:true);}
    }

    [Fact]
    public void NumericArchiveRejectsTruncationForeignRootsAndUnknownHeaders()
    {
        var bytes=GojoNumericFormat.Encode(Import(Fixture()));
        Assert.Throws<InvalidDataException>(()=>NativeRecord.RestoreArchiveContent(bytes.AsSpan(0,bytes.Length-1)));
        Assert.Throws<InvalidDataException>(()=>NativeRecord.RestoreArchiveContent(GojoNumericFormat.Encode(new Carrier())));
        Assert.Throws<InvalidDataException>(()=>NativeRecord.RestoreArchiveContent("BROKEN"u8));
        Assert.Throws<InvalidDataException>(()=>NativeRecord.RestoreArchiveContent([]));
        var badVersion=(byte[])bytes.Clone();badVersion[4]=255;
        Assert.Throws<InvalidDataException>(()=>NativeRecord.RestoreArchiveContent(badVersion));
    }
}
