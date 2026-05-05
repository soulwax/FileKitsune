using System.Runtime.Versioning;
using FileTransformer.Application.Abstractions;
using Microsoft.VisualBasic.FileIO;

namespace FileTransformer.Infrastructure.FileSystem;

[SupportedOSPlatform("windows")]
public sealed class RecycleBinService : IRecycleBinService
{
    public Task RecycleFileAsync(string fullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
            fullPath,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException);

        return Task.CompletedTask;
    }
}
