using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class DateResolution
{
    public DateTimeOffset? Value { get; init; }

    public DateSourceKind Source { get; init; } = DateSourceKind.None;

    public double Confidence { get; init; }

    public string Explanation { get; init; } = string.Empty;

    public bool IsReliable => Value is not null && Confidence >= 0.65d;
}
