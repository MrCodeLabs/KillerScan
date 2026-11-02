using System.Text.Json;
using System.Text.RegularExpressions;

namespace KillerScan.Engine;

/// <summary>Compatibility with the fork's saved evidence and HA contracts; no network work.</summary>
internal static class ForkRecognition
{
    internal static T? Clone<T>(T? value) => value == null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
    internal static void Capture(NetworkDevice device)
    {
        var facts = new List<RecognitionEvidence>();
        void Fact(string source, string? value, bool reused = false, DateTimeOffset? time = null)
        {
            if (!string.IsNullOrWhiteSpace(value)) facts.Add(new() { Source = source,
                Value = new string(value.Take(512).Select(c => char.IsControl(c) ? ' ' : c).ToArray()),
                Origin = device.ObservationCompletedAtUtc.HasValue ? "scanner" : "external", ObservedAtUtc = source == "OUI" ? null : reused ? time : time ?? device.ObservationCompletedAtUtc, Reused = reused });
        }
        Fact("Name", device.Hostname); Fact("OUI", device.Vendor, true);
        Fact("TCP", string.Join(", ", device.OpenPorts)); Fact("HTTP", device.HttpTitle); Fact("HTTP", device.HttpServer);
        Fact("TLS", device.TlsSubject); Fact("SSH", device.SshBanner); Fact("SNMP", device.SnmpDescr); Fact("NetBIOS", device.NetbiosName);
        foreach (string service in device.MdnsServices) Fact("mDNS", service, device.RecognitionScope == "deep", device.MdnsObservedAtUtc);
        Fact("SSDP", device.SsdpServer, device.RecognitionScope == "deep", device.SsdpObservedAtUtc);
        var recognition = device.Recognition;
        var models = facts.Where(fact => fact.Source is "SNMP" or "HTTP" or "SSDP")
            .SelectMany(fact => Regex.Matches(fact.Value, @"\bmodel(?:name)?\s*[:=]\s*([\p{L}\p{N}][^|;,]{0,63})(?=[|;,]|$)", RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100)).Cast<Match>().Select(match => match.Groups[1].Value.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        device.Identification = new DeviceIdentification
        {
            AutomaticType = recognition?.AutomaticType ?? device.DeviceType,
            Confidence = recognition?.AutomaticConfidence.ToString() ?? "None", ManualType = recognition?.Confidence == RecognitionConfidence.Manual ? recognition.Type : null,
            ConflictingSignals = recognition?.Conflict ?? false, ModelConflict = models.Length > 1, ReportedModel = models.Length == 1 ? models[0] : null,
            AssessedAtUtc = DateTimeOffset.UtcNow, ObservationStartedAtUtc = device.ObservationStartedAtUtc,
            ObservationCompletedAtUtc = device.ObservationCompletedAtUtc, Scope = device.RecognitionScope,
            DisabledFeatures = [.. device.RecognitionDisabledFeatures], Evidence = facts.ToArray(),
            Candidates = recognition?.Alternatives.Select(item => item with { Sources = [.. item.Sources] }).ToArray() ?? []
        };
        device.HomeAssistant = HomeAssistantAdvisor.Assess(device);
    }
}
