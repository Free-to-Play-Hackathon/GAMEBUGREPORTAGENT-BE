using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.AI.Providers;

public sealed class OpenAiOptionsValidator : IValidateOptions<OpenAiOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("Ai:OpenAI:ApiKey is required.");
        }

        if (options.ApiKey.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("Ai:OpenAI:ApiKey=mock is not allowed. Configure a real key with Ai__OpenAI__ApiKey.");
        }

        if (options.ApiKey.Contains("replace", StringComparison.OrdinalIgnoreCase) ||
            options.ApiKey.Contains("your-openai-api-key", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("Ai:OpenAI:ApiKey must be a real OpenAI API key, not a placeholder.");
        }

        return ValidateOptionsResult.Success;
    }
}
