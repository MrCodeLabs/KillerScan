using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace KillerScan.Services;

internal static class ForkCompatibility
{
    // Read-only: upgrading must not replace an existing catalog or infer a site from addresses.
    internal static string? SelectedLocation(string root)
    {
        string path = Path.Combine(root, "locations.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("SelectedId", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    internal static string AdapterName(string id)
    {
        // Local interface metadata only. A missing adapter stays explicitly unresolved; it
        // must not silently become Automatic or send a scan through another connection.
        try { return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))?.Name ?? id; }
        catch { return id; }
    }
}
