using Microsoft.Extensions.Logging;

namespace FileTransformer.App.Services;

public sealed class UiLoggerProvider : ILoggerProvider
{
    private readonly UiLogStore logStore;

    public UiLoggerProvider(UiLogStore logStore)
    {
        this.logStore = logStore;
    }

    public ILogger CreateLogger(string categoryName) => new UiLogger(categoryName, logStore);

    public void Dispose()
    {
    }

    private sealed class UiLogger : ILogger
    {
        private readonly string categoryName;
        private readonly UiLogStore logStore;

        public UiLogger(string categoryName, UiLogStore logStore)
        {
            this.categoryName = categoryName;
            this.logStore = logStore;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (exception is not null)
            {
                message = $"{message} ({exception.Message})";
            }

            logStore.Add(new UiLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel.ToString(),
                Category = categoryName,
                Message = message
            });
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
