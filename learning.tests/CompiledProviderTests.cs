using System.Text.Json;
using GojoQuickDraw.Learning;

namespace GojoQuickDraw.Learning.Tests;

public sealed class CompiledProviderTests
{
    private static (double Probability,int Unseen) TokenPrediction(JsonElement model,int[] bands)
    {
        var columns=model.GetProperty("columns").EnumerateArray().Select(v=>v.GetString()!).ToArray();
        var weights=model.GetProperty("weights").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
        var index=columns.Select((token,i)=>(token,i)).ToDictionary(v=>v.token,v=>v.i);
        var score=0.0;var unseen=0;
        foreach(var token in NeighborhoodFeatures.Tokens(bands))
            if(index.TryGetValue(token,out var i))score+=weights[i];else unseen++;
        return (1/(1+Math.Exp(-Math.Clamp(score,-40,40))),unseen);
    }

    [Fact]
    public void CompiledProviderPreservesSparseFeatureScoresBitForBit()
    {
        var random=new Random(8123);
        foreach(var count in new[]{2,8,24})
        {
            var columns=new List<string>{"bias","01:0","pair:00:0:3:0","unused"};
            for(var i=0;i<count;i++)for(var band=-1;band<=7;band++)
                if(random.Next(4)!=0)columns.Add($"{i}:{band}");
            for(var a=-1;a<=7;a++)for(var b=-1;b<=7;b++)
            {
                if(random.Next(3)!=0)columns.Add($"pair:0:{a}:3:{b}");
                if(random.Next(3)!=0)columns.Add($"pair:1:{a}:5:{b}");
            }
            var weights=columns.Select(_=>(random.NextDouble()-.5)*8).ToArray();
            using var model=JsonDocument.Parse(JsonSerializer.Serialize(new{columns,weights}));
            var compiled=NeighborhoodFeatures.Compile(model.RootElement);
            for(var sample=0;sample<500;sample++)
            {
                var bands=Enumerable.Range(0,count).Select(_=>random.Next(-1,8)).ToArray();
                var expected=TokenPrediction(model.RootElement,bands);
                var actual=compiled.Predict(bands);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Probability),BitConverter.DoubleToInt64Bits(actual.Probability));
                Assert.Equal(expected.Unseen,actual.Unseen);
            }
        }
    }

    [Fact]
    public void CacheUsesTheModelIdentityAndCompiledProviderDoesNotNeedItsJsonDocument()
    {
        var first=JsonDocument.Parse("""{"provider":{"columns":["bias"],"weights":[2]}}""");
        using var second=JsonDocument.Parse("""{"provider":{"columns":["bias"],"weights":[-2]}}""");
        var compiled=NeighborhoodFeatures.Compile(first.RootElement.GetProperty("provider"));
        Assert.Same(compiled,NeighborhoodFeatures.Compile(first.RootElement.GetProperty("provider")));
        var other=NeighborhoodFeatures.Compile(second.RootElement.GetProperty("provider"));
        Assert.NotSame(compiled,other);
        var prediction=compiled.Predict([0,0]);
        Assert.Equal(prediction,NeighborhoodFeatures.Predict(first.RootElement.GetProperty("provider"),[0,0]));
        Assert.True(prediction.Probability>other.Predict([0,0]).Probability);
        first.Dispose();
        Assert.Equal(prediction,compiled.Predict([0,0]));
    }

    [Theory]
    [InlineData("""{"columns":[],"weights":[]}""")]
    [InlineData("""{"columns":["0:1"],"weights":[0]}""")]
    [InlineData("""{"columns":["bias"],"weights":[100]}""")]
    [InlineData("""{"columns":["bias"],"weights":[-100]}""")]
    public void MissingCellsZeroWeightsAndSaturatedScoresKeepTheirOldMeaning(string json)
    {
        using var model=JsonDocument.Parse(json);
        int[] bands=[1,0,5,3,2,4,0,1];
        Assert.Equal(TokenPrediction(model.RootElement,bands),NeighborhoodFeatures.Predict(model.RootElement,bands));
    }

    [Theory]
    [InlineData("""{"columns":["bias"],"weights":[]}""")]
    [InlineData("""{"columns":["bias","bias"],"weights":[0,1]}""")]
    public void InvalidProviderCannotEnterTheCompileCache(string json)
    {
        using var model=JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(()=>NeighborhoodFeatures.Compile(model.RootElement));
        Assert.Throws<InvalidDataException>(()=>NeighborhoodFeatures.Compile(model.RootElement));
    }
}
