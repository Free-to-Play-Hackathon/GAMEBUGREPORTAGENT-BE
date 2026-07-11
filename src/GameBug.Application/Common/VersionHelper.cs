using System;
using System.Linq;

namespace GameBug.Application.Common;

public static class VersionHelper
{
    public static bool IsInRange(string? versionStr, string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(versionStr))
        {
            return true; // If no version is specified, it's not a mismatch/conflict
        }

        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end))
        {
            return true;
        }

        if (!TryParseVersion(versionStr, out var version))
        {
            return true; // Ignore if we can't parse the version
        }

        if (!string.IsNullOrWhiteSpace(start) && TryParseVersion(start, out var startVersion))
        {
            if (version < startVersion) return false;
        }

        if (!string.IsNullOrWhiteSpace(end) && TryParseVersion(end, out var endVersion))
        {
            if (version > endVersion) return false;
        }

        return true;
    }

    private static bool TryParseVersion(string versionStr, out Version version)
    {
        // Strip pre-release suffixes like "-beta"
        int dashIndex = versionStr.IndexOf('-');
        if (dashIndex > 0)
        {
            versionStr = versionStr[..dashIndex];
        }

        // Try standard Version.TryParse
        if (Version.TryParse(versionStr, out version!))
        {
            return true;
        }

        // Fallback: clean non-numeric characters
        var cleanStr = new string(versionStr.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return Version.TryParse(cleanStr, out version!);
    }
}
