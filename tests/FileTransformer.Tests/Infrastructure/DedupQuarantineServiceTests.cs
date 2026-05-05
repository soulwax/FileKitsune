using FileTransformer.Application.Models;
using FileTransformer.Infrastructure.Configuration;
using FileTransformer.Infrastructure.FileSystem;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class DedupQuarantineServiceTests
{
    [Fact]
    public async Task QuarantineFileAsync_MovesFileIntoRunFolderAndRemovesOriginal()
    {
        var fixture = CreateFixture();
        try
        {
            var source = Path.Combine(fixture.RootDirectory, "docs", "copy.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            await File.WriteAllTextAsync(source, "duplicate");

            var runId = Guid.NewGuid();
            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));

            var record = await service.QuarantineFileAsync(
                runId,
                fixture.RootDirectory,
                source,
                Path.Combine("docs", "copy.txt"),
                9,
                CancellationToken.None);

            Assert.False(File.Exists(source));
            Assert.True(File.Exists(record.QuarantineFullPath));
            Assert.Equal(runId, record.RunId);
            Assert.Equal(Path.GetFullPath(source), record.OriginalFullPath);
            Assert.Equal(Path.Combine("docs", "copy.txt"), record.OriginalRelativePath);
            Assert.Equal(service.GetRunDirectory(runId), record.QuarantineRunDirectory);
            Assert.StartsWith(record.QuarantineRunDirectory, record.QuarantineFullPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("duplicate", await File.ReadAllTextAsync(record.QuarantineFullPath));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    [Fact]
    public async Task QuarantineFileAsync_RejectsOutOfRootSource()
    {
        var fixture = CreateFixture();
        try
        {
            var outside = Path.Combine(fixture.WorkDirectory, "outside.txt");
            await File.WriteAllTextAsync(outside, "outside");

            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.QuarantineFileAsync(
                    Guid.NewGuid(),
                    fixture.RootDirectory,
                    outside,
                    "outside.txt",
                    7,
                    CancellationToken.None));

            Assert.True(File.Exists(outside));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    [Fact]
    public async Task QuarantineFileAsync_RejectsEscapingRelativePath()
    {
        var fixture = CreateFixture();
        try
        {
            var source = Path.Combine(fixture.RootDirectory, "copy.txt");
            await File.WriteAllTextAsync(source, "duplicate");

            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.QuarantineFileAsync(
                    Guid.NewGuid(),
                    fixture.RootDirectory,
                    source,
                    Path.Combine("..", "escape.txt"),
                    9,
                    CancellationToken.None));

            Assert.True(File.Exists(source));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    [Fact]
    public async Task QuarantineFileAsync_RejectsExistingQuarantineTarget()
    {
        var fixture = CreateFixture();
        try
        {
            var source = Path.Combine(fixture.RootDirectory, "copy.txt");
            await File.WriteAllTextAsync(source, "duplicate");

            var runId = Guid.NewGuid();
            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));
            var existingQuarantineTarget = Path.Combine(service.GetRunDirectory(runId), "copy.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(existingQuarantineTarget)!);
            await File.WriteAllTextAsync(existingQuarantineTarget, "already quarantined");

            await Assert.ThrowsAsync<IOException>(() =>
                service.QuarantineFileAsync(
                    runId,
                    fixture.RootDirectory,
                    source,
                    "copy.txt",
                    9,
                    CancellationToken.None));

            Assert.True(File.Exists(source));
            Assert.Equal("already quarantined", await File.ReadAllTextAsync(existingQuarantineTarget));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    [Fact]
    public async Task QuarantineFileAsync_CanceledBeforeMoveLeavesSourceInPlace()
    {
        var fixture = CreateFixture();
        try
        {
            var source = Path.Combine(fixture.RootDirectory, "copy.txt");
            await File.WriteAllTextAsync(source, "duplicate");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                service.QuarantineFileAsync(
                    Guid.NewGuid(),
                    fixture.RootDirectory,
                    source,
                    "copy.txt",
                    9,
                    cancellation.Token));

            Assert.True(File.Exists(source));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    [Fact]
    public async Task RestoreFileAsync_RestoresFileToOriginalPath()
    {
        var fixture = CreateFixture();
        try
        {
            var original = Path.Combine(fixture.RootDirectory, "nested", "copy.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
            await File.WriteAllTextAsync(original, "duplicate");

            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));
            var record = await service.QuarantineFileAsync(
                Guid.NewGuid(),
                fixture.RootDirectory,
                original,
                Path.Combine("nested", "copy.txt"),
                9,
                CancellationToken.None);

            var result = await service.RestoreFileAsync(record, CancellationToken.None);

            Assert.True(result.Restored);
            Assert.Equal("Restored", result.Status);
            Assert.True(File.Exists(original));
            Assert.False(File.Exists(record.QuarantineFullPath));
            Assert.Equal("duplicate", await File.ReadAllTextAsync(original));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    [Fact]
    public async Task RestoreFileAsync_SkipsWhenOriginalPathExists()
    {
        var fixture = CreateFixture();
        try
        {
            var original = Path.Combine(fixture.RootDirectory, "copy.txt");
            await File.WriteAllTextAsync(original, "duplicate");

            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));
            var record = await service.QuarantineFileAsync(
                Guid.NewGuid(),
                fixture.RootDirectory,
                original,
                "copy.txt",
                9,
                CancellationToken.None);
            await File.WriteAllTextAsync(original, "new file");

            var result = await service.RestoreFileAsync(record, CancellationToken.None);

            Assert.False(result.Restored);
            Assert.Equal("RestoreSkippedOriginalExists", result.Status);
            Assert.True(File.Exists(record.QuarantineFullPath));
            Assert.Equal("new file", await File.ReadAllTextAsync(original));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    [Fact]
    public async Task RestoreFileAsync_SkipsWhenQuarantineFileIsMissing()
    {
        var fixture = CreateFixture();
        try
        {
            var record = new DedupQuarantineRecord
            {
                RunId = Guid.NewGuid(),
                OriginalFullPath = Path.Combine(fixture.RootDirectory, "copy.txt"),
                OriginalRelativePath = "copy.txt",
                QuarantineFullPath = Path.Combine(fixture.AppDataDirectory, "quarantine", "missing.txt"),
                QuarantineRunDirectory = Path.Combine(fixture.AppDataDirectory, "quarantine"),
                SizeBytes = 9
            };
            var service = new DedupQuarantineService(new AppStoragePaths(fixture.AppDataDirectory));

            var result = await service.RestoreFileAsync(record, CancellationToken.None);

            Assert.False(result.Restored);
            Assert.Equal("RestoreMissingQuarantine", result.Status);
            Assert.False(File.Exists(record.OriginalFullPath));
        }
        finally
        {
            DeleteDirectory(fixture.WorkDirectory);
        }
    }

    private static QuarantineFixture CreateFixture()
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), $"FileKitsuneQuarantine_{Guid.NewGuid():N}");
        var rootDirectory = Path.Combine(workDirectory, "root");
        var appDataDirectory = Path.Combine(workDirectory, "appdata");
        Directory.CreateDirectory(rootDirectory);
        Directory.CreateDirectory(appDataDirectory);

        return new QuarantineFixture(workDirectory, rootDirectory, appDataDirectory);
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record QuarantineFixture(
        string WorkDirectory,
        string RootDirectory,
        string AppDataDirectory);
}
