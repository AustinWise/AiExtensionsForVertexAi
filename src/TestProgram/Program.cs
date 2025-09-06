using AWise.AiExtensionsForVertexAi;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.AI;

namespace TestProgram;

internal class Program
{
    static async Task Main(string[] args)
    {
        string projectId = "ai-test-414105";
        string location = "us-central1";
        string publisher = "google";
        string model = "gemini-2.5-flash-lite";

        var client = new VertexAiChatClient(new PredictionServiceClientBuilder()
        {
            Endpoint = $"https://{location}-aiplatform.googleapis.com",
        }.Build());
        var options = new ChatOptions()
        {
            ModelId = $"projects/{projectId}/locations/{location}/publishers/{publisher}/models/{model}",
        };

        Console.WriteLine("Trying GetResponse:");
        var response = await client.GetResponseAsync(new ChatMessage(ChatRole.User, "Say hi."), options);
        Console.WriteLine(response);
        Console.WriteLine();

        Console.WriteLine("Trying GetStreamingResponseAsync:");
        await foreach (var update in client.GetStreamingResponseAsync(new ChatMessage(ChatRole.User, "Please write a poem in iambic pentameter about a turtle who likes to eat strawberries."), options))
        {
            Console.Write(update.Text);
        }
        Console.WriteLine();
        Console.WriteLine();

        Console.WriteLine("DONE!");
    }
}
