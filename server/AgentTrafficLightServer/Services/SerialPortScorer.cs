namespace AgentTrafficLight.Server.Services;

public static class SerialPortScorer
{
    private static readonly string[] s_positiveKeywords = ["esp32", "usb", "serial", "jtag", "cp210", "ch340", "ftdi"];
    private static readonly string[] s_negativeKeywords = ["bluetooth", "bt", "rfcomm"];

    public static string? BestPort(
        IEnumerable<string> portNames,
        IEnumerable<KeyValuePair<string, string>>? descriptions = null)
    {
        var descriptionDict = descriptions?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? bestPort = null;
        var bestScore = int.MinValue;

        foreach (var port in portNames)
        {
            var description = descriptionDict.TryGetValue(port, out var desc) ? desc : string.Empty;
            var text = $"{port} {description}".ToLowerInvariant();

            if (s_negativeKeywords.Any(k => text.Contains(k, StringComparison.Ordinal)))
            {
                continue;
            }

            var score = s_positiveKeywords.Sum(k => text.Contains(k, StringComparison.Ordinal) ? 1 : 0);
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
