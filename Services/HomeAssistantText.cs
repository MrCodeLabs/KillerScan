using System.Globalization;
using System.Text;
using System.Windows;
using KillerScan.Engine;

namespace KillerScan.Services;

internal static class HomeAssistantText
{
    internal static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["CheckChoice"] = "Check: {0}",
        ["ResearchChoice"] = "Start with this integration. The manufacturer or name is a lead; confirm the exact model, firmware and prerequisites below.",
        ["FirmwareChoice"] = "ESP: check firmware",
        ["FirmwareRoute"] = "Identify the existing firmware or app: eWeLink → SonoffLAN, ESPHome → ESPHome, Tasmota → Tasmota. An ESP chip alone does not choose an integration; do not flash it to identify it.",
        ["ResearchNext"] = "Open the linked documentation and verify the prerequisites. If they match, follow its setup instructions; otherwise check the alternatives below.",
        ["CommunitySource"] = "Community integration via HACS; check installation instructions and maintenance",
        ["TuyaAccount"] = "Device must be linked to your Tuya Smart / Smart Life account; check exposed functions",
        ["GoeApi"] = "Compatible go-e model/firmware and enabled local HTTP API v2 or WebSocket",
        ["EwelinkFirmware"] = "Supported device with original eWeLink firmware; check LAN/cloud mode",
        ["Title"] = "Home Assistant integrations",
        ["AllTitle"] = "Best HA integrations for this scan",
        ["BestChoice"] = "Best integration choice",
        ["NoChoice"] = "Needs identification",
        ["Missing"] = "Exact model or supported protocol is missing. Identify the model before selecting an integration.",
        ["Native"] = "The device-specific official integration takes priority over generic protocols.",
        ["ProtocolChoice"] = "Best evidenced protocol in this catalogue; verify the required functions.",
        ["AllSummary"] = "{0} devices · {1} integration choices",
        ["Details"] = "Evidence and alternatives",
        ["Inputs"] = "Refine device information",
        ["Next"] = "In HA, use Add integration and select this integration. Check its model support and prerequisites below.",
        ["NoDevices"] = "This scan has no devices yet. Run a scan first.",
        ["Note"] = "Suggestions are not verified compatibility. No connection or setup is performed. Optional inputs stay in this window; documentation links never include device data.",
        ["Unknown"] = "No specific match in this catalogue. This does not mean the device is unsupported.",
        ["Candidates"] = "Integration candidates", ["Workflow"] = "Find the best integration path",
        ["Identity"] = "Manufacturer / exact model (optional, no credentials)",
        ["Protocol"] = "Known connection / protocol (optional)", ["Goal"] = "Required function",
        ["Monitor"] = "Read state", ["ControlGoal"] = "Control and state", ["MediaGoal"] = "Media",
        ["Observed"] = "Observed hint; verify support", ["Manual"] = "Your input; verify support", ["Research"] = "Research route, not a compatibility match",
        ["Verify"] = "Confirm the exact model, firmware, required entities and prerequisites against the official integration documentation.",
        ["Identify"] = "Read the product label and vendor app/manual: exact model, firmware, connection and desired HA entities. Keep credentials out of this note.",
        ["Discovery"] = "In HA, open Settings → Devices & services → Discovered / Add integration. Check the proposed integration against its documentation.",
        ["Compare"] = "Prefer supported official integrations that expose the functions you need. Compare local/cloud access, availability and maintenance.",
        ["Protocols"] = "No match? Verify documented Matter, HomeKit, MQTT or API support. Open ports, a web login page or a MAC vendor alone do not establish compatibility.",
        ["NonIp"] = "Zigbee, Z-Wave, BLE and Thread devices may be absent from this IPv4 scan. Check a compatible adapter or hub; Thread alone is not Matter.",
        ["Control"] = "Before control, verify commands, state feedback and unavailable-device behavior. Understand the read-only path before testing actuators.",
        ["Test"] = "Test one read-only entity, required functions, availability and restart recovery before extending the setup.",
        ["Community"] = "If official support is missing, review a maintained community integration or documented MQTT/REST/SNMP mapping. Do not flash unidentified hardware.",
        ["Docs"] = "Documentation", ["Copy"] = "Copy integration plan",
        ["Fresh"] = "Captured hints can be cached, old, conflicting or self-reported. Confirm device identity before setup.",
        ["Catalogue"] = "Catalogue reviewed {0}; curated, not exhaustive.", ["Requirements"] = "Prerequisites to verify",
        ["Support"] = "Exact model, firmware and required entities",
        ["Authentication"] = "Supported authentication / account requirements",
        ["HueBridge"] = "Compatible Hue Bridge and pairing",
        ["NativeApi"] = "Existing ESPHome firmware with native API enabled; no firmware is installed",
        ["Mqtt"] = "Configured broker and compatible device topics/discovery; a broker is not the device",
        ["ProfileS"] = "ONVIF Profile S and suitable credentials; RTSP alone is insufficient",
        ["Renderer"] = "DLNA MediaRenderer; UPnP or a media server alone is insufficient",
        ["Pairing"] = "Supported accessory, pairing code and suitable pairing state",
        ["MatterSetup"] = "Confirmed Matter support, IPv6 and commissioning; Thread alone is insufficient",
        ["DocumentedApi"] = "Documented endpoint, payload and authentication; HTTP 200 alone is insufficient",
        ["Oids"] = "Supported OIDs and read access; sysDescr alone does not define HA entities",
        ["Radio"] = "Compatible radio/adapter/hub and exact-device support",
        ["Conflict"] = "Conflicting evidence; no best integration has been selected."
    };
    internal static string L(string key) => Application.Current?.TryFindResource("Str_HA_" + key) as string
        ?? (English.TryGetValue(key, out string? text) ? text : key);
    internal static string Catalogue(HomeAssistantAdvice advice) => string.Format(CultureInfo.CurrentCulture, L("Catalogue"), advice.DocumentationReviewedOn);
    internal static string Choice(HomeAssistantAdvice advice) => HomeAssistantAdvisor.Best(advice) is { } best
        ? HomeAssistantAdvisor.Integration(best.Domain)!.Name : HomeAssistantAdvisor.FirmwareResearch(advice) ? L("FirmwareChoice") :
        HomeAssistantAdvisor.ResearchChoice(advice) is { } research ? string.Format(CultureInfo.CurrentCulture, L("CheckChoice"), HomeAssistantAdvisor.Integration(research.Domain)!.Name) : L("NoChoice");
    internal static string Next(HomeAssistantAdvice advice) => HomeAssistantAdvisor.FirmwareResearch(advice) ? L("FirmwareRoute") :
        (HomeAssistantAdvisor.Best(advice) ?? HomeAssistantAdvisor.ResearchChoice(advice)) is { } selected &&
        (selected.Basis == "Research" || selected.Requirements.Contains("CommunitySource")) ? L("ResearchNext") : L("Next");
    internal static string Reason(HomeAssistantAdvice advice) => L(HomeAssistantAdvisor.ChoiceReason(advice));
    internal static string Time(DateTimeOffset? time) => time?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? AccessText.L("NoTime");
    internal static string Describe(NetworkDevice device, HomeAssistantAdvice advice, HomeAssistantContext? context = null)
    {
        var text = new StringBuilder().AppendLine(L("Title")).AppendLine(device.IpAddress + "  " + device.Hostname)
            .AppendLine(L("BestChoice") + ": " + Choice(advice)).AppendLine(Reason(advice))
            .AppendLine(L("Note")).AppendLine(Catalogue(advice)).AppendLine(L("Fresh"));
        if (context != null && context.ManufacturerModel.Length > 0) text.AppendLine(L("Manual") + ": " + context.ManufacturerModel);
        if (context != null) text.AppendLine(L("Goal") + ": " + L(context.Goal == "Control" ? "ControlGoal" : context.Goal == "Media" ? "MediaGoal" : "Monitor"));
        if (advice.Status == "Conflict") text.AppendLine(L("Conflict"));
        if (advice.Candidates.Length == 0) text.AppendLine(L("Unknown"));
        foreach (var candidate in advice.Candidates)
        {
            var entry = HomeAssistantAdvisor.Integration(candidate.Domain);
            if (entry == null) continue;
            text.AppendLine().AppendLine(entry.Name + " — " + L(candidate.Basis)).AppendLine(entry.DocumentationUrl);
            foreach (var fact in candidate.Evidence)
                text.AppendLine(fact.Source + " · " + Time(fact.ObservedAtUtc) + (fact.Reused ? " [" + IdentificationText.L("Cached") + "]" : "") + ": " + fact.Value);
            text.AppendLine(L("Requirements") + ": " + string.Join("; ", candidate.Requirements.Select(L)));
        }
        if (HomeAssistantAdvisor.Best(advice) != null || HomeAssistantAdvisor.ResearchChoice(advice) != null || HomeAssistantAdvisor.FirmwareResearch(advice))
        {
            text.AppendLine().AppendLine(Next(advice));
            if (advice.Steps.Contains("Control")) text.AppendLine(L("Control"));
        }
        else
        {
            text.AppendLine().AppendLine(L("Workflow"));
            for (int index = 0; index < advice.Steps.Length; index++) text.AppendLine((index + 1) + ". " + L(advice.Steps[index]));
        }
        return text.AppendLine(HomeAssistantAdvisor.CatalogueUrl).ToString();
    }
    internal static NetworkDevice FromHistory(HistoricalDevice device) => new()
    { IpAddress = device.IpAddress, Hostname = device.Hostname, Vendor = device.Vendor, DeviceType = device.DeviceType,
        OpenPorts = [.. device.OpenPorts], Identification = device.Identification, HomeAssistant = device.HomeAssistant };
    internal static NetworkDevice Freeze(NetworkDevice device) => new()
    { IpAddress = device.IpAddress, Hostname = device.Hostname, Vendor = device.Vendor, DeviceType = device.DeviceType, OpenPorts = [.. device.OpenPorts], Identification = device.Identification?.Snapshot(),
        HomeAssistant = device.HomeAssistant?.Snapshot(), HttpTitle = device.HttpTitle, HttpServer = device.HttpServer,
        SnmpDescr = device.SnmpDescr, SsdpServer = device.SsdpServer, MdnsServices = [.. device.MdnsServices] };
}
