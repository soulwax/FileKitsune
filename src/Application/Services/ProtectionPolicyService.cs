using System.IO.Enumeration;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Services;

public sealed class ProtectionPolicyService
{
    private static readonly string[] SessionExtensions =
    [
        ".als",
        ".flp",
        ".logicx",
        ".cpr",
        ".ptx",
        ".session"
    ];

    private static readonly string[] SessionKeywords =
    [
        "session",
        "projekt",
        "project",
        "mix",
        "master",
        "semester"
    ];

    public (bool IsProtected, string Reason) Evaluate(ScannedFile file, OrganizationSettings settings)
    {
        var protection = settings.ProtectionPolicy;

        foreach (var pattern in protection.ProtectedFolderPatterns)
        {
            if (MatchesPattern(file.RelativeDirectoryPath, pattern))
            {
                return (true, $"Protected folder pattern matched: {pattern}");
            }
        }

        foreach (var pattern in protection.ProtectedFilePatterns)
        {
            if (MatchesPattern(file.RelativePath, pattern) || MatchesPattern(file.FileName, pattern))
            {
                return (true, $"Protected file pattern matched: {pattern}");
            }
        }

        if (protection.TreatProjectOrSessionFoldersAsAtomic && IsAtomicFolder(file))
        {
            return (true, "Atomic project/session folder preserved.");
        }

        return (false, string.Empty);
    }

    public IReadOnlyDictionary<string, string> BuildGroupingKeys(
        IReadOnlyList<ScannedFile> files,
        ProtectionPolicy policy)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byDirectory = files.GroupBy(file => file.RelativeDirectoryPath, StringComparer.OrdinalIgnoreCase);

        foreach (var directoryGroup in byDirectory)
        {
            var sameDirectoryFiles = directoryGroup.ToList();

            foreach (var file in sameDirectoryFiles)
            {
                if (policy.TreatProjectOrSessionFoldersAsAtomic && IsAtomicFolder(file))
                {
                    map[file.RelativePath] = $"atomic::{directoryGroup.Key}";
                    continue;
                }

                if (policy.KeepRelatedFilesTogetherByBasename)
                {
                    var basename = Path.GetFileNameWithoutExtension(file.FileName);
                    var related = sameDirectoryFiles.Count(candidate =>
                        string.Equals(
                            Path.GetFileNameWithoutExtension(candidate.FileName),
                            basename,
                            StringComparison.OrdinalIgnoreCase));

                    if (related > 1)
                    {
                        map[file.RelativePath] = $"basename::{directoryGroup.Key}::{basename}";
                        continue;
                    }
                }

                map[file.RelativePath] = $"file::{file.RelativePath}";
            }
        }

        return map;
    }

    private static bool MatchesPattern(string value, string pattern) =>
        !string.IsNullOrWhiteSpace(pattern) &&
        (FileSystemName.MatchesSimpleExpression(pattern, value, ignoreCase: true) ||
         value.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    private static bool IsAtomicFolder(ScannedFile file)
    {
        if (SessionExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return SessionKeywords.Any(keyword =>
            file.RelativeDirectoryPath.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
