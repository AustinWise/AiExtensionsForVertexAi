using AWise.AiExtensionsForVertexAi;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace SnapshotTests;

public class VertexAiChatClientTests : TestBase
{
    [Fact]
    public async Task TestBasicTextGeneration()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new VertexAiChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        var options = new ChatOptions()
        {
            Temperature = 0.0f,
        };

        var res = await client.GetResponseAsync("Say \"Hi\" and nothing else.", options);

        Assert.IsType<GenerateContentResponse>(res.RawRepresentation);

        await Verify(res);
    }

    [Fact]
    public async Task TestImageClassification()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new VertexAiChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        List<ChatMessage> chatMessage =
        [
            new ChatMessage(ChatRole.User, [new DataContent(Properties.Resources.circle, "image/png")]),
            new ChatMessage(ChatRole.User, "What type of shape is in this picture?"),
        ];
        var options = new ChatOptions()
        {
            Temperature = 0.0f,
        };

        var res = await client.GetResponseAsync(chatMessage, options);

        await Verify(res);
    }

    [Fact]
    public async Task TestSystemInstructions()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new VertexAiChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        var options = new ChatOptions()
        {
            Instructions = "You're a language translator. Your mission is to translate text in English to French.",
            Temperature = 0.0f,
        };

        var res = await client.GetResponseAsync("Why is the sky blue?", options);

        await Verify(res);
    }

    [Fact]
    public async Task TestSystemMessage()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new VertexAiChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        List<ChatMessage> chatMessage =
        [
            new ChatMessage(ChatRole.System, "You're a language translator. Your mission is to translate text in English to French."),
            new ChatMessage(ChatRole.User, "Why is the sky blue?"),
        ];
        var options = new ChatOptions()
        {
            Temperature = 0.0f,
        };

        var res = await client.GetResponseAsync(chatMessage, options);

        await Verify(res);
    }

    [Fact]
    public async Task TestFunctionCalling()
    {
        var vertex = CreatePredictionServiceClient();
        var client = new VertexAiChatClient(vertex, defaultModelId: FLASH_MODEL_NAME).AsBuilder().UseFunctionInvocation().Build();
        var options = new ChatOptions()
        {
            Tools = [AIFunctionFactory.Create(AddTwoNumbers)],
            Temperature = 0.0f,
        };

        var response = await client.GetResponseAsync("What is the sum of 1087 and 5099?", options);

        await Verify(response);
    }

    [Description("Adds two numbers together")]
    static int AddTwoNumbers(int a, int b)
    {
        return a + b;
    }

    [Fact]
    public async Task TestGenerationOptions()
    {
        var (vertex, interceptor) = CreatePredictionServiceClientAndInterceptor();
        var client = new VertexAiChatClient(vertex, defaultModelId: FLASH_MODEL_NAME);
        var options = new ChatOptions()
        {
            Temperature = 0.001f,
            TopK = 5,
            TopP = 0.1f,
            MaxOutputTokens = 20,
            FrequencyPenalty = 0.2f,
            PresencePenalty = 0.3f,
            Seed = 42,
            ResponseFormat = ChatResponseFormat.Text,
            StopSequences = ["copacabana"],
        };

        _ = await client.GetResponseAsync("Say \"Hi\" and nothing else.", options);

        var request = (GenerateContentRequest)Assert.Single(interceptor.Requests);

        Assert.Equal(0.001f, request.GenerationConfig.Temperature);
        Assert.Equal(5f, request.GenerationConfig.TopK);
        Assert.Equal(0.1f, request.GenerationConfig.TopP);
        Assert.Equal(20, request.GenerationConfig.MaxOutputTokens);
        Assert.Equal(0.2f, request.GenerationConfig.FrequencyPenalty);
        Assert.Equal(0.3f, request.GenerationConfig.PresencePenalty);
        Assert.Equal(42, request.GenerationConfig.Seed);
        Assert.Equal("text/plain", request.GenerationConfig.ResponseMimeType);
        Assert.Single(request.GenerationConfig.StopSequences, "copacabana");
    }
}
