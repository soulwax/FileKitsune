using FileTransformer.Application.Models;

namespace FileTransformer.Application.Abstractions;

public interface IEnvironmentSanityService
{
    Task<IReadOnlyList<EnvironmentSanityItem>> GetChecklistAsync(CancellationToken cancellationToken);

    Task<EnvironmentPingAvailability> GetGeminiPingAvailabilityAsync(GeminiOptions settings, CancellationToken cancellationToken);

    Task<EnvironmentPingResult> PingGeminiAsync(GeminiOptions settings, CancellationToken cancellationToken);
}
