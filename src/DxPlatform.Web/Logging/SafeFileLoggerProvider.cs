using System.Collections.Concurrent;
using System.Text.Json;

namespace DxPlatform.Web.Logging;

public sealed class SafeFileLoggerProvider(string pathTemplate)
    : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, SafeFileLogger> _loggers = new();
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name =>
            new SafeFileLogger(name, pathTemplate, () => _scopeProvider));

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        _scopeProvider = scopeProvider;

    public void Dispose() => _loggers.Clear();

    private sealed class SafeFileLogger(
        string category,
        string configuredPath,
        Func<IExternalScopeProvider> scopeProvider) : ILogger
    {
        private static readonly object WriteLock = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            scopeProvider().Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

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

            var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["LogLevel"] = logLevel.ToString(),
                ["EventId"] = eventId.Id,
                ["Category"] = category,
                ["Message"] = formatter(state, exception)
            };

            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var value in values.Where(value => value.Key != "{OriginalFormat}"))
                {
                    properties[value.Key] = value.Value;
                }
            }

            scopeProvider().ForEachScope((scope, target) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                {
                    foreach (var value in scopeValues)
                    {
                        target[value.Key] = value.Value;
                    }
                }
            }, properties);

            var path = configuredPath.Replace(
                "-.log",
                $"-{DateTime.UtcNow:yyyyMMdd}.log",
                StringComparison.Ordinal);
            var directory = Path.GetDirectoryName(path);

            try
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var line = JsonSerializer.Serialize(properties);
                lock (WriteLock)
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch (IOException)
            {
                // ログ出力障害で業務処理を停止させない。
            }
            catch (UnauthorizedAccessException)
            {
                // 配置先の権限は運用チェックリストで検証する。
            }
        }
    }
}

