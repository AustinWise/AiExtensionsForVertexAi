using AWise.AiExtensionsForVertexAi;
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

        var res = await client.GetResponseAsync(new ChatMessage(ChatRole.User, "Say Hi."));

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

        var res = await client.GetResponseAsync(chatMessage);

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
        };

        var response = await client.GetResponseAsync("What is the sum of 1087 and 5099?", options);

        await Verify(response);
    }

    [Description("Adds two numbers together")]
    static int AddTwoNumbers(int a, int b)
    {
        return a + b;
    }
}
