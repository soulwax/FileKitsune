using FileTransformer.Domain.Models;
using FileTransformer.Domain.Services;

namespace FileTransformer.Application.Services;

public sealed class PathSafetyService
{
    public PathValidationResult ValidateDestination(string rootDirectory, string relativePath)
    {
        var relativeValidation = WindowsPathRules.ValidateRelativePath(relativePath);
        if (!relativeValidation.IsValid)
        {
            return relativeValidation;
        }

        var rootFullPath = NormalizeRoot(rootDirectory);
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, relativeValidation.NormalizedRelativePath));
        if (!IsWithinRoot(rootFullPath, candidate))
        {
            return new PathValidationResult
            {
                IsValid = false,
                NormalizedRelativePath = relativeValidation.NormalizedRelativePath,
                Errors = ["The destination path escapes the selected root directory."]
            };
        }

        return relativeValidation;
    }

    public string CombineWithinRoot(string rootDirectory, string relativePath)
    {
        var rootFullPath = NormalizeRoot(rootDirectory);
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
        if (!IsWithinRoot(rootFullPath, candidate))
        {
            throw new InvalidOperationException("The requested path is outside the selected root directory.");
        }

        return candidate;
    }

    public bool IsWithinRoot(string rootDirectory, string fullPath)
    {
        var root = NormalizeRoot(rootDirectory);
        var candidate = Path.GetFullPath(fullPath);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoot(string rootDirectory)
    {
        var fullPath = Path.GetFullPath(rootDirectory);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : $"{fullPath}{Path.DirectorySeparatorChar}";
    }
}
