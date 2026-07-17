using Microsoft.Extensions.Logging;

namespace AgentSignalBridge.Server.Tests.Fakes;

/// <summary>
/// In-memory logger that records written log entries for test assertions.
/// </summary>
/// <typeparam name="TCategoryName">The category name for the logger.</typeparam>
public sealed class FakeLogger<TCategoryName> : ILogger<TCategoryName>
{
    private readonly List<LogEntry> _entries = [];

    /// <summary>
    /// Gets the log entries written to this logger.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries.AsReadOnly();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
    }

    /// <summary>
    /// A recorded log entry.
    /// </summary>
    /// <param name="LogLevel">The log level.</param>
    /// <param name="Exception">The logged exception, if any.</param>
    /// <param name="Message">The formatted log message.</param>
    public sealed record LogEntry(LogLevel LogLevel, Exception? Exception, string Message);
}
