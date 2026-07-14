namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Scores available serial ports to identify the one most likely connected to the ESP32 hardware.
/// </summary>
public static class SerialPortScorer
{
    private static readonly (string Keyword, int Score)[] s_positiveKeywords =
    [
        ("esp32", 100),
        ("usb", 20),
        ("jtag", 10),
        ("serial", 10),
        ("cp210", 5),
        ("ch340", 5),
        ("ftdi", 5)
    ];

    private static readonly string[] s_negativeKeywords = ["bluetooth", "bt", "rfcomm"];

    /// <summary>
    /// Selects the serial port with the highest score based on keywords in the port name and descriptions.
    /// Ports matching negative keywords (e.g. Bluetooth) are excluded.
    /// </summary>
    /// <param name="portNames">The available serial port names.</param>
    /// <param name="descriptions">Optional port descriptions keyed by port name.</param>
    /// <returns>The best matching port name, or <c>null</c> if no suitable port is found.</returns>
    public static string? BestPort(
        IEnumerable<string> portNames,
        IEnumerable<KeyValuePair<string, string>>? descriptions = null)
    {
        var descriptionDict = descriptions?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? bestPort = null;
        var bestScore = 0;

        foreach (var port in portNames)
        {
            var description = descriptionDict.TryGetValue(port, out var desc) ? desc : string.Empty;
            var text = $"{port} {description}".ToLowerInvariant();

            if (s_negativeKeywords.Any(k => text.Contains(k, StringComparison.Ordinal)))
            {
                continue;
            }

            var score = s_positiveKeywords.Sum(k => text.Contains(k.Keyword, StringComparison.Ordinal) ? k.Score : 0);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestPort = port;
        }

        return bestPort;
    }
}
