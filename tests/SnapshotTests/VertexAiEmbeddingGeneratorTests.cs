using AWise.AiExtensionsForVertexAi;
using Microsoft.Extensions.AI;
using System.Numerics.Tensors;

namespace SnapshotTests;

public class VertexAiEmbeddingGeneratorTests : TestBase
{
    [Fact]
    public async Task TestGenerateSingleEmbedding()
    {
        var vertex = CreatePredictionServiceClient();
        var generator = new VertexAiEmbeddingGenerator(vertex, defaultModelId: EmbeddingModelName);

        var res = await generator.GenerateAsync("Hello world!");

        Assert.Equal(3072, res.Dimensions);
    }

    [Fact]
    public async Task TestMultipleEmbeddings()
    {
        var vertex = CreatePredictionServiceClient();
        var generator = new VertexAiEmbeddingGenerator(vertex, defaultModelId: EmbeddingModelName);

        var res = await generator.GenerateAsync([
            "I like dogs.",
            "I like cats.",
            "When in the course of human events,",
        ]);

        Assert.Equal(3, res.Count);
        double dogCatSimilarity = TensorPrimitives.CosineSimilarity(res[0].Vector.Span, res[1].Vector.Span);
        double catIndependenceSimilarity = TensorPrimitives.CosineSimilarity(res[1].Vector.Span, res[2].Vector.Span);
        Assert.True(dogCatSimilarity > catIndependenceSimilarity);
    }

    [Fact]
    public async Task TestDimensionReduction()
    {
        var vertex = CreatePredictionServiceClient();
        var generator = new VertexAiEmbeddingGenerator(vertex, defaultModelId: EmbeddingModelName);
        var options = new EmbeddingGenerationOptions()
        {
            Dimensions = 512,
        };

        var res = await generator.GenerateAsync("Hello world!", options);

        Assert.Equal(512, res.Dimensions);
    }
}
