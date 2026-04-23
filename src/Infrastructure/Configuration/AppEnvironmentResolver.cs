namespace FileTransformer.Infrastructure.Configuration;

public sealed class AppEnvironmentResolver
{
    private readonly IReadOnlyDictionary<string, string> processEnvironment;
    private readonly IReadOnlyDictionary<string, string> dotEnvValues;

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
        this.processEnvironment = processEnvironment ?? LoadProcessEnvironment();
        this.dotEnvValues = dotEnvValues ?? DotEnv.LoadIfPresent(
            Path.Combine(currentDirectory ?? Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(baseDirectory ?? AppContext.BaseDirectory, ".env"));
    }

    public ResolvedEnvironmentValue? GetValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (processEnvironment.TryGetValue(key, out var processValue) && !string.IsNullOrWhiteSpace(processValue))
            {
                return new ResolvedEnvironmentValue(key, processValue, "Process");
            }

            if (dotEnvValues.TryGetValue(key, out var dotEnvValue) && !string.IsNullOrWhiteSpace(dotEnvValue))
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
