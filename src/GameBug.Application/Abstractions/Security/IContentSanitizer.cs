namespace GameBug.Application.Abstractions.Security;

public record RedactionEvent(string Type, string ReplacementToken, string ValueHash);
public record SanitizedDocument(
    string Text,
    IReadOnlyList<RedactionEvent> Redactions,
    IReadOnlyList<string> InjectionSignals,
    string SanitizerVersion);

public interface IContentSanitizer
{
    string Sanitize(string input);
    SanitizedDocument SanitizeDocument(string input);
}
