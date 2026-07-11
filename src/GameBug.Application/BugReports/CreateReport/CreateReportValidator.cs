using FluentValidation;

namespace GameBug.Application.BugReports.CreateReport;

public class CreateReportValidator : AbstractValidator<CreateReportCommand>
{
    private static readonly string[] AllowedExtensions = { ".log", ".txt", ".png", ".jpg", ".jpeg" };
    private static readonly string[] AllowedMimeTypes = { "text/plain", "application/octet-stream", "image/png", "image/jpeg" };

    public CreateReportValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MinimumLength(10).WithMessage("Description must be at least 10 characters.")
            .MaximumLength(10000).WithMessage("Description cannot exceed 10,000 characters.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency key is required.")
            .MinimumLength(16).WithMessage("Idempotency key must be at least 16 characters.")
            .MaximumLength(128).WithMessage("Idempotency key cannot exceed 128 characters.");

        RuleFor(x => x.Attachments)
            .Must(x => x == null || x.Count <= 5)
            .WithMessage("Maximum of 5 attachments is allowed.");

        RuleForEach(x => x.Attachments)
            .ChildRules(attachment =>
            {
                attachment.RuleFor(a => a.OriginalFileName)
                    .NotEmpty().WithMessage("Attachment file name is required.")
                    .Must(BeAnAllowedExtension).WithMessage("File extension is not allowed.");

                attachment.RuleFor(a => a.ContentType)
                    .NotEmpty().WithMessage("Attachment content type is required.")
                    .Must(BeAnAllowedMimeType).WithMessage("MIME type is not allowed.");

                attachment.RuleFor(a => a)
                    .Must(HaveConsistentExtensionAndMimeType)
                    .WithMessage("File extension and MIME type do not match.");
            });
    }

    private static bool HaveConsistentExtensionAndMimeType(FileAttachmentCommand attachment)
    {
        string extension = Path.GetExtension(attachment.OriginalFileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => attachment.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase),
            ".jpg" or ".jpeg" => attachment.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase),
            ".log" or ".txt" =>
                attachment.ContentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
                attachment.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private bool BeAnAllowedExtension(string fileName)
    {
        string ext = Path.GetExtension(fileName);
        return AllowedExtensions.Contains(ext.ToLowerUnicodeUtc() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private bool BeAnAllowedMimeType(string contentType)
    {
        return AllowedMimeTypes.Contains(contentType.ToLowerUnicodeUtc() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }
}

internal static class StringExtensions
{
    public static string ToLowerUnicodeUtc(this string str)
    {
        return str?.ToLowerInvariant() ?? string.Empty;
    }
}
