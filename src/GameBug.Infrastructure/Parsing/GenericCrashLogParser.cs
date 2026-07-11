using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GameBug.Application.Abstractions.Parsing;
using GameBug.Domain.Evidence;

namespace GameBug.Infrastructure.Parsing;

public class GenericCrashLogParser : ILogEvidenceExtractor
{
    private static readonly Regex BuildRegex = new(
        @"\b(?:build(?:version)?|version|ver)\b\s*[:=]\s*([a-zA-Z0-9.-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PlatformRegex = new(
        @"\b(?:platform|os|device)\b\s*[:=]\s*([a-zA-Z0-9.-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExceptionRegex = new(
        @"(?:Unhandled exception|Exception|Error)\s*:\s*([a-zA-Z0-9._]+)(?:\s*:\s*(.*))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HexAddressRegex = new(
        @"0x[a-fA-F0-9]+",
        RegexOptions.Compiled);

    private static readonly Regex TimestampRegex = new(
        @"^\[?(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:[+-]\d{2}:?\d{2}|Z)?)\]?",
        RegexOptions.Compiled);

    private static readonly Regex KeyValueRegex = new(
        @"\b(?<key>LogFormat|Screen|Action|ResourceType|CurrencyType|ResourceBefore|CurrencyBefore|ResourceAfter|CurrencyAfter|ExpectedRewardCount|ReceivedRewardCount|ServerResponse|ErrorCode)\s*=\s*(?<value>[^\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<ParsedLogResult> ExtractAsync(Stream logStream, CancellationToken cancellationToken)
    {
        var detectedEncoding = DetectEncoding(logStream);
        var encoding = (Encoding)detectedEncoding.Clone();
        encoding.DecoderFallback = DecoderFallback.ReplacementFallback;

        using var reader = new StreamReader(logStream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

        string? buildVersion = null;
        string? platform = null;
        string? exceptionType = null;
        string? exceptionMessage = null;

        var stackFrames = new List<string>();
        var timelineEvents = new List<ParsedTimelineEvent>();
        int lineNumber = 0;

        const int MaxLogCharsLimit = 2 * 1024 * 1024; // 2 MB of characters
        int totalCharsRead = 0;
        bool isCapturingExceptionMessage = false;
        string? formatVersion = null;
        string? screen = null;
        string? action = null;
        string? resourceType = null;
        decimal? resourceBefore = null;
        decimal? resourceAfter = null;
        int? expectedRewardCount = null;
        int? receivedRewardCount = null;
        string? serverResponse = null;
        string? errorCode = null;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            totalCharsRead += line.Length;
            if (totalCharsRead > MaxLogCharsLimit)
            {
                break;
            }

            bool hasGameplayFields = false;
            foreach (Match field in KeyValueRegex.Matches(line))
            {
                string key = field.Groups["key"].Value.ToLowerInvariant();
                hasGameplayFields |= key != "logformat";
                string value = field.Groups["value"].Value.Trim().TrimEnd(',', ';');
                switch (key)
                {
                    case "logformat": formatVersion ??= value; break;
                    case "screen": screen ??= value; break;
                    case "action": action ??= value; break;
                    case "resourcetype":
                    case "currencytype": resourceType ??= value; break;
                    case "resourcebefore":
                    case "currencybefore": if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var before)) resourceBefore ??= before; break;
                    case "resourceafter":
                    case "currencyafter": if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var after)) resourceAfter = after; break;
                    case "expectedrewardcount": if (int.TryParse(value, out var expected)) expectedRewardCount = expected; break;
                    case "receivedrewardcount": if (int.TryParse(value, out var received)) receivedRewardCount = received; break;
                    case "serverresponse": serverResponse = value; break;
                    case "errorcode": errorCode = value; break;
                }
            }

            // 1. Detect Build Version
            if (buildVersion == null)
            {
                var match = BuildRegex.Match(line);
                if (match.Success)
                {
                    buildVersion = match.Groups[1].Value;
                }
            }

            // 2. Detect Platform
            if (platform == null)
            {
                var match = PlatformRegex.Match(line);
                if (match.Success)
                {
                    platform = match.Groups[1].Value;
                }
            }

            // 3. Detect Exception & Message
            if (exceptionType == null)
            {
                var match = ExceptionRegex.Match(line);
                if (match.Success)
                {
                    exceptionType = match.Groups[1].Value;
                    exceptionMessage = match.Groups[2].Value.Trim();
                    isCapturingExceptionMessage = true;
                }
            }
            else if (isCapturingExceptionMessage)
            {
                if (line.TrimStart().StartsWith("at ") || line.Contains("StackTrace") || line.Contains("stack trace") ||
                    TimestampRegex.IsMatch(line) || line.StartsWith("[") || line.Trim().Length == 0)
                {
                    isCapturingExceptionMessage = false;
                }
                else
                {
                    exceptionMessage += "\n" + line.Trim();
                }
            }

            // 4. Detect Stack Trace Frames
            bool isStackFrame = false;
            if (line.TrimStart().StartsWith("at ") || line.Contains("StackTrace") || line.Contains("stack trace"))
            {
                isStackFrame = true;
                string normalizedFrame = HexAddressRegex.Replace(line.Trim(), "0x0");
                
                // Deduplicate identical adjacent frames
                if (stackFrames.Count == 0 || stackFrames[^1] != normalizedFrame)
                {
                    stackFrames.Add(normalizedFrame);
                }
            }

            // 5. Detect Timeline/Game events
            if (!isStackFrame && (hasGameplayFields || line.Contains("[GameEvent]") || line.Contains("[Timeline]") || line.Contains("[INFO]") || line.Contains("[WARN]") ||
                line.Contains("Player") || line.Contains("entered") || line.Contains("clicked") || line.Contains("cast")))
            {
                DateTimeOffset? timestamp = null;
                var tsMatch = TimestampRegex.Match(line);
                if (tsMatch.Success)
                {
                    if (DateTimeOffset.TryParse(tsMatch.Groups[1].Value, out var parsedTs))
                    {
                        timestamp = parsedTs;
                    }
                }

                string eventName = "GameEvent";
                if (line.Contains("[GameEvent]")) eventName = "GameEvent";
                else if (line.Contains("[INFO]")) eventName = "Info";
                else if (line.Contains("[WARN]")) eventName = "Warning";
                else if (line.Contains("cast")) eventName = "SkillCast";
                else if (line.Contains("entered")) eventName = "MapTransition";

                string excerpt = line.Trim();
                if (excerpt.Length > 256)
                {
                    excerpt = excerpt.Substring(0, 256) + "...";
                }

                timelineEvents.Add(new ParsedTimelineEvent(timestamp, eventName, excerpt, lineNumber));
            }
        }

        // Calculate StackSignature if exception and stack frames exist
        StackSignature? stackSignature = null;
        if (!string.IsNullOrEmpty(exceptionType) && stackFrames.Any())
        {
            var stableFrames = stackFrames
                .Where(f => !f.Contains("System.") &&
                            !f.Contains("Microsoft.") &&
                            !f.Contains("UnityEngine.") &&
                            !f.Contains("UnityEditor.") &&
                            !f.Contains("Mono."))
                .Take(5)
                .ToList();

            if (!stableFrames.Any())
            {
                stableFrames = stackFrames.Take(5).ToList();
            }

            var frameSummary = string.Join("\n", stableFrames);
            var rawTextToHash = $"{exceptionType}\n{frameSummary}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawTextToHash));
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

            var sigResult = StackSignature.Create(exceptionType, frameSummary, hashHex);
            if (sigResult.IsSuccess)
            {
                stackSignature = sigResult.Value;
            }
        }

        return new ParsedLogResult(
            exceptionType,
            exceptionMessage,
            stackFrames,
            buildVersion,
            platform,
            timelineEvents,
            stackSignature,
            new GameplayLogFacts(
                formatVersion, screen, action, resourceType, resourceBefore, resourceAfter,
                expectedRewardCount, receivedRewardCount, serverResponse, errorCode));
    }

    private static Encoding DetectEncoding(Stream stream)
    {
        long initialPosition = stream.CanSeek ? stream.Position : 0;
        byte[] bom = new byte[4];
        int read = stream.Read(bom, 0, 4);
        if (stream.CanSeek)
        {
            stream.Position = initialPosition;
        }

        if (read >= 2)
        {
            if (bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode; // UTF-16 LE
            if (bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode; // UTF-16 BE
            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8;
        }

        // Heuristics: check for null bytes in the first few read bytes
        if (read >= 4)
        {
            int nullsEven = 0;
            int nullsOdd = 0;
            for (int i = 0; i < read; i++)
            {
                if (bom[i] == 0)
                {
                    if (i % 2 == 0) nullsEven++;
                    else nullsOdd++;
                }
            }
            if (nullsEven > 0 && nullsOdd == 0) return Encoding.BigEndianUnicode;
            if (nullsOdd > 0 && nullsEven == 0) return Encoding.Unicode;
        }

        return Encoding.UTF8;
    }
}
