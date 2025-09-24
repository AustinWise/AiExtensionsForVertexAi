using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.AI;

namespace AWise.AiExtensionsForVertexAi;

public class VertexAiEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<double>>
{
    private readonly PredictionServiceClient _client;
    private readonly string? _defaultModelId;

    public VertexAiEmbeddingGenerator(PredictionServiceClient client, string? defaultModelId = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _defaultModelId = defaultModelId;
    }

    public async Task<GeneratedEmbeddings<Embedding<double>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new PredictRequest();
        if (options != null)
        {
            if (!string.IsNullOrEmpty(options.ModelId))
            {
                request.Endpoint = options.ModelId;
            }
            if (options.Dimensions.HasValue)
            {
                request.Parameters = new Google.Protobuf.WellKnownTypes.Value()
                {
                    StructValue = new Google.Protobuf.WellKnownTypes.Struct()
                    {
                        Fields =
                        {
                            ["outputDimensionality"] = new Google.Protobuf.WellKnownTypes.Value()
                            {
                                NumberValue = options.Dimensions.Value,
                            },
                        },
                    },
                };
            }
        }
        if (string.IsNullOrEmpty(request.Endpoint))
        {
            if (string.IsNullOrEmpty(_defaultModelId))
            {
                throw new ArgumentException($"Please specify the ModelId, either in EmbeddingGenerationOptions.ModelId or the defaultModelId when creating the {nameof(VertexAiEmbeddingGenerator)}.");
            }
            else
            {
                request.Endpoint = _defaultModelId;
            }
        }

        var instanceList = new Google.Protobuf.WellKnownTypes.ListValue();
        foreach (var v in values)
        {
            request.Instances.Add(new Google.Protobuf.WellKnownTypes.Value()
            {
                StructValue = new Google.Protobuf.WellKnownTypes.Struct()
                {
                    Fields =
                    {
                        ["content"] = new Google.Protobuf.WellKnownTypes.Value()
                        {
                            StringValue = v,
                        },
                    },
                },
            });
        }

        var response = await _client.PredictAsync(request, cancellationToken);

        var ret = new GeneratedEmbeddings<Embedding<double>>(response.Predictions.Count);

        foreach (var p in response.Predictions)
        {
            if (p.KindCase != Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StructValue)
            {
                throw new InvalidOperationException("Expected ListValue predicition, but got: " + p.KindCase);
            }
            if (!p.StructValue.Fields.TryGetValue("embeddings", out var embeddings))
            {
                throw new InvalidOperationException("Expected 'embeddings' field in StructValue, but it was not found.");
            }
            if (embeddings.KindCase != Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StructValue)
            {
                throw new InvalidOperationException("Expected StructValue in 'embeddings' field, but got: " + embeddings.KindCase);
            }
            if (!embeddings.StructValue.Fields.TryGetValue("values", out var embeddingValues))
            {
                throw new InvalidOperationException("Expected 'values' field in 'embeddings' StructValue, but it was not found.");
            }
            if (embeddingValues.KindCase != Google.Protobuf.WellKnownTypes.Value.KindOneofCase.ListValue)
            {
                throw new InvalidOperationException("Expected ListValue in 'values' field, but got: " + embeddingValues.KindCase);
            }

            double[] doubles = new double[embeddingValues.ListValue.Values.Count];
            for (int i = 0; i < doubles.Length; i++)
            {
                var v = embeddingValues.ListValue.Values[i];
                if (v.KindCase != Google.Protobuf.WellKnownTypes.Value.KindOneofCase.NumberValue)
                {
                    throw new InvalidOperationException("Expected NumberValue in ListValue, but got: " + v.KindCase);
                }
                doubles[i] = v.NumberValue;
            }
            ret.Add(new Embedding<double>(doubles));
        }

        return ret;
    }

    object? IEmbeddingGenerator.GetService(System.Type serviceType, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }
        else if (serviceType == typeof(VertexAiEmbeddingGenerator))
        {
            return this;
        }
        else if (serviceType == typeof(PredictionServiceClient))
        {
            return _client;
        }

        return null;
    }

    void IDisposable.Dispose()
    {
        // nothing to do
    }
}
