using AWise.AiExtensionsForVertexAi;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace SnapshotTests
{
    public class UnitTest1
    {
        // TODO: make use environmental variables
        private bool IsRecording => false;

        [Fact]
        public async Task Test1()
        {
            string projectId = "ai-test-414105";
            string location = "us-central1";
            string publisher = "google";
            string model = "gemini-2.5-flash-lite";

            var modelId = $"projects/{projectId}/locations/{location}/publishers/{publisher}/models/{model}";

            var vertex = CreatePredictionServiceClient();
            var client = new VertexAiChatClient(vertex, defaultModelId: modelId);
            var res = await client.GetResponseAsync(new ChatMessage(ChatRole.User, "Say Hi."));

            await Verify(res);
        }

        private PredictionServiceClient CreatePredictionServiceClient([CallerMemberName] string? caller = null)
        {
            string? folder = Path.GetDirectoryName(GetType().Assembly.Location);

            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder, "SnapshotTests.csproj")))
                    break;
                folder = Path.GetDirectoryName(folder);
            }

            if (folder is null)
            {
                throw new Exception("could not find snapshot folder");
            }

            folder = Path.Combine(folder, "snapshots");

            string requestFile = Path.Combine(folder, $"{GetType().Name}.{caller}.request.binarypb");
            string responseFile = Path.Combine(folder, $"{GetType().Name}.{caller}.response.binarypb");

            Interceptor interceptor;
            if (IsRecording)
            {
                interceptor = new RecordingInterceptor(requestFile, responseFile);
            }
            else
            {
                interceptor = new ReplayInterceptor(requestFile, responseFile);
            }

            var builder = new PredictionServiceClientBuilder()
            {
                Endpoint = "https://us-central1-aiplatform.googleapis.com",
                Settings = new PredictionServiceSettings()
                {
                    Interceptor = interceptor,
                },
            };
            return builder.Build();
        }


        class RecordingInterceptor(string requestFile, string responseFile) : Interceptor
        {
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                using (var fs = File.Create(requestFile))
                {
                    ((IMessage)request).WriteTo(fs);
                }
                var response = continuation.Invoke(request, context);
                using (var fs = File.Create(responseFile))
                {
                    ((IMessage)response.ResponseAsync.GetAwaiter().GetResult()).WriteTo(fs);
                }
                return response;
            }

            #region NYI
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }
            #endregion
        }


        class ReplayInterceptor(string requestFile, string responseFile) : Interceptor
        {
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                var requestSnapshot = (IMessage)Activator.CreateInstance<TRequest>();
                using (var fs = File.OpenRead(requestFile))
                {
                    requestSnapshot.MergeFrom(fs);
                }
                Assert.Equal(requestSnapshot, (IMessage)request);
                var response = Activator.CreateInstance<TResponse>();
                using (var fs = File.OpenRead(responseFile))
                {
                    ((IMessage)response).MergeFrom(fs);
                }
                // TODO: maybe capture these other things?
                return new AsyncUnaryCall<TResponse>(Task.FromResult(response), Task.FromResult(Metadata.Empty), () => Status.DefaultSuccess, () => Metadata.Empty, () => { });
            }

            #region NYI
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }

            public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
            {
                throw new NotImplementedException();
            }
            #endregion
        }
    }
}
