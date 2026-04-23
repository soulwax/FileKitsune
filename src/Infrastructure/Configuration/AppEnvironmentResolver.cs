namespace FileTransformer.Infrastructure.Configuration;

public sealed class AppEnvironmentResolver
{
    private readonly IReadOnlyDictionary<string, string>? processEnvironment;
    private readonly IReadOnlyDictionary<string, string>? dotEnvValues;
    private readonly string? currentDirectory;
    private readonly string? baseDirectory;

    public AppEnvironmentResolver()
        : this(null, null)
    {
    }

    public AppEnvironmentResolver(
        IReadOnlyDictionary<string, string>? processEnvironment,
        IReadOnlyDictionary<string, string>? dotEnvValues,
        string? currentDirectory = null,
        string? baseDirectory = null)
    {
        this.processEnvironment = processEnvironment;
        this.dotEnvValues = dotEnvValues;
        this.currentDirectory = currentDirectory;
        this.baseDirectory = baseDirectory;
    }

    public ResolvedEnvironmentValue? GetValue(params string[] keys)
    {
        var activeProcessEnvironment = processEnvironment ?? LoadProcessEnvironment();
        var activeDotEnvValues = dotEnvValues ?? DotEnv.LoadIfPresent(AppEnvironmentPaths.GetCandidateEnvPaths(currentDirectory, baseDirectory).ToArray());

        foreach (var key in keys)
        {
            if (activeProcessEnvironment.TryGetValue(key, out var processValue) && !string.IsNullOrWhiteSpace(processValue))
            {
                return new ResolvedEnvironmentValue(key, processValue, "Process");
            }

            if (activeDotEnvValues.TryGetValue(key, out var dotEnvValue) && !string.IsNullOrWhiteSpace(dotEnvValue))
            {
                return new ResolvedEnvironmentValue(key, dotEnvValue, ".env");
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> LoadProcessEnvironment()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Environment.GetEnvironmentVariables().Keys)
        {
            if (key is not string name)
            {
                continue;
            }

            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[name] = value;
            }
        }

        return result;
    }
}

public sealed record ResolvedEnvironmentValue(string Key, string Value, string Source);
