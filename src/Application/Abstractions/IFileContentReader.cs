using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Abstractions;

public interface IFileContentReader
{
    Task<FileContentSnapshot> ReadAsync(
        ScannedFile file,
        OrganizationSettings settings,
        CancellationToken cancellationToken);
}
