using Google.Api.Gax.Grpc;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AWise.AiExtensionsForVertexAi;

public class VertexAiChatClient : IChatClient
{
    private readonly PredictionServiceClient _client;
    private readonly string? _defaultModelId;

    public VertexAiChatClient(PredictionServiceClient client, string? defaultModelId = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _defaultModelId = defaultModelId;
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var response = await _client.GenerateContentAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.Candidates.Count != 1)
        {
            throw new InvalidOperationException($"Unexpected number of candidates: {response.Candidates.Count}");
        }
        var candidate = response.Candidates[0];
        var chatResponse = new ChatResponse()
        {
        };
        if (!TestHelpers.IsRunningInUnitTest)
        {
            chatResponse.ResponseId = response.ResponseId;
        }
        if (candidate.HasFinishMessage)
        {
            chatResponse.FinishReason = GetFinishReason(candidate.FinishReason, candidate.FinishMessage);
        }

        chatResponse.Messages.Add(new ChatMessage()
        {
            Role = GetRole(candidate.Content),
            Contents = ConvertToAiContent(candidate.Content),
        });
        return chatResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(messages, options);
        var callSettings = CallSettings.FromCancellationToken(cancellationToken);
        using (var stream = _client.StreamGenerateContent(request, callSettings))
        {
            // TODO: ConfigureAwait
            await foreach (var response in stream.GetResponseStream())
            {
                if (response.Candidates.Count != 1)
                {
                    throw new InvalidOperationException($"Unexpected number of candidates: {response.Candidates.Count}");
                }
                var candidate = response.Candidates[0];
                var chatResponse = new ChatResponseUpdate()
                {
                    ResponseId = response.ResponseId,
                };
                if (candidate.HasFinishMessage)
                {
                    chatResponse.FinishReason = GetFinishReason(candidate.FinishReason, candidate.FinishMessage);
                }
                chatResponse.Role = GetRole(candidate.Content);
                chatResponse.Contents = ConvertToAiContent(candidate.Content);
                yield return chatResponse;
            }
        }
    }

    // TODO: make sure these mappings and exceptions make sense. Like would it be better to create custom ChatFinishReasons for each type?
    private static ChatFinishReason? GetFinishReason(Candidate.Types.FinishReason finishReason, string finishMessage)
    {
        return finishReason switch
        {
            Candidate.Types.FinishReason.Stop => ChatFinishReason.Stop,
            Candidate.Types.FinishReason.MaxTokens => ChatFinishReason.Length,
            Candidate.Types.FinishReason.Safety => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.Recitation => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.Spii => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.Blocklist => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.ProhibitedContent => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.ModelArmor => ChatFinishReason.ContentFilter,
            Candidate.Types.FinishReason.MalformedFunctionCall => throw new InvalidOperationException("Malformed tool call: " + finishMessage),
            Candidate.Types.FinishReason.Other => throw new InvalidOperationException("Other finish reason: " + finishMessage),
            Candidate.Types.FinishReason.Unspecified => null,
            _ => null,
        };
    }

    private static ChatRole GetRole(Content content)
    {
        return content.Role switch
        {
            "user" => ChatRole.User,
            "model" => ChatRole.Assistant,
            _ => throw new InvalidOperationException("Unexpected role: " + content.Role),
        };
    }

    private static IList<AIContent> ConvertToAiContent(Content content)
    {
        var ret = new List<AIContent>();
        foreach (var part in content.Parts)
        {
            switch (part.DataCase)
            {
                case Part.DataOneofCase.Text:
                    ret.Add(new TextContent(part.Text));
                    break;
                case Part.DataOneofCase.FunctionCall:
                    var args = part.FunctionCall.Args.Fields.ToDictionary(a => a.Key, a => (object?)ProtoJsonConversions.ConvertValueToJsonNode(a.Value));
                    ret.Add(new FunctionCallContent(part.FunctionCall.Name, part.FunctionCall.Name,args));
                    break;
                case Part.DataOneofCase.FunctionResponse:
                case Part.DataOneofCase.InlineData:
                case Part.DataOneofCase.FileData:
                case Part.DataOneofCase.ExecutableCode:
                case Part.DataOneofCase.CodeExecutionResult:
                    throw new NotImplementedException("Not implemented part type: " + part.DataCase);
                case Part.DataOneofCase.None:
                default:
                    throw new InvalidOperationException("Unexpected part type: " + part.DataCase);
            }
        }
        return ret;
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
                        request.GenerationConfig.ResponseJsonSchema = ProtoJsonConversions.ConvertJsonElementToValue(json.Schema.Value);
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
                            decl.ParametersJsonSchema = ProtoJsonConversions.ConvertJsonElementToValue(function.JsonSchema);
                        }
                        if (function.ReturnJsonSchema.HasValue)
                        {
                            decl.ResponseJsonSchema = ProtoJsonConversions.ConvertJsonElementToValue(function.ReturnJsonSchema.Value);
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

        if (string.IsNullOrEmpty(request.Model))
        {
            if (string.IsNullOrEmpty(_defaultModelId))
            {
                // The error message from the API when we don't specify a model is somewhat inscrutable:
                //   Invalid resource field value in the request.
                // And forgetting to set a model is easy. So this is the one piece of validation we do
                // before sending the request.
                throw new ArgumentException($"Please specify the ModelId, either in ChatOptions.ModelId or the defaultModelId when creating the {nameof(VertexAiChatClient)}.");
            }
            else
            {
                request.Model = _defaultModelId;
            }
        }

        foreach (var message in messages)
        {
            var content = new Content();
            if (message.Role == ChatRole.User || message.Role == ChatRole.Tool)
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
                else if (messageContent is DataContent dataContent)
                {
                    part.InlineData = new Blob()
                    {
                        // dataContent.Data is a ReadOnlyMemory, so its size can't change.
                        // The only this that could change is the backing array, but that
                        // should not be unsafe.
                        Data = UnsafeByteOperations.UnsafeWrap(dataContent.Data),
                        MimeType = dataContent.MediaType,
                    };
                }
                else if (messageContent is FunctionCallContent functionCall)
                {
                    part.FunctionCall = new FunctionCall()
                    {
                        Name = functionCall.Name,
                    };
                    if (functionCall.Arguments != null)
                    {
                        var args = new Google.Protobuf.WellKnownTypes.Struct();
                        foreach (var arg in functionCall.Arguments)
                        {
                            args.Fields[arg.Key] = ProtoJsonConversions.ConvertObjectToValue(arg.Value);
                        }
                        part.FunctionCall.Args = args;
                    }
                }
                else if (messageContent is FunctionResultContent functionResult)
                {
                    part.FunctionResponse = new FunctionResponse()
                    {
                        Name = functionResult.CallId,
                        // TODO: figure out if it is valid to always pull a struct out of this.
                        Response = ProtoJsonConversions.ConvertObjectToValue(functionResult.Result).StructValue,
                    };
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
