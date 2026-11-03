using System.IO;
using System.Text.Json;
using KillerScan.Engine;

namespace KillerScan.Services;

// Deliberately outside Engine and the scan/export contracts. User leads are never scan facts.
internal sealed record SurveyEntry
{
    [System.Text.Json.Serialization.JsonRequired]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public bool Manual { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public string Name { get; init; } = "";
    public string Room { get; init; } = "";
    public string ManufacturerModel { get; init; } = "";
    public string Firmware { get; init; } = "";
    public string Connection { get; init; } = "";
    public string DesiredFunctions { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string Goal { get; init; } = "Monitor";
    public string LinkedMac { get; init; } = "";
    public SurveyObservation? Observation { get; init; }
    internal HomeAssistantContext Context => new() { ManufacturerModel = ManufacturerModel, Protocol = Protocol, Goal = Goal };
}

internal sealed record SurveyObservation
{
    [System.Text.Json.Serialization.JsonRequired]
    public NetworkDevice Device { get; init; } = new();
    public DateTimeOffset? LastSeenUtc { get; init; }
    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed class SurveyStore
{
    private sealed class Document
    {
        [System.Text.Json.Serialization.JsonRequired]
        public int Version { get; set; }
        [System.Text.Json.Serialization.JsonRequired]
        public string LocationId { get; set; } = "";
        [System.Text.Json.Serialization.JsonRequired]
        public List<SurveyEntry> Entries { get; set; } = [];
    }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path, _location;
    private readonly bool _memoryOnly;
    private Document _memory;
    internal SurveyStore(string path, string location, bool memoryOnly = false)
    { _path = path; _location = location; _memoryOnly = memoryOnly; _memory = new() { Version = 1, LocationId = location }; }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    internal static string Mac(string? value)
    {
        string normalized = (value ?? "").Replace(":", "").Replace("-", "").Trim().ToUpperInvariant();
        return normalized.Length == 12 && normalized.All(Uri.IsHexDigit) && normalized != "000000000000" && normalized != "FFFFFFFFFFFF" ? normalized : "";
    }
    internal static string DeviceName(NetworkDevice device)
    {
        string name = new[] { device.Hostname, device.IpAddress, device.MacAddress }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "?";
        return name.Length <= 160 ? name : name.Substring(0, char.IsHighSurrogate(name[159]) ? 159 : 160);
    }
    private Document Read()
    {
        if (_memoryOnly) return Clone(_memory);
        if (!File.Exists(_path)) return new() { Version = 1, LocationId = _location };
        try
        {
            var data = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path), Json);
            if (data == null || data.Version != 1 || data.LocationId != _location || data.Entries == null ||
                data.Entries.Any(entry => entry == null || !Guid.TryParseExact(entry.Id, "N", out _) || !Valid(entry) ||
                    (entry.LinkedMac != "" && Mac(entry.LinkedMac) != entry.LinkedMac) ||
                    (entry.Observation != null && (!Usable(entry.Observation.Device) ||
                        (entry.Observation.Device.LocationId != null && entry.Observation.Device.LocationId != _location) ||
                        (entry.LinkedMac != "" && Mac(entry.Observation.Device.MacAddress) != entry.LinkedMac)))) ||
                data.Entries.Select(entry => entry.Id).Distinct().Count() != data.Entries.Count ||
                data.Entries.Where(entry => entry.LinkedMac != "").GroupBy(entry => entry.LinkedMac).Any(group => group.Count() > 1))
                throw new InvalidDataException("Invalid survey schema or location.");
            return data;
        }
        catch (Exception error) when (error is JsonException || error is ArgumentException || error is NullReferenceException)
        { throw new InvalidDataException("Invalid survey; the original file was preserved.", error); }
    }
    private static bool Usable(NetworkDevice? device) => device != null &&
        new[] { device.IpAddress, device.MacAddress, device.Hostname, device.Vendor, device.DeviceType, device.HttpTitle, device.HttpServer, device.SnmpDescr, device.SsdpServer }.All(value => value != null) &&
        device.OpenPorts != null && device.MdnsServices != null && device.MdnsServices.All(value => value != null) &&
        (device.Identification is not { } identification || identification.Evidence != null && identification.Evidence.All(fact => fact != null && fact.Source != null && fact.Value != null)) &&
        (device.HomeAssistant is not { } advice || advice.Candidates != null && advice.Steps != null && advice.Candidates.All(candidate => candidate != null && candidate.Evidence != null && candidate.Requirements != null && candidate.Evidence.All(fact => fact != null && fact.Source != null && fact.Value != null)));
    private static bool Valid(SurveyEntry entry) => !string.IsNullOrWhiteSpace(entry.Name) && entry.Name.Length <= 160 &&
        new[] { entry.Room, entry.ManufacturerModel, entry.Firmware, entry.Connection, entry.DesiredFunctions, entry.Notes, entry.Protocol, entry.Goal, entry.LinkedMac }
            .All(value => value != null && value.Length <= 8000) && entry.Goal is "Monitor" or "Control" or "Media";
    internal IReadOnlyList<SurveyEntry> Load() => Clone(Read().Entries);
    private T Change<T>(Func<Document, T> change)
    {
        if (!_memoryOnly) Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using var lease = _memoryOnly ? null : new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var document = Read();
        T result = change(document);
        if (_memoryOnly) _memory = Clone(document);
        else DevelopmentData.WriteAtomic(_path, JsonSerializer.Serialize(document, Json));
        return result;
    }
    internal void Save(SurveyEntry entry) => Change(document =>
    {
        if (!Valid(entry) || !Guid.TryParseExact(entry.Id, "N", out _)) throw new ArgumentException("A device name is required.");
        int index = document.Entries.FindIndex(item => item.Id == entry.Id);
        if (index < 0) throw new InvalidOperationException("The survey entry no longer exists. Reload the survey.");
        // Editing may change only manual fields, never the network binding or observation.
        var previous = document.Entries[index];
        document.Entries[index] = Clone(entry with { Manual = previous.Manual, LinkedMac = previous.LinkedMac, Observation = previous.Observation });
        return true;
    });
    internal void AddManual(SurveyEntry entry) => Change(document =>
    {
        if (!Valid(entry) || !Guid.TryParseExact(entry.Id, "N", out _) || document.Entries.Any(item => item.Id == entry.Id)) throw new ArgumentException("A unique device name/ID is required.");
        document.Entries.Add(Clone(entry with { Manual = true, LinkedMac = "", Observation = null })); return true;
    });
    internal void Remove(string id) => Change(document => document.Entries.RemoveAll(item => item.Id == id));
    private SurveyObservation Capture(NetworkDevice device)
    {
        if (!Usable(device)) throw new InvalidDataException("Invalid device observation.");
        if (device.LocationId != null && device.LocationId != _location) throw new InvalidDataException("The device belongs to a different location.");
        var frozen = Clone(device);
        frozen.LocationId = _location;
        return new() { Device = frozen, LastSeenUtc = device.Identification?.ObservationCompletedAtUtc };
    }
    // Confirmation is supplied by the UI. Cancellation returns before any disk mutation.
    internal SurveyEntry? AddObserved(NetworkDevice device, Func<IReadOnlyList<SurveyEntry>, string?> chooseIpMatch, SurveyEntry? edited = null)
    {
        var snapshot = Capture(device); string mac = Mac(device.MacAddress);
        var entries = Load();
        var matched = mac == "" ? null : entries.SingleOrDefault(item => !item.Manual && item.LinkedMac == mac);
        if (matched == null)
        {
            var candidates = entries.Where(item => !item.Manual && item.Observation?.Device.IpAddress == device.IpAddress &&
                device.IpAddress.Length > 0 && (item.LinkedMac == "" || mac == "")).ToArray();
            if (candidates.Length > 0)
            {
                string? choice = chooseIpMatch(candidates);
                if (choice == null) return null;
                matched = choice == "" ? null : candidates.SingleOrDefault(item => item.Id == choice)
                    ?? throw new ArgumentException("Select an existing survey entry explicitly.");
            }
        }
        string? expected = matched?.Id;
        return Change(document =>
        {
            var current = expected == null ? null : document.Entries.SingleOrDefault(item => item.Id == expected);
            if (expected != null && current == null) throw new InvalidOperationException("The survey changed. Reload before linking.");
            if (mac != "" && document.Entries.Any(item => item.LinkedMac == mac && item.Id != expected))
                throw new InvalidOperationException("This MAC is already linked. Reload the survey.");
            var updated = current ?? new SurveyEntry { Name = DeviceName(device) };
            if (edited != null)
            {
                if (!Valid(edited)) throw new ArgumentException("A device name is required.");
                if (current != null && edited.Id != current.Id)
                    throw new InvalidOperationException("Reload the linked entry before editing its manual fields.");
                updated = edited with { Id = updated.Id, Manual = false };
            }
            var keep = current?.Observation;
            if (keep == null || Mac(keep.Device.MacAddress) != mac || (snapshot.LastSeenUtc != null && (keep.LastSeenUtc == null || snapshot.LastSeenUtc >= keep.LastSeenUtc))) keep = snapshot;
            updated = updated with { LinkedMac = mac, Observation = keep };
            if (current == null) document.Entries.Add(updated); else document.Entries[document.Entries.IndexOf(current)] = updated;
            return Clone(updated);
        });
    }
    internal void Link(string id, NetworkDevice device) => Change(document =>
    {
        int index = document.Entries.FindIndex(entry => entry.Id == id);
        if (index < 0) throw new InvalidOperationException("The survey entry no longer exists.");
        string mac = Mac(device.MacAddress);
        if (mac != "" && document.Entries.Any(entry => entry.Id != id && entry.LinkedMac == mac))
            throw new InvalidOperationException("This MAC is already linked. Reload the survey.");
        document.Entries[index] = document.Entries[index] with { LinkedMac = mac, Observation = Capture(device) };
        return true;
    });
    internal void Refresh(IEnumerable<NetworkDevice> devices)
    {
        var found = devices.ToArray();
        if (found.Length == 0) return;
        var entries = Load();
        if (!entries.Any(entry => entry.LinkedMac != "" && found.Any(device => Mac(device.MacAddress) == entry.LinkedMac))) return;
        Change(document =>
        {
            for (int index = 0; index < document.Entries.Count; index++)
            {
                var entry = document.Entries[index];
                var matches = found.Where(device => entry.LinkedMac != "" && Mac(device.MacAddress) == entry.LinkedMac).ToArray();
                if (matches.Length != 1) continue; // Ambiguous duplicate MAC observations need a human.
                var observation = Capture(matches[0]);
                if (observation.LastSeenUtc == null || (entry.Observation?.LastSeenUtc is { } previous && observation.LastSeenUtc < previous)) continue;
                document.Entries[index] = entry with { Observation = observation };
            }
            return true;
        });
    }
    internal static NetworkDevice AdviceDevice(SurveyEntry entry)
    {
        var device = entry.Observation == null ? new NetworkDevice() : Clone(entry.Observation.Device);
        if (entry.Observation != null && device.Identification == null)
        {
            // Legacy network fields have no trustworthy capture time. Retain only the existing
            // advisor's network facts, explicitly cached, without promoting any manual fields.
            device.Identification = new DeviceIdentification { Scope = "survey", AssessedAtUtc = entry.Observation.SavedAtUtc,
                Evidence = HomeAssistantAdvisor.Assess(device).Candidates.SelectMany(candidate => candidate.Evidence)
                    .Select(fact => fact with { Reused = true }).Distinct().ToArray() };
        }
        if (device.Identification is { } identification)
            device.Identification = identification with { Evidence = identification.Evidence.Select(fact => fact with { Reused = true }).ToArray() };
        if (device.HomeAssistant is { } advice)
            device.HomeAssistant = advice with { Candidates = advice.Candidates.Select(candidate => candidate with
                { Evidence = candidate.Evidence.Select(fact => fact with { Reused = true }).ToArray() }).ToArray() };
        return device;
    }
}
