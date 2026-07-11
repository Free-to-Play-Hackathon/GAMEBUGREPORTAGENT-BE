using System.Security.Cryptography;
using System.Text;
using GameBug.Application.Abstractions.AI;
using GameBug.Application.Duplicates;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.AI.Providers;

public sealed class DeterministicHashEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingOptions _options;

    public DeterministicHashEmbeddingProvider(IOptions<EmbeddingOptions> options)
    {
        _options = options.Value;
    }

    public Task<EmbeddingResult> EmbedAsync(string normalizedText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var vector = new float[_options.Dimension];
        foreach (string token in DuplicateTextNormalizer.Tokens(normalizedText))
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            int bucket = BitConverter.ToUInt16(hash, 0) % vector.Length;
            float sign = (hash[2] & 1) == 0 ? 1f : -1f;
            vector[bucket] += sign;
        }

        float norm = (float)Math.Sqrt(vector.Sum(value => value * value));
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return Task.FromResult(new EmbeddingResult(
            vector,
            _options.Provider,
            _options.Model,
            _options.Version,
            _options.Dimension));
    }
}
