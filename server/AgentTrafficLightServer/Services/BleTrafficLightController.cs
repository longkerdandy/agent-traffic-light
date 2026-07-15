using System.Globalization;
using AgentTrafficLight.Server.Configuration;
using AgentTrafficLight.Server.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
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
        if (_device is { ConnectionStatus: BluetoothConnectionStatus.Connected } && _characteristic != null)
        {
            return;
        }

        _device?.Dispose();
        _device = null;
        _characteristic = null;

        _device = await GetDeviceAsync(cancellationToken).ConfigureAwait(false);

        if (_device == null)
        {
            throw new InvalidOperationException($"BLE device {_options.DeviceAddress} was not found.");
        }

        _logger.LogInformation("BLE device object obtained for {Address}", _options.DeviceAddress);

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var serviceResult = await _device.GetGattServicesForUuidAsync(
                    new Guid(_options.ServiceUuid),
                    BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (serviceResult.Status == GattCommunicationStatus.Success && serviceResult.Services.Count > 0)
            {
                var service = serviceResult.Services[0];
                var characteristicResult = await service.GetCharacteristicsForUuidAsync(
                        new Guid(_options.CharacteristicUuid),
                        BluetoothCacheMode.Uncached)
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);

                if (characteristicResult.Status == GattCommunicationStatus.Success && characteristicResult.Characteristics.Count > 0)
                {
                    _characteristic = characteristicResult.Characteristics[0];
                    _logger.LogInformation("BLE GATT characteristic ready");
                    return;
                }

                _logger.LogWarning(
                    "BLE characteristic discovery attempt {Attempt} failed with status {Status}",
                    attempt,
                    characteristicResult.Status);
            }
            else
            {
                _logger.LogWarning(
                    "BLE service discovery attempt {Attempt} failed with status {Status}",
                    attempt,
                    serviceResult.Status);
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"BLE service {_options.ServiceUuid} was not found.");
    }

    private async Task<BluetoothLEDevice?> GetDeviceAsync(CancellationToken cancellationToken)
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

        if (!string.IsNullOrEmpty(_options.DeviceAddress))
        {
            _logger.LogInformation("Falling back to FromBluetoothAddressAsync for {Address}", _options.DeviceAddress);
            return await BluetoothLEDevice.FromBluetoothAddressAsync(ParseMacAddress(_options.DeviceAddress))
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _device?.Dispose();
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
