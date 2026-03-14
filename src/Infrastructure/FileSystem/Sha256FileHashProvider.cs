using System.Security.Cryptography;
using FileTransformer.Application.Abstractions;

namespace FileTransformer.Infrastructure.FileSystem;

public sealed class Sha256FileHashProvider : IFileHashProvider
{
    public async Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes);
    }
}
