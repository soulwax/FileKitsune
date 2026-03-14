using System;

namespace FileTransformer.Domain.Enums;

[Flags]
public enum OrganizationDimension
{
    None = 0,
    SemanticCategory = 1,
    Project = 2,
    Year = 4,
    Month = 8,
    FileType = 16
}
