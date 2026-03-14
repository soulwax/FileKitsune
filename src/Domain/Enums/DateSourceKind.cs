namespace FileTransformer.Domain.Enums;

public enum DateSourceKind
{
    None = 0,
    ContentDerived = 1,
    FileName = 2,
    ModifiedTime = 3,
    CreatedTime = 4
}
