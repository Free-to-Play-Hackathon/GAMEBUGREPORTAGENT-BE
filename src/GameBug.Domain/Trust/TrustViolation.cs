using System;

namespace GameBug.Domain.Trust;

public record TrustViolation(
    string Code,
    string OutputPath,
    Guid? SourceId,
    bool IsBlocking,
    string Message);
