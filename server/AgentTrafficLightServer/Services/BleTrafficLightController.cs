using System.Globalization;
using AgentTrafficLight.Server.Configuration;
using AgentTrafficLight.Server.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Controls the AgentCore-Light traffic-light hardware over BLE by writing
/// JSON status payloads to the vendor GATT characteristic.
/// </summary>
public sealed class BleTrafficLightController : ITrafficLightController, IAsyncDisposable
{
    private readonly BleOptions _options;
    private readonly ILogger<BleTrafficLightController> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private BluetoothLEDevice? _device;
    private GattSession? _session;
    private GattCharacteristic? _characteristic;
    private bool _disposed;

    /// <inheritdoc />
    public TrafficLightState CurrentState { get; private set; } = TrafficLightState.Off;

    /// <summary>
    /// Initializes a new instance of the <see cref="BleTrafficLightController"/> class.
    /// </summary>
    /// <param name="options">BLE connection options.</param>
    /// <param name="logger">The logger.</param>
    public BleTrafficLightController(IOptions<BleOptions> options, ILogger<BleTrafficLightController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (state == CurrentState)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            if (_characteristic == null)
            {
                _logger.LogWarning("BLE characteristic not available; dropping state {State}", state);
                return;
            }

            var json = $"{{\"status\":\"{state.ToCommandString()}\"}}";
            var writer = new DataWriter();
            writer.WriteString(json);
            var buffer = writer.DetachBuffer();

            var writeOption = _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                ? GattWriteOption.WriteWithoutResponse
                : GattWriteOption.WriteWithResponse;

            var result = await _characteristic.WriteValueAsync(buffer, writeOption)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (result == GattCommunicationStatus.Success)
            {
                CurrentState = state;
                _logger.LogInformation("Traffic light state changed to {State} ({Json})", state, json);
            }
            else
            {
                _logger.LogWarning("Failed to write BLE state {State}: {Status}", state, result);
                _characteristic = null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_session is { SessionStatus: GattSessionStatus.Active } && _characteristic != null)
        {
            return;
        }

        DisposeConnection();

        _device = await GetDeviceAsync(cancellationToken).ConfigureAwait(false);

        if (_device == null)
        {
            throw new InvalidOperationException($"BLE device {_options.DeviceAddress} was not found.");
        }

        _logger.LogInformation("BLE device object obtained for {Address}", _options.DeviceAddress);

        _session = await GattSession.FromDeviceIdAsync(_device.BluetoothDeviceId)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);

        if (_session == null)
        {
            throw new InvalidOperationException("Failed to create GATT session.");
        }

        if (!_session.CanMaintainConnection)
        {
            throw new InvalidOperationException("Device does not support GATT sessions.");
        }

        var tcs = new TaskCompletionSource();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        TypedEventHandler<GattSession, GattSessionStatusChangedEventArgs> handler = (sender, args) =>
        {
            _logger.LogDebug(
                "GATT session status changed to {Status} with error {Error}",
                args.Status,
                args.Error);

            if (args.Status == GattSessionStatus.Active)
            {
                tcs.TrySetResult();
            }
        };

        _session.SessionStatusChanged += handler;
        try
        {
            _session.MaintainConnection = true;

            var service = await GetServiceAsync(cancellationToken).ConfigureAwait(false);
            _characteristic = await GetCharacteristicAsync(service, cancellationToken).ConfigureAwait(false);

            await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            _logger.LogInformation("BLE GATT session active");
        }
        finally
        {
            _session.SessionStatusChanged -= handler;
        }
    }

    private async Task<GattDeviceService> GetServiceAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await _device!.GetGattServicesForUuidAsync(
                    new Guid(_options.ServiceUuid),
                    BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "BLE service discovery attempt {Attempt}: {Status}",
                attempt,
                result.Status);

            if (result.Status == GattCommunicationStatus.Success && result.Services.Count > 0)
            {
                return result.Services[0];
            }

            if (result.Status == GattCommunicationStatus.Unreachable && attempt < maxAttempts)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new InvalidOperationException($"BLE service {_options.ServiceUuid} was not found: {result.Status}.");
        }

        throw new InvalidOperationException($"BLE service {_options.ServiceUuid} was not found.");
    }

    private async Task<GattCharacteristic> GetCharacteristicAsync(
        GattDeviceService service,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await service.GetCharacteristicsForUuidAsync(
                    new Guid(_options.CharacteristicUuid),
                    BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "BLE characteristic discovery attempt {Attempt}: {Status}",
                attempt,
                result.Status);

            if (result.Status == GattCommunicationStatus.Success && result.Characteristics.Count > 0)
            {
                return result.Characteristics[0];
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"BLE characteristic {_options.CharacteristicUuid} was not found.");
    }

    private async Task<BluetoothLEDevice?> GetDeviceAsync(CancellationToken cancellationToken)
    {
        // Prefer already-enumerated devices so we can connect without scanning.
        var device = await GetEnumeratedDeviceAsync(cancellationToken).ConfigureAwait(false);
        if (device != null)
        {
            return device;
        }

        // Fall back to active scanning. Windows desktop apps often need a live
        // advertisement watcher before they can connect to an unpaired peripheral.
        return await ScanForDeviceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<BluetoothLEDevice?> GetEnumeratedDeviceAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.DeviceAddress))
        {
            var address = ParseMacAddress(_options.DeviceAddress);
            _logger.LogInformation("Looking up BLE device by address {Address}", _options.DeviceAddress);

            var selector = BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(address);
            var devices = await DeviceInformation.FindAllAsync(selector)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (devices.Count > 0)
            {
                return await BluetoothLEDevice.FromIdAsync(devices[0].Id)
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrEmpty(_options.DeviceName))
        {
            _logger.LogInformation("Looking up BLE device by name {Name}", _options.DeviceName);

            var selector = BluetoothLEDevice.GetDeviceSelectorFromDeviceName(_options.DeviceName);
            var devices = await DeviceInformation.FindAllAsync(selector)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (devices.Count > 0)
            {
                return await BluetoothLEDevice.FromIdAsync(devices[0].Id)
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return null;
    }

    private async Task<BluetoothLEDevice?> ScanForDeviceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting BLE advertisement scan");

        var tcs = new TaskCompletionSource<ulong?>();
        var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active,
        };

        watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(2000);
        watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(0);

        TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs> handler =
            (_, args) =>
            {
                if (tcs.Task.IsCompleted)
                {
                    return;
                }

                var address = FormatMacAddress(args.BluetoothAddress);
                _logger.LogDebug(
                    "BLE advertisement from {Address} (local name: {LocalName}, rssi: {Rssi})",
                    address,
                    args.Advertisement.LocalName,
                    args.RawSignalStrengthInDBm);

                if (MatchesOptions(args))
                {
                    _logger.LogInformation(
                        "Found matching BLE device {Address} ({LocalName})",
                        address,
                        args.Advertisement.LocalName);
                    tcs.TrySetResult(args.BluetoothAddress);
                }
            };

        watcher.Received += handler;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(_options.ScanTimeoutMs));

        try
        {
            watcher.Start();

            var address = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            if (!address.HasValue)
            {
                return null;
            }

            return await BluetoothLEDevice.FromBluetoothAddressAsync(address.Value)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("BLE advertisement scan timed out");
            return null;
        }
        finally
        {
            watcher.Received -= handler;
            watcher.Stop();
        }
    }

    private bool MatchesOptions(BluetoothLEAdvertisementReceivedEventArgs args)
    {
        if (!string.IsNullOrEmpty(_options.DeviceAddress))
        {
            var scannedAddress = FormatMacAddress(args.BluetoothAddress);
            if (string.Equals(scannedAddress, _options.DeviceAddress, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(_options.DeviceName))
        {
            if (string.Equals(args.Advertisement.LocalName, _options.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ulong ParseMacAddress(string address)
    {
        var parts = address.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6)
        {
            throw new ArgumentException($"Invalid MAC address: {address}", nameof(address));
        }

        ulong value = 0;
        foreach (var part in parts)
        {
            value = (value << 8) | byte.Parse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static string FormatMacAddress(ulong address)
    {
        return string.Join(
            ":",
            Enumerable.Range(0, 6)
                .Select(i => ((address >> ((5 - i) * 8)) & 0xFF).ToString("X2", CultureInfo.InvariantCulture)));
    }

    private void DisposeConnection()
    {
        _characteristic = null;
        _session?.Dispose();
        _session = null;
        _device?.Dispose();
        _device = null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        DisposeConnection();
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
