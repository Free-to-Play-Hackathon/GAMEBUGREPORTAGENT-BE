using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GameBug.Application.Duplicates;

public static class DuplicateTextNormalizer
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex TokenPattern = new("[a-z0-9_\\-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormKC).ToLower(CultureInfo.InvariantCulture).Trim();
        return Whitespace.Replace(normalized, " ");
    }

    public static IReadOnlyList<string> Tokens(string? value) =>
        TokenPattern.Matches(Normalize(value))
            .Select(match => match.Value)
            .Where(token => token.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static string BuildSearchText(params string?[] parts) =>
        Normalize(string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part))));
}
