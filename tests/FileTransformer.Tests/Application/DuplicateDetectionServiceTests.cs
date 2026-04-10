using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Services;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class DuplicateDetectionServiceTests
{
    [Fact]
    public async Task DetectAsync_ReturnsEmptyWhenDisabled()
    {
        var service = new DuplicateDetectionService(new FakeHashProvider(), NullLogger<DuplicateDetectionService>.Instance);
        var policy = new DuplicatePolicy { EnableExactDuplicateDetection = false };

        var results = await service.DetectAsync(
            [
                new ScannedFile { FullPath = @"C:\Root\a.txt", RelativePath = "a.txt", SizeBytes = 10 },
                new ScannedFile { FullPath = @"C:\Root\b.txt", RelativePath = "b.txt", SizeBytes = 10 }
            ],
            policy,
            progress: null,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task DetectAsync_FlagsDuplicatesByHash()
    {
        var provider = new FakeHashProvider
        {
            Hashes =
            {
                [@"C:\Root\a.txt"] = "HASH-1",
                [@"C:\Root\b.txt"] = "HASH-1",
                [@"C:\Root\c.txt"] = "HASH-2"
            }
        };

        var service = new DuplicateDetectionService(provider, NullLogger<DuplicateDetectionService>.Instance);
        var policy = new DuplicatePolicy { EnableExactDuplicateDetection = true };

        var results = await service.DetectAsync(
            [
                new ScannedFile { FullPath = @"C:\Root\b.txt", RelativePath = "b.txt", SizeBytes = 10 },
                new ScannedFile { FullPath = @"C:\Root\a.txt", RelativePath = "a.txt", SizeBytes = 10 },
                new ScannedFile { FullPath = @"C:\Root\c.txt", RelativePath = "c.txt", SizeBytes = 10 }
            ],
            policy,
            progress: null,
            CancellationToken.None);

        Assert.True(results.TryGetValue("b.txt", out var match));
        Assert.Equal("HASH-1", match.ContentHash);
        Assert.Equal("a.txt", match.CanonicalRelativePath);
        Assert.False(results.ContainsKey("a.txt"));
        Assert.False(results.ContainsKey("c.txt"));
    }

    [Fact]
    public async Task DetectAsync_PrefersCleanerPathBeforeAlphabeticalOrder()
    {
        var provider = new FakeHashProvider
        {
            Hashes =
            {
                [@"C:\Root\Review\a.txt"] = "HASH-1",
                [@"C:\Root\Projects\b.txt"] = "HASH-1"
            }
        };

        var service = new DuplicateDetectionService(provider, NullLogger<DuplicateDetectionService>.Instance);
        var policy = new DuplicatePolicy { EnableExactDuplicateDetection = true };

        var results = await service.DetectAsync(
            [
                new ScannedFile
                {
                    FullPath = @"C:\Root\Review\a.txt",
                    RelativePath = @"Review\a.txt",
                    RelativeDirectoryPath = "Review",
                    FileName = "a.txt",
                    SizeBytes = 10
                },
                new ScannedFile
                {
                    FullPath = @"C:\Root\Projects\b.txt",
                    RelativePath = @"Projects\b.txt",
                    RelativeDirectoryPath = "Projects",
                    FileName = "b.txt",
                    SizeBytes = 10
                }
            ],
            policy,
            progress: null,
            CancellationToken.None);

        Assert.True(results.TryGetValue(@"Review\a.txt", out var match));
        Assert.Equal(@"Projects\b.txt", match.CanonicalRelativePath);
    }

    [Fact]
    public async Task DetectAsync_PrefersOlderFileWhenPathQualityMatches()
    {
        var provider = new FakeHashProvider
        {
            Hashes =
            {
                [@"C:\Root\Docs\older.txt"] = "HASH-1",
                [@"C:\Root\Docs\newer.txt"] = "HASH-1"
            }
        };

        var service = new DuplicateDetectionService(provider, NullLogger<DuplicateDetectionService>.Instance);
        var policy = new DuplicatePolicy { EnableExactDuplicateDetection = true };

        var results = await service.DetectAsync(
            [
                new ScannedFile
                {
                    FullPath = @"C:\Root\Docs\newer.txt",
                    RelativePath = @"Docs\newer.txt",
                    RelativeDirectoryPath = "Docs",
                    FileName = "newer.txt",
                    SizeBytes = 10,
                    CreatedUtc = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
                    ModifiedUtc = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero)
                },
                new ScannedFile
                {
                    FullPath = @"C:\Root\Docs\older.txt",
                    RelativePath = @"Docs\older.txt",
                    RelativeDirectoryPath = "Docs",
                    FileName = "older.txt",
                    SizeBytes = 10,
                    CreatedUtc = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
                    ModifiedUtc = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)
                }
            ],
            policy,
            progress: null,
            CancellationToken.None);

        Assert.True(results.TryGetValue(@"Docs\newer.txt", out var match));
        Assert.Equal(@"Docs\older.txt", match.CanonicalRelativePath);
    }

    [Fact]
    public async Task DetectAsync_OnlyHashesFilesThatShareTheSameSize()
    {
        var provider = new TrackingHashProvider
        {
            Hashes =
            {
                [@"C:\Root\a.txt"] = "HASH-1",
                [@"C:\Root\b.txt"] = "HASH-1"
            }
        };

        var service = new DuplicateDetectionService(provider, NullLogger<DuplicateDetectionService>.Instance);
        var policy = new DuplicatePolicy { EnableExactDuplicateDetection = true };

        var results = await service.DetectAsync(
            [
                new ScannedFile { FullPath = @"C:\Root\a.txt", RelativePath = "a.txt", FileName = "a.txt", SizeBytes = 100 },
                new ScannedFile { FullPath = @"C:\Root\b.txt", RelativePath = "b.txt", FileName = "b.txt", SizeBytes = 100 },
                new ScannedFile { FullPath = @"C:\Root\c.txt", RelativePath = "c.txt", FileName = "c.txt", SizeBytes = 101 }
            ],
            policy,
            progress: null,
            CancellationToken.None);

        Assert.Equal(2, provider.RequestedPaths.Count);
        Assert.DoesNotContain(@"C:\Root\c.txt", provider.RequestedPaths);
        Assert.True(results.ContainsKey("b.txt"));
    }

    private sealed class FakeHashProvider : IFileHashProvider
    {
        public Dictionary<string, string> Hashes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Hashes.TryGetValue(fullPath, out var hash) ? hash : string.Empty);
        }
    }

    private sealed class TrackingHashProvider : IFileHashProvider
    {
        public Dictionary<string, string> Hashes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> RequestedPaths { get; } = [];

        public Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken)
        {
            RequestedPaths.Add(fullPath);
            return Task.FromResult(Hashes.TryGetValue(fullPath, out var hash) ? hash : string.Empty);
        }
    }
}
