using Google.Api.Gax.Grpc;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AWise.AiExtensionsForVertexAi;

public class VertexAiChatClient : IChatClient
{
    private readonly PredictionServiceClient _client;

    public VertexAiChatClient(PredictionServiceClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var response = await _client.GenerateContentAsync(request, cancellationToken).ConfigureAwait(false);
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        // TODO: validate that all nulls are ok
        var callSettings = new CallSettings(cancellationToken, null, null, null, null, null);
        using (var stream = _client.StreamGenerateContent(request, callSettings))
        {
            // TODO: ConfigureAwait
            await foreach (var res in stream.GetResponseStream())
            {
                yield return null;
            }
        }
        throw new NotImplementedException();
    }

    private GenerateContentRequest CreateRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var request = new GenerateContentRequest();
        if (options != null)
        {
            if (options.ConversationId != null)
            {
                throw new NotImplementedException("ConversationId must be null; stateful client not yet implemented.");
            }
            if (options.Instructions != null)
            {
                var content = new Content()
                {
                    Role = "system", // TODO: confirm this role makes sense
                };
                content.Parts.Add(new Part()
                {
                    Text = options.Instructions,
                });
                request.SystemInstruction = content;
            }
            if (options.Temperature.HasValue)
            {
                request.GenerationConfig.Temperature = options.Temperature.Value;
            }
            if (options.MaxOutputTokens.HasValue)
            {
                request.GenerationConfig.MaxOutputTokens = options.MaxOutputTokens.Value;
            }
            if (options.TopP.HasValue)
            {
                request.GenerationConfig.TopP = options.TopP.Value;
            }
            if (options.TopK.HasValue)
            {
                request.GenerationConfig.TopK = options.TopK.Value;
            }
            if (options.FrequencyPenalty.HasValue)
            {
                request.GenerationConfig.FrequencyPenalty = options.FrequencyPenalty.Value;
            }
            if (options.PresencePenalty.HasValue)
            {
                request.GenerationConfig.PresencePenalty = options.PresencePenalty.Value;
            }
            if (options.Seed.HasValue)
            {
                // TODO: consider either throwing an ArgumentOutOfRange exception instead of a cast error.
                request.GenerationConfig.Seed = (int)options.Seed.Value;
            }
            if (options.ResponseFormat != null)
            {
                if (options.ResponseFormat is ChatResponseFormatText)
                {
                    request.GenerationConfig.ResponseMimeType = "text/plain";
                }
                else if (options.ResponseFormat is ChatResponseFormatJson json)
                {
                    request.GenerationConfig.ResponseMimeType = "application/json";
                    if (json.Schema.HasValue)
                    {
                        request.GenerationConfig.ResponseJsonSchema = ConvertSchema(json.Schema.Value);
                    }
                }
                else
                {
                    throw new ArgumentException("Unexpected ChatResponseFormat: " + options.ResponseFormat.GetType().Name);
                }
            }
            if (options.ModelId != null)
            {
                request.Model = options.ModelId;
            }
            if (options.StopSequences != null)
            {
                request.GenerationConfig.StopSequences.AddRange(options.StopSequences);
            }
            if (options.AllowMultipleToolCalls.HasValue)
            {
                throw new NotImplementedException("AllowMultipleToolCalls not yet implemented.");
            }
            if (options.ToolMode != null)
            {
                if (options.ToolMode is AutoChatToolMode)
                {
                    request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.Auto;
                }
                else if (options.ToolMode is NoneChatToolMode)
                {
                    request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.None;
                }
                else if (options.ToolMode is RequiredChatToolMode required)
                {
                    // TODO: validate that this makes sense. Specifically does the "any" on the VertexAI side mean "required", as the M.E.AI API requires.
                    if (required.RequiredFunctionName is null)
                    {
                        request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.Any;
                    }
                    else
                    {
                        request.ToolConfig.FunctionCallingConfig.Mode = FunctionCallingConfig.Types.Mode.Any;
                        request.ToolConfig.FunctionCallingConfig.AllowedFunctionNames.Add(required.RequiredFunctionName);
                    }
                }
                else
                {
                    throw new ArgumentException("Unexpected tool mode type: " + options.ToolMode.GetType().Name);
                }
            }
            if (options.Tools != null)
            {
                foreach (var optionTool in options.Tools)
                {
                    var functionDeclarations = new List<FunctionDeclaration>();
                    if (optionTool is AIFunction function)
                    {
                        var decl = new FunctionDeclaration()
                        {
                            Name = function.Name,
                            Description = function.Description,
                        };
                        // TODO: is there a better way to detect empty object?
                        if (function.JsonSchema.ValueKind == JsonValueKind.Object && function.JsonSchema.EnumerateObject().GetEnumerator().MoveNext())
                        {
                            decl.ParametersJsonSchema = ConvertSchema(function.JsonSchema);
                        }
                        if (function.ReturnJsonSchema.HasValue)
                        {
                            decl.ResponseJsonSchema = ConvertSchema(function.ReturnJsonSchema.Value);
                        }
                        if (function.AdditionalProperties.Count != 0)
                        {
                            throw new NotImplementedException("AIFunction.AdditionalProperties not yet supported");
                        }
                        functionDeclarations.Add(decl);
                    }
                    else
                    {
                        // TODO: implement grounding with Google search: https://cloud.google.com/vertex-ai/generative-ai/docs/grounding/grounding-with-google-search
                        // TOOD: implement code execution: https://cloud.google.com/vertex-ai/generative-ai/docs/multimodal/code-execution
                        throw new NotImplementedException("Not yet implemented tool type: " + optionTool.GetType().Name);
                    }
                    if (functionDeclarations.Count != 0)
                    {
                        var tool = new Tool();
                        tool.FunctionDeclarations.AddRange(functionDeclarations);
                        request.Tools.Add(tool);
                    }
                }
            }
            if (options.RawRepresentationFactory != null)
            {
                throw new NotImplementedException("RawRepresentationFactory not implemented.");
            }
            if (options.AdditionalProperties != null)
            {
                throw new NotImplementedException("AdditionalProperties not implemented.");
            }
        }
        foreach (var message in messages)
        {
            var content = new Content();
            if (message.Role == ChatRole.User)
            {
                content.Role = "user";
            }
            else if (message.Role == ChatRole.Assistant)
            {
                content.Role = "model";
            }
            else if (message.Role == ChatRole.System)
            {
                // TODO: do we have to stuff this into the request.SystemInstruction? Something else?
                throw new NotImplementedException("Not implemented: ChatRole.System");
            }
            else if (message.Role == ChatRole.Tool)
            {
                // TODO: does this get mapped to user or model??
                throw new NotImplementedException("Not implemented: ChatRole.Tool");
            }
            else
            {
                throw new ArgumentException("Unexpected chat role: " + message.Role.Value);
            }

            // TODO: throw for other properties we don't support??

            foreach (var messageContent in message.Contents)
            {
                var part = new Part();
                if (messageContent is TextContent textContent)
                {
                    part.Text = textContent.Text;
                }
                // TODO: implement more content types
                else
                {
                    throw new NotImplementedException("Unimplemented AIContent type: " + messageContent.GetType().Name);
                }
                content.Parts.Add(part);
            }

            request.Contents.Add(content);
        }
        return request;
    }

    private static Google.Protobuf.WellKnownTypes.Value ConvertSchema(JsonElement element)
    {
        return new Google.Protobuf.WellKnownTypes.Value()
        {
            // TODO: confirm this works; maybe it should be a struct instead???
            StringValue = element.ToString(),
        };
    }

    object? IChatClient.GetService(System.Type serviceType, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }
        else if (serviceType == typeof(VertexAiChatClient))
        {
            return this;
        }
        else if (serviceType == typeof(PredictionServiceClient))
        {
            return _client;
        }

        return null;
    }

    public void Dispose()
    {
    }
}
