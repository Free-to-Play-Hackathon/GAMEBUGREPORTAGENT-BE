using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.AI.Providers;

public sealed class OpenAiOptionsValidator(IHostEnvironment environment) : IValidateOptions<OpenAiOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("Ai:OpenAI:ApiKey is required.");
        }

        if (options.ApiKey.Equals("mock", StringComparison.OrdinalIgnoreCase) &&
            !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Fail("Ai:OpenAI:ApiKey=mock is allowed only in Development or Testing.");
        }

        return ValidateOptionsResult.Success;
    }
}
