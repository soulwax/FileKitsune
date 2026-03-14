using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class DuplicateDetectionService
{
    private readonly IFileHashProvider fileHashProvider;
    private readonly ILogger<DuplicateDetectionService> logger;

    public DuplicateDetectionService(IFileHashProvider fileHashProvider, ILogger<DuplicateDetectionService> logger)
    {
        this.fileHashProvider = fileHashProvider;
        this.logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, DuplicateMatch>> DetectAsync(
        IReadOnlyList<ScannedFile> files,
        DuplicatePolicy policy,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!policy.EnableExactDuplicateDetection)
        {
            return new Dictionary<string, DuplicateMatch>(StringComparer.OrdinalIgnoreCase);
        }

        var candidatesBySize = files
            .Where(file => file.SizeBytes > 0)
            .GroupBy(file => file.SizeBytes)
            .Where(group => group.Count() > 1)
            .ToList();

        var matches = new Dictionary<string, DuplicateMatch>(StringComparer.OrdinalIgnoreCase);
        var total = candidatesBySize.Sum(group => group.Count());
        var processed = 0;

        foreach (var group in candidatesBySize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hashes = new List<(ScannedFile File, string Hash)>();
            foreach (var file in group.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var hash = await fileHashProvider.ComputeHashAsync(file.FullPath, cancellationToken);
                    hashes.Add((file, hash));
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Failed to hash {File}", file.RelativePath);
                }
                finally
                {
                    processed++;
                    progress?.Report(new WorkflowProgress
                    {
                        Stage = "duplicates",
                        Processed = processed,
                        Total = total,
                        Message = $"Checked {processed} of {total} files for exact duplicates."
                    });
                }
            }

            foreach (var hashGroup in hashes.GroupBy(item => item.Hash, StringComparer.Ordinal))
            {
                var ordered = hashGroup.OrderBy(item => item.File.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
                if (ordered.Count <= 1)
                {
                    continue;
                }

                var canonical = ordered[0].File.RelativePath;
                foreach (var duplicate in ordered.Skip(1))
                {
                    matches[duplicate.File.RelativePath] = new DuplicateMatch
                    {
                        IsDuplicate = true,
                        ContentHash = duplicate.Hash,
                        CanonicalRelativePath = canonical
                    };
                }
            }
        }

        return matches;
    }
}
