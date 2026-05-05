using FileTransformer.Infrastructure.FileSystem;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class RecycleBinServiceTests
{
    [Fact]
    public async Task RecycleFileAsync_ThrowsIfCancellationWasAlreadyRequested()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileKitsuneRecycleBinCancel_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "keep me");

        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var service = new RecycleBinService();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                service.RecycleFileAsync(path, cancellation.Token));

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task RecycleFileAsync_SendsExistingFileToRecycleBinOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"FileKitsuneRecycleBin_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "recycle me");

        try
        {
            var service = new RecycleBinService();
            await service.RecycleFileAsync(path, CancellationToken.None);

            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
