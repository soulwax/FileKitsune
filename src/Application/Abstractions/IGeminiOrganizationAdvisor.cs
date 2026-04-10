using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Abstractions;

public interface IGeminiOrganizationAdvisor
{
    Task<OrganizationGuidance?> AdviseAsync(
        IReadOnlyList<FileAnalysisContext> contexts,
        OrganizationSettings settings,
        GeminiOptions options,
        CancellationToken cancellationToken);
}
