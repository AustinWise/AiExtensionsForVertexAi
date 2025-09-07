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
}
