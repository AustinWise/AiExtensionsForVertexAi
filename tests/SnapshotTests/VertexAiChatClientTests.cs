using AWise.AiExtensionsForVertexAi;
using Microsoft.Extensions.AI;

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
}
