using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileTransformer.Infrastructure.Configuration;

public static class DotEnv
{
    public static IReadOnlyDictionary<string, string> LoadIfPresent(params string[] candidatePaths)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidatePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                if (!TryParseLine(line, out var key, out var value))
                {
                    continue;
                }

                if (seen.Add(key))
                {
                    values[key] = value;
                }
            }
        }

        return values;
    }

    private static bool TryParseLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith('#') || trimmed.StartsWith(';'))
        {
            return false;
        }

        if (trimmed.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..].Trim();
        }

        var separatorIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        key = trimmed[..separatorIndex].Trim();
        value = trimmed[(separatorIndex + 1)..].Trim();

        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        return !string.IsNullOrWhiteSpace(key);
    }
}
