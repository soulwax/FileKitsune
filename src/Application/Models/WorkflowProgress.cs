namespace FileTransformer.Application.Models;

public sealed class WorkflowProgress
{
    public string Stage { get; init; } = string.Empty;

    public int Processed { get; init; }

    public int Total { get; init; }

    public string Message { get; init; } = string.Empty;
}
