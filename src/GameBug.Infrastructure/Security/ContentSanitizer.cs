using System.Text.RegularExpressions;
using GameBug.Application.Abstractions.Security;
using System.Security.Cryptography;
using System.Text;

namespace GameBug.Infrastructure.Security;

public class ContentSanitizer : IContentSanitizer
{
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IpRegex = new(
        @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
        RegexOptions.Compiled);

    private static readonly Regex AbsolutePathRegex = new(
        @"([a-zA-Z]:\\Users\\[a-zA-Z0-9_-]+|/home/[a-zA-Z0-9_-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BearerTokenRegex = new(
        @"Bearer\s+[a-zA-Z0-9\-._~+/]+=*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JwtRegex = new(
        @"\beyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\b",
        RegexOptions.Compiled);

    private static readonly Regex SecretFieldRegex = new(
        @"\b(?:api[_-]?key|secret|session[_-]?id|account[_-]?id|device[_-]?id)\s*[:=]\s*[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Sanitize(string input)
        => SanitizeDocument(input).Text;

    public SanitizedDocument SanitizeDocument(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new SanitizedDocument(input, Array.Empty<RedactionEvent>(), Array.Empty<string>(), "sanitizer-v1");
        }

        string result = input;
        var redactions = new List<RedactionEvent>();
        AddEvents(EmailRegex, input, "Email", "[EMAIL_REDACTED]", redactions);
        AddEvents(IpRegex, input, "IpAddress", "[IP_REDACTED]", redactions);
        AddEvents(AbsolutePathRegex, input, "LocalPath", "[PATH_REDACTED]", redactions);
        AddEvents(BearerTokenRegex, input, "BearerToken", "[TOKEN_REDACTED]", redactions);
        AddEvents(JwtRegex, input, "Jwt", "[TOKEN_REDACTED]", redactions);
        AddEvents(SecretFieldRegex, input, "SensitiveIdentifier", "[IDENTIFIER_REDACTED]", redactions);
        result = EmailRegex.Replace(result, "[EMAIL_REDACTED]");
        result = IpRegex.Replace(result, "[IP_REDACTED]");
        result = AbsolutePathRegex.Replace(result, "[PATH_REDACTED]");
        result = BearerTokenRegex.Replace(result, "[TOKEN_REDACTED]");
        result = JwtRegex.Replace(result, "[TOKEN_REDACTED]");
        result = SecretFieldRegex.Replace(result, "[IDENTIFIER_REDACTED]");

        var signals = Regex.IsMatch(
            input,
            @"(?i)(ignore\s+(all\s+)?previous|system\s+prompt|developer\s+message|follow\s+these\s+instructions)")
            ? new[] { "PROMPT_INJECTION_MARKER" }
            : Array.Empty<string>();

        return new SanitizedDocument(result, redactions, signals, "sanitizer-v1");
    }

    private static void AddEvents(
        Regex regex,
        string input,
        string type,
        string replacement,
        ICollection<RedactionEvent> events)
    {
        foreach (Match match in regex.Matches(input))
        {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(match.Value)));
            events.Add(new RedactionEvent(type, replacement, hash));
        }
    }
}
