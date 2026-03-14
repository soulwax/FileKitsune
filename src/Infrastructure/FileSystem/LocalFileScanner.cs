using System.IO.Enumeration;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Infrastructure.FileSystem;

public sealed class LocalFileScanner : IFileScanner
{
    private readonly ILogger<LocalFileScanner> logger;

    public LocalFileScanner(ILogger<LocalFileScanner> logger)
    {
        this.logger = logger;
    }

    public async Task<IReadOnlyList<ScannedFile>> ScanAsync(
        OrganizationSettings settings,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(
            () => ScanInternal(settings, progress, cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<ScannedFile> ScanInternal(
        OrganizationSettings settings,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<ScannedFile>();
        var root = Path.GetFullPath(settings.RootDirectory);
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pending.Pop();

            foreach (var directory in SafeEnumerateDirectories(currentDirectory))
            {
                var directoryInfo = new DirectoryInfo(directory);
                if (ShouldSkipDirectory(directoryInfo, settings))
                {
                    continue;
                }

                var relativeDirectory = Path.GetRelativePath(root, directory);
                if (ShouldExclude(relativeDirectory, settings.ExcludePatterns))
                {
                    continue;
                }

                pending.Push(directory);
            }

            foreach (var filePath in SafeEnumerateFiles(currentDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(root, filePath);

                if (ShouldExclude(relativePath, settings.ExcludePatterns) || !ShouldInclude(relativePath, settings.IncludePatterns))
                {
                    continue;
                }

                var info = new FileInfo(filePath);
                if (ShouldSkipFile(info, settings))
                {
                    continue;
                }

                results.Add(new ScannedFile
                {
                    FullPath = filePath,
                    RelativePath = relativePath,
                    RelativeDirectoryPath = Path.GetDirectoryName(relativePath) ?? string.Empty,
                    FileName = info.Name,
                    Extension = info.Extension,
                    SizeBytes = info.Exists ? info.Length : 0,
                    CreatedUtc = info.CreationTimeUtc,
                    ModifiedUtc = info.LastWriteTimeUtc,
                    IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden),
                    IsSystem = info.Attributes.HasFlag(FileAttributes.System),
                    IsReparsePoint = info.Attributes.HasFlag(FileAttributes.ReparsePoint)
                });

                if (results.Count % 25 == 0)
                {
                    progress?.Report(new WorkflowProgress
                    {
                        Stage = "scan",
                        Processed = results.Count,
                        Total = settings.MaxFilesToScan,
                        Message = $"Discovered {results.Count} files."
                    });
                }

                if (results.Count >= settings.MaxFilesToScan)
                {
                    return results;
                }
            }
        }

        return results;
    }

    private IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Skipping inaccessible directory {Directory}", directory);
            return [];
        }
    }

    private IEnumerable<string> SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Skipping inaccessible files in {Directory}", directory);
            return [];
        }
    }

    private static bool ShouldInclude(string relativePath, IReadOnlyCollection<string> includePatterns)
    {
        if (includePatterns.Count == 0 || includePatterns.All(pattern => string.IsNullOrWhiteSpace(pattern)))
        {
            return true;
        }

        return includePatterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, relativePath, ignoreCase: true)) ||
               includePatterns.Any(pattern => relativePath.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldExclude(string relativePath, IReadOnlyCollection<string> excludePatterns)
    {
        return excludePatterns.Any(pattern =>
            !string.IsNullOrWhiteSpace(pattern) &&
            (FileSystemName.MatchesSimpleExpression(pattern, relativePath, ignoreCase: true) ||
             relativePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ShouldSkipDirectory(DirectoryInfo directoryInfo, OrganizationSettings settings)
    {
        var attributes = directoryInfo.Attributes;
        if (!settings.ProtectionPolicy.FollowSymlinksOrJunctions && attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return true;
        }

        if (settings.ProtectionPolicy.SkipHiddenOrSystemFiles &&
            (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System)))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldSkipFile(FileInfo fileInfo, OrganizationSettings settings)
    {
        var attributes = fileInfo.Attributes;
        if (!settings.ProtectionPolicy.FollowSymlinksOrJunctions && attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return true;
        }

        if (settings.ProtectionPolicy.SkipHiddenOrSystemFiles &&
            (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System)))
        {
            return true;
        }

        return false;
    }
}
