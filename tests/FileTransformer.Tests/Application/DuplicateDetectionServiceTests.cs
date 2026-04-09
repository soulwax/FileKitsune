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

    private sealed class FakeHashProvider : IFileHashProvider
    {
        public Dictionary<string, string> Hashes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Hashes.TryGetValue(fullPath, out var hash) ? hash : string.Empty);
        }
    }
}
