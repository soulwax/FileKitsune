using System.Security.Cryptography;
using FileTransformer.Infrastructure.FileSystem;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class Sha256FileHashProviderTests
{
    [Fact]
    public async Task ComputeHashAsync_ProducesStableSha256ForLargeFile()
    {
        var filePath = Path.GetTempFileName();

        try
        {
            var data = new byte[1024 * 1024 + 137];
            for (var index = 0; index < data.Length; index++)
            {
                data[index] = (byte)(index % 251);
            }

            await File.WriteAllBytesAsync(filePath, data);

            var provider = new Sha256FileHashProvider();
            var expected = Convert.ToHexString(SHA256.HashData(data));

            var actual = await provider.ComputeHashAsync(filePath, CancellationToken.None);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
