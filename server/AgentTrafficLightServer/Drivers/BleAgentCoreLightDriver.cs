using System.Globalization;
using AgentTrafficLight.Contracts.Drivers;
using AgentTrafficLight.Server.Configuration;
using AgentTrafficLight.Server.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace AgentTrafficLight.Server.Drivers;

/// <summary>
/// BLE driver for AgentCore-Light compatible hardware.
/// Writes JSON command payloads to the vendor GATT characteristic.
/// </summary>
public sealed class BleAgentCoreLightDriver : IAgentCoreLightDriver
{
    private readonly BleOptions _options;
    private readonly ILogger<BleAgentCoreLightDriver> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private BluetoothLEDevice? _device;
    private GattSession? _session;
    private GattCharacteristic? _characteristic;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BleAgentCoreLightDriver"/> class.
    /// </summary>
    /// <param name="options">BLE connection options.</param>
    /// <param name="logger">The logger.</param>
    public BleAgentCoreLightDriver(IOptions<BleOptions> options, ILogger<BleAgentCoreLightDriver> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConnected => !_disposed && _isConnected && _characteristic != null;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isConnected)
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

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var sessionActiveTask = EventAsync.WaitForEventAsync<GattSession, GattSessionStatusChangedEventArgs>(
                h => _session.SessionStatusChanged += h,
                h => _session.SessionStatusChanged -= h,
                args =>
                {
                    _logger.LogDebug(
                        "GATT session status changed to {Status} with error {Error}",
                        args.Status,
                        args.Error);
                    return args.Status == GattSessionStatus.Active;
                },
                cts.Token);

            _session.MaintainConnection = true;

            var service = await GetServiceAsync(cancellationToken).ConfigureAwait(false);
            _characteristic = await GetCharacteristicAsync(service, cancellationToken).ConfigureAwait(false);

            await sessionActiveTask.ConfigureAwait(false);
            _isConnected = true;
            _logger.LogInformation("BLE GATT session active");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DisposeConnection();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(AgentCoreLightCommand command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Lazy reconnect: if we are not currently connected, try to connect first.
        if (!IsConnected)
        {
            _logger.LogInformation("BLE driver is disconnected; reconnecting before writing command {Command}", command);
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        if (await TryWriteCommandAsync(command, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Write failed. Drop the connection and attempt one reconnect + retry.
        _logger.LogWarning("BLE write failed for command {Command}; reconnecting and retrying once", command);
        DisposeConnection();
        await ConnectAsync(cancellationToken).ConfigureAwait(false);

        if (!await TryWriteCommandAsync(command, cancellationToken).ConfigureAwait(false))
        {
            throw new IOException($"Failed to write BLE command {command} after reconnecting.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            DisposeConnection();
        }
        finally
        {
            _lock.Release();
        }

        _lock.Dispose();
    }

    private async Task<bool> TryWriteCommandAsync(AgentCoreLightCommand command, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isConnected || _characteristic == null)
            {
                return false;
            }

            var json = $"{{\"status\":\"{command.ToCommandString()}\"}}";
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
                _logger.LogInformation("AgentCoreLight command sent: {Command} ({Json})", command, json);
                return true;
            }

            _logger.LogWarning("Failed to write BLE command {Command}: {Status}", command, result);
            _isConnected = false;
            _characteristic = null;
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<BluetoothLEDevice?> GetDeviceAsync(CancellationToken cancellationToken)
    {
        // Always scan first. Windows desktop apps often cannot connect to an
        // unpaired peripheral unless a live advertisement watcher has recently
        // seen the device. Relying on stale DeviceInformation entries frequently
        // results in ERROR_NOT_READY (0x80070016) when accessing GATT services.
        var scanned = await ScanForDeviceAsync(cancellationToken).ConfigureAwait(false);
        if (scanned.HasValue)
        {
            return await BluetoothLEDevice.FromBluetoothAddressAsync(
                    scanned.Value.Address,
                    scanned.Value.AddressType)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
        }

        // Fall back to already-enumerated devices.
        return await GetEnumeratedDeviceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(ulong Address, BluetoothAddressType AddressType)?> ScanForDeviceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting BLE advertisement scan");

        var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active,
        };

        watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(2000);
        watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(0);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(_options.ScanTimeoutMs));

        var matchTask = EventAsync.WaitForEventAsync<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs>(
            h => watcher.Received += h,
            h => watcher.Received -= h,
            args =>
            {
                var address = FormatMacAddress(args.BluetoothAddress);
                _logger.LogDebug(
                    "BLE advertisement from {Address} (local name: {LocalName}, rssi: {Rssi})",
                    address,
                    args.Advertisement.LocalName,
                    args.RawSignalStrengthInDBm);

                if (!MatchesOptions(args))
                {
                    return false;
                }

                _logger.LogInformation(
                    "Found matching BLE device {Address} ({LocalName}, type: {AddressType})",
                    address,
                    args.Advertisement.LocalName,
                    args.BluetoothAddressType);
                return true;
            },
            cts.Token);

        watcher.Start();

        try
        {
            var args = await matchTask.ConfigureAwait(false);
            return (args.BluetoothAddress, args.BluetoothAddressType);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("BLE advertisement scan timed out");
            return null;
        }
        finally
        {
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

    private async Task<GattDeviceService> GetServiceAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
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

                if (attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        "BLE service discovery failed with {Status}, retrying...",
                        result.Status);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException($"BLE service {_options.ServiceUuid} was not found: {result.Status}.");
                }
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80070016) && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "BLE service discovery returned ERROR_NOT_READY on attempt {Attempt}, retrying...",
                    attempt);
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
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
        _isConnected = false;
        _characteristic = null;
        _session?.Dispose();
        _session = null;
        _device?.Dispose();
        _device = null;
    }
}
