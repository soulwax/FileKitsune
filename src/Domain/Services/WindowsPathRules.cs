using System.Text;
using System.Text.RegularExpressions;
using FileTransformer.Domain.Models;

namespace FileTransformer.Domain.Services;

public static partial class WindowsPathRules
{
    private static readonly HashSet<string> ReservedNames =
    [
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    ];

    public static PathValidationResult ValidateRelativePath(string relativePath)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            errors.Add("The destination path is empty.");
            return new PathValidationResult { IsValid = false, Errors = errors };
        }

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim();

        if (Path.IsPathRooted(normalized))
        {
            errors.Add("The destination path must remain relative to the selected root directory.");
        }

        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            errors.Add("The destination path contains no usable segments.");
        }

        var sanitizedSegments = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                errors.Add("Path traversal segments are not allowed.");
                continue;
            }

            var sanitized = SanitizePathSegment(segment);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                errors.Add($"The path segment '{segment}' is not valid on Windows.");
                continue;
            }

            sanitizedSegments.Add(sanitized);
        }

        if (normalized.Length > 240)
        {
            errors.Add("The resulting relative path is close to the Windows long-path threshold.");
        }

        return new PathValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            NormalizedRelativePath = string.Join(Path.DirectorySeparatorChar, sanitizedSegments)
        };
    }

    public static string SanitizePathSegment(string segment)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(segment.Length);

        foreach (var character in segment.Normalize(NormalizationForm.FormC))
        {
            builder.Append(invalidChars.Contains(character) ? '-' : character);
        }

        var sanitized = MultiWhitespaceRegex().Replace(builder.ToString(), " ").Trim().TrimEnd('.', ' ');

        if (ReservedNames.Contains(sanitized.ToUpperInvariant()))
        {
            sanitized = $"{sanitized}_item";
        }

        return sanitized;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
