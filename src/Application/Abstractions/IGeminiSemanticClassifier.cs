using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Abstractions;

public interface IGeminiSemanticClassifier
{
    Task<SemanticInsight?> ClassifyAsync(
        SemanticAnalysisRequest request,
        GeminiOptions options,
        CancellationToken cancellationToken);
}
