using System.Globalization;
using System.Text.RegularExpressions;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Services;

public sealed partial class DateResolutionService
{
    private static readonly DateSourceKind[] DefaultFallbackOrder =
    [
        DateSourceKind.ContentDerived,
        DateSourceKind.FileName,
        DateSourceKind.ModifiedTime,
        DateSourceKind.CreatedTime
    ];

    public DateResolution Resolve(ScannedFile file, FileContentSnapshot content, OrganizationSettings settings)
    {
        foreach (var source in BuildSourceOrder(settings.OrganizationPolicy.PreferredDateSource))
        {
            var resolution = source switch
            {
                DateSourceKind.ContentDerived => TryResolveFromText(content.Text),
                DateSourceKind.FileName => TryResolveFromText(Path.GetFileNameWithoutExtension(file.FileName)),
                DateSourceKind.ModifiedTime => CreateTimestampResolution(file.ModifiedUtc, DateSourceKind.ModifiedTime, "Using file modified time."),
                DateSourceKind.CreatedTime => CreateTimestampResolution(file.CreatedUtc, DateSourceKind.CreatedTime, "Using file created time."),
                _ => new DateResolution()
            };

            if (resolution.Value is null)
            {
                continue;
            }

            if (!settings.OrganizationPolicy.OnlyCreateDateFoldersWhenReliable || resolution.IsReliable)
            {
                return resolution;
            }
        }

        return new DateResolution
        {
            Explanation = "No sufficiently reliable date source was available."
        };
    }

    private static IEnumerable<DateSourceKind> BuildSourceOrder(DateSourceKind preferredDateSource)
    {
        yield return preferredDateSource;

        foreach (var source in DefaultFallbackOrder)
        {
            if (source != preferredDateSource)
            {
                yield return source;
            }
        }
    }

    private static DateResolution TryResolveFromText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new DateResolution();
        }

        foreach (Match match in DateRegex().Matches(value))
        {
            var candidate = match.Value.Trim();
            if (!TryParseDate(candidate, out var parsed))
            {
                continue;
            }

            var source = value.Length > 80 ? DateSourceKind.ContentDerived : DateSourceKind.FileName;
            return new DateResolution
            {
                Value = parsed,
                Source = source,
                Confidence = source == DateSourceKind.ContentDerived ? 0.88d : 0.82d,
                Explanation = source == DateSourceKind.ContentDerived
                    ? $"Found date '{candidate}' in extracted text."
                    : $"Found date '{candidate}' in the file name."
            };
        }

        return new DateResolution();
    }

    private static bool TryParseDate(string value, out DateTimeOffset parsed)
    {
        var formats = new[]
        {
            "dd.MM.yyyy",
            "d.M.yyyy",
            "yyyy-MM-dd",
            "yyyy_MM_dd",
            "yyyyMMdd",
            "dd-MM-yyyy",
            "dd_MM_yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(
                    value,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                    out var dateTime))
            {
                parsed = new DateTimeOffset(dateTime);
                return true;
            }
        }

        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.AssumeLocal, out var deDate))
        {
            parsed = new DateTimeOffset(deDate);
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var invariantDate))
        {
            parsed = new DateTimeOffset(invariantDate);
            return true;
        }

        parsed = default;
        return false;
    }

    private static DateResolution CreateTimestampResolution(DateTimeOffset value, DateSourceKind source, string explanation) =>
        new()
        {
            Value = value,
            Source = source,
            Confidence = source == DateSourceKind.ModifiedTime ? 0.72d : 0.68d,
            Explanation = explanation
        };

    [GeneratedRegex(@"\b(\d{1,2}[._-]\d{1,2}[._-]\d{4}|\d{4}[._-]\d{2}[._-]\d{2}|\d{8})\b")]
    private static partial Regex DateRegex();
}
