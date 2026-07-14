using System.IO.Ports;
using System.Runtime.Versioning;
using System.Text;
using AgentTrafficLight.Server.Configuration;
using Microsoft.Extensions.Options;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Manages the serial connection to the AgentCore-Light hardware, including
/// automatic port detection, command writing, and reconnect logic.
/// </summary>
public sealed class SerialController : ISerialController, IDisposable
{
    private readonly SerialOptions _options;
    private readonly ILogger<SerialController> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Timer _reconnectTimer;
    private SerialPort? _port;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsConnected => _port is { IsOpen: true };

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialController"/> class.
    /// </summary>
    /// <param name="options">Serial connection options.</param>
    /// <param name="logger">The logger.</param>
    public SerialController(IOptions<SerialOptions> options, ILogger<SerialController> logger)
    {
        _options = options.Value;
        _logger = logger;
        var interval = TimeSpan.FromMilliseconds(_options.ReconnectIntervalMs);
        _reconnectTimer = new Timer(
            _ => { _ = ReconnectAsync(); },
            null,
            interval,
            interval);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            if (_port is not { IsOpen: true })
            {
                _logger.LogWarning("Serial port not connected; dropping command '{Command}'", command);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(command + "\n");
            await _port.BaseStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Wrote command '{Command}' to serial port", command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write command '{Command}' to serial port", command);
            ClosePort();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ReconnectAsync()
    {
        if (_disposed || IsConnected)
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _lock.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        try
        {
            ClosePort();

            var portName = _options.Port;
            if (string.Equals(portName, "auto", StringComparison.OrdinalIgnoreCase))
            {
                portName = DetectPort();
                if (string.IsNullOrEmpty(portName))
                {
                    _logger.LogWarning("No serial port auto-detected");
                    return;
                }
            }

            _logger.LogInformation(
                "Opening serial port {Port} at {BaudRate} baud",
                portName,
                _options.BaudRate);

            _port = new SerialPort(portName, _options.BaudRate)
            {
                DtrEnable = _options.DtrEnable,
                RtsEnable = _options.RtsEnable,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                Encoding = Encoding.UTF8
            };
            _port.Open();
            _logger.LogInformation("Serial port {Port} opened", portName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port");
            ClosePort();
        }
    }

    private string? DetectPort()
    {
        var portNames = SerialPort.GetPortNames();
        if (portNames.Length == 0)
        {
            return null;
        }

        var descriptions = GetPortDescriptions();
        return SerialPortScorer.BestPort(portNames, descriptions);
    }

    private Dictionary<string, string> GetPortDescriptions()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsPortDescriptions();
        }

        if (OperatingSystem.IsLinux())
        {
            return GetLinuxPortDescriptions();
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    [SupportedOSPlatform("windows")]
    private Dictionary<string, string> GetWindowsPortDescriptions()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                var caption = obj["Caption"]?.ToString();
                var description = obj["Description"]?.ToString();
                var text = string.Join(
                    " ",
                    new[] { name, caption, description }.Where(s => !string.IsNullOrWhiteSpace(s)));

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var match = System.Text.RegularExpressions.Regex.Match(
                    text,
                    @"\(COM\d+\)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var comPort = match.Value.Trim('(', ')');
                    result[comPort] = text;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Windows serial port descriptions");
        }

        return result;
    }

    private static Dictionary<string, string> GetLinuxPortDescriptions()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        const string byIdPath = "/dev/serial/by-id";
        try
        {
            if (!Directory.Exists(byIdPath))
            {
                return result;
            }

            foreach (var file in Directory.EnumerateFiles(byIdPath))
            {
                var target = Path.GetFullPath(file);
                var fileName = Path.GetFileName(file);
                result[target] = fileName;
            }
        }
        catch
        {
            // Best-effort: ignore enumeration failures.
        }

        return result;
    }

    private void ClosePort()
    {
        try
        {
            _port?.Close();
        }
        catch
        {
            // Ignore close errors.
        }

        _port?.Dispose();
        _port = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reconnectTimer.Dispose();
        ClosePort();
        _lock.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
