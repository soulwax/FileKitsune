using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Infrastructure.Configuration;

namespace FileTransformer.Infrastructure.FileSystem;

public sealed class DedupQuarantineService : IDedupQuarantineService
{
    private readonly AppStoragePaths paths;

    public DedupQuarantineService(AppStoragePaths paths)
    {
        this.paths = paths;
    }

    public async Task<DedupQuarantineRecord> QuarantineFileAsync(
        Guid runId,
        string rootDirectory,
        string sourceFullPath,
        string sourceRelativePath,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSource = Path.GetFullPath(sourceFullPath);
        if (!IsWithinRoot(rootDirectory, normalizedSource))
        {
            throw new InvalidOperationException("The duplicate file is outside the selected root folder.");
        }

        if (!File.Exists(normalizedSource))
        {
            throw new FileNotFoundException("The duplicate file no longer exists.", normalizedSource);
        }

        var runDirectory = GetRunDirectory(runId);
        var quarantinePath = ResolveQuarantinePath(runDirectory, sourceRelativePath);
        if (File.Exists(quarantinePath))
        {
            throw new IOException("A quarantined file already exists for this relative path.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(quarantinePath) ?? runDirectory);
        File.Move(normalizedSource, quarantinePath);
        await Task.CompletedTask;

        return new DedupQuarantineRecord
        {
            RunId = runId,
            OriginalFullPath = normalizedSource,
            OriginalRelativePath = sourceRelativePath,
            QuarantineFullPath = quarantinePath,
            QuarantineRunDirectory = runDirectory,
            SizeBytes = sizeBytes
        };
    }

    public async Task<DedupQuarantineRestoreResult> RestoreFileAsync(
        DedupQuarantineRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(record.QuarantineFullPath))
        {
            return new DedupQuarantineRestoreResult
            {
                Record = record,
                Status = "RestoreMissingQuarantine",
                Message = "The quarantined file is no longer available."
            };
        }

        if (File.Exists(record.OriginalFullPath))
        {
            return new DedupQuarantineRestoreResult
            {
                Record = record,
                Status = "RestoreSkippedOriginalExists",
                Message = "A file already exists at the original path."
            };
        }

        var originalDirectory = Path.GetDirectoryName(record.OriginalFullPath);
        if (!string.IsNullOrWhiteSpace(originalDirectory))
        {
            Directory.CreateDirectory(originalDirectory);
        }

        File.Move(record.QuarantineFullPath, record.OriginalFullPath);
        await Task.CompletedTask;

        return new DedupQuarantineRestoreResult
        {
            Record = record,
            Status = "Restored",
            Message = "The file was restored to its original path."
        };
    }

    public string GetRunDirectory(Guid runId) =>
        Path.Combine(paths.QuarantineDirectory, runId.ToString("N"));

    private static string ResolveQuarantinePath(string runDirectory, string sourceRelativePath)
    {
        if (Path.IsPathRooted(sourceRelativePath))
        {
            throw new InvalidOperationException("The quarantine relative path must not be rooted.");
        }

        var normalizedRunDirectory = NormalizeRoot(runDirectory);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRunDirectory, sourceRelativePath));
        if (!candidate.StartsWith(normalizedRunDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The quarantine path escapes the run folder.");
        }

        return candidate;
    }

    private static bool IsWithinRoot(string rootDirectory, string fullPath)
    {
        var root = NormalizeRoot(rootDirectory);
        var candidate = Path.GetFullPath(fullPath);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoot(string rootDirectory)
    {
        var fullPath = Path.GetFullPath(rootDirectory);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : $"{fullPath}{Path.DirectorySeparatorChar}";
    }
}
