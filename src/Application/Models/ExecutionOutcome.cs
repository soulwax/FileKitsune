using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Models;

public sealed class ExecutionOutcome
{
    public int RequestedOperations { get; init; }

    public int SuccessfulOperations { get; init; }

    public int SkippedOperations { get; init; }

    public int FailedOperations { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string SummaryResourceKey { get; init; } = string.Empty;

    public object[] SummaryArguments { get; init; } = [];

    public ExecutionJournal? Journal { get; init; }

    public List<string> Messages { get; init; } = [];
}
