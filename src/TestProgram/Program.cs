using AWise.AiExtensionsForVertexAi;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.AI;

namespace TestProgram
{
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

            var response = await client.GetResponseAsync(new ChatMessage(ChatRole.User, "Say hi."), options);
            Console.WriteLine(response);
        }
    }
}
