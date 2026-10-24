using System.Text.RegularExpressions;

namespace KillerScan.Engine;

/// <summary>A curated, offline catalogue. All matching is bounded and performs no I/O.</summary>
public static class HomeAssistantAdvisor
{
    public const string CatalogueRevision = "2026-10-23.3";
    public const string CatalogueUrl = "https://www.home-assistant.io/integrations/";
    private static readonly HomeAssistantIntegration[] Catalogue =
    [
        Entry("shelly", "Shelly", "Support", "Authentication"),
        Entry("hue", "Philips Hue", "Support", "HueBridge"),
        Entry("esphome", "ESPHome", "Support", "NativeApi"),
        Entry("tasmota", "Tasmota", "Support", "Mqtt"),
        Entry("tplink", "TP-Link Smart Home (Kasa/Tapo)", "Support", "Authentication"),
        Entry("onvif", "ONVIF", "Support", "ProfileS"),
        Entry("dlna_dmr", "DLNA Digital Media Renderer", "Support", "Renderer"),
        Entry("homekit_controller", "HomeKit Device", "Support", "Pairing"),
        Entry("matter", "Matter", "Support", "MatterSetup"),
        Entry("mqtt", "MQTT", "Mqtt"),
        Entry("sonos", "Sonos", "Support"),
        Entry("cast", "Google Cast", "Support"),
        Entry("roku", "Roku", "Support"),
        Entry("samsungtv", "Samsung Smart TV", "Support", "Authentication"),
        Entry("webostv", "LG webOS TV", "Support", "Authentication"),
        Entry("synology_dsm", "Synology DSM", "Support", "Authentication"),
        Entry("denonavr", "Denon AVR Network Receivers", "Support", "Authentication"),
        Entry("reolink", "Reolink", "Support", "Authentication"),
        Entry("tuya", "Tuya", "Support", "TuyaAccount"),
        Entry("proxmoxve", "Proxmox VE", "Support", "Authentication"),
        new("goecharger_api2", "go-eCharger API v2 (HACS)", "https://github.com/marq24/ha-goecharger-api2", ["Support", "CommunitySource", "GoeApi"]),
        new("sonoff", "SonoffLAN (HACS)", "https://github.com/AlexxIT/SonoffLAN", ["Support", "CommunitySource", "EwelinkFirmware"]),
        Entry("ipp", "Internet Printing Protocol (IPP)", "Support"),
        Entry("rest", "RESTful", "DocumentedApi"),
        Entry("snmp", "SNMP", "Oids"),
        Entry("zha", "Zigbee Home Automation", "Radio"),
        Entry("zwave_js", "Z-Wave", "Radio"),
        Entry("bluetooth", "Bluetooth", "Radio")
    ];
    private static HomeAssistantIntegration Entry(string domain, string name, params string[] requirements) =>
        new(domain, name, CatalogueUrl + domain + "/", requirements);

    public static IReadOnlyList<HomeAssistantIntegration> Integrations => Catalogue.Select(item => item with { Requirements = [.. item.Requirements] }).ToArray();
    public static HomeAssistantIntegration? Integration(string domain) =>
        Catalogue.FirstOrDefault(item => item.Domain == domain) is { } item ? item with { Requirements = [.. item.Requirements] } : null;

    public static HomeAssistantAdvice ForDevice(NetworkDevice device) => device.HomeAssistant?.Snapshot() ?? Assess(device);

    /// <summary>Deterministic choice: native platform before generic protocols, then actual
    /// protocol support before a research lead. Conflicts and unsupported guesses never win.</summary>
    public static HomeAssistantCandidate? Best(HomeAssistantAdvice advice)
    {
        if (advice.Status == "Conflict") return null;
        return advice.Candidates.Where(item => (item.Basis == "Observed" || item.Basis == "Manual" && Priority(item.Domain) >= 30) && Integration(item.Domain) != null)
            .OrderBy(item => item.Basis == "Observed" ? 0 : 1)
            .ThenByDescending(item => Priority(item.Domain)).ThenBy(item => item.Domain, StringComparer.Ordinal).FirstOrDefault();
    }
    private static int Priority(string domain) => domain switch
    {
        "cast" => 75, "ipp" => 60,
        "homekit_controller" => 70, "matter" => 65, "onvif" => 60, "dlna_dmr" => 50, "mqtt" => 30,
        "rest" or "snmp" or "zha" or "zwave_js" or "bluetooth" => 10, _ => 100
    };
    // A research suggestion is deliberately separate from Best/RecommendedDomain:
    // manufacturer/name evidence may guide investigation, never certify support.
    public static bool FirmwareResearch(HomeAssistantAdvice advice) => advice.Status != "Conflict"
        && Best(advice) == null && advice.Candidates.Any(item => item.Basis == "Research" && item.Domain == "esphome")
        && advice.Candidates.Any(item => item.Basis == "Research" && item.Domain == "tasmota")
        && !advice.Candidates.Any(item => item.Basis == "Research" && item.Domain is not ("esphome" or "tasmota" or "sonoff"));
    public static HomeAssistantCandidate? ResearchChoice(HomeAssistantAdvice advice) =>
        advice.Status == "Conflict" || Best(advice) != null || FirmwareResearch(advice) ? null :
        advice.Candidates.Where(item => item.Basis == "Research" && Integration(item.Domain) != null)
            .OrderByDescending(item => Priority(item.Domain)).ThenBy(item => item.Domain, StringComparer.Ordinal).FirstOrDefault();
    public static string ChoiceReason(HomeAssistantAdvice advice) => advice.Status == "Conflict" ? "Conflict" : Best(advice) is not { } best ?
        FirmwareResearch(advice) ? "FirmwareRoute" : ResearchChoice(advice) != null ? "ResearchChoice" : "Missing" :
        best.Basis == "Manual" ? "Manual" : best.Requirements.Contains("CommunitySource") ? "CommunitySource" : Priority(best.Domain) == 100 ? "Native" : "ProtocolChoice";

    public static HomeAssistantAdvice Assess(NetworkDevice device, HomeAssistantContext? context = null)
    {
        var facts = Facts(device).ToArray();
        var candidates = new List<HomeAssistantCandidate>();
        foreach (var entry in Catalogue)
        {
            var matched = facts.Where(fact => Matches(entry.Domain, fact.Source, fact.Value)).ToArray();
            if (matched.Length > 0) candidates.Add(new HomeAssistantCandidate
            { Domain = entry.Domain, Evidence = matched, Requirements = [.. entry.Requirements] });
        }
        // sysDescr supports investigating SNMP, not claiming ready-made HA sensors or controls.
        if (facts.Any(fact => fact.Source == "SNMP")) AddLead("snmp", "Research", facts.Where(fact => fact.Source == "SNMP").ToArray());
        // The frozen OUI/name facts are leads only, including for legacy input.
        Lead("tuya", @"\btuya\b");
        Lead("reolink", @"\breolink\b");
        Lead("denonavr", @"\bdenon(?:[ _-]+(?:avr|avc)[ _-]?[a-z0-9]+)?\b");
        Lead("goecharger_api2", @"(?:^|[^a-z0-9])go[ _-]?e(?:charger)?(?=$|[^a-z0-9])");
        Lead("sonoff", @"\b(?:sonoff|ewelink)\b");
        // A virtual NIC OUI also occurs on guests: it must not select Proxmox VE.
        Lead("proxmoxve", @"\b(?:proxmox|pve)(?:[ _-]|$)", nameOnly: true);
        Lead("samsungtv", @"\bsamsung[ _-]?(?:smart[ _-]?)?tv\b", nameOnly: true);
        var module = facts.Where(fact => (fact.Source is "Name" or "OUI") &&
            Has(fact.Value, @"\bespressif\b|(?:^|[= ])ESP[_-][a-f0-9]{4,12}(?:\.|$)")).ToArray();
        if (module.Length > 0 && !candidates.Any(item => Priority(item.Domain) >= 30))
        {
            AddLead("esphome", "Research", module);
            AddLead("tasmota", "Research", module);
            AddLead("sonoff", "Research", module);
        }
        if (context != null)
        {
            string lead = Bound(context.ManufacturerModel, 160);
            foreach (string domain in Catalogue.Select(item => item.Domain))
                if (Matches(domain, "HTTP", lead)) AddLead(domain, "Manual", []);
            if (Integration(context.Protocol) != null) AddLead(context.Protocol, "Manual", []);
        }
        bool conflicting = device.Identification is { ConflictingSignals: true } or { ModelConflict: true }
            || candidates.Count(item => item.Basis == "Observed" && item.Domain is "shelly" or "hue" or "esphome" or "tasmota" or "tplink" or "sonos" or "roku" or "samsungtv" or "webostv" or "synology_dsm" or "denonavr" or "reolink" or "tuya" or "proxmoxve" or "goecharger_api2" or "sonoff") > 1;
        var steps = new List<string>();
        bool specific = candidates.Any(item => item.Basis == "Observed");
        steps.Add(specific ? "Verify" : "Identify");
        steps.AddRange(["Discovery", "Compare", "Protocols"]);
        if (context?.Protocol is "zha" or "zwave_js" or "bluetooth" or "matter" || !specific) steps.Add("NonIp");
        if (context?.Goal == "Control") steps.Add("Control");
        steps.AddRange(["Test", "Community"]);
        var advice = new HomeAssistantAdvice
        {
            Status = conflicting ? "Conflict" : specific ? "Candidate" : candidates.Count > 0 ? "Research" : "Unknown",
            Candidates = candidates.OrderBy(item => item.Basis == "Observed" ? 0 : item.Basis == "Manual" ? 1 : 2)
                .ThenBy(item => item.Domain, StringComparer.Ordinal).ToArray(), Steps = steps.ToArray()
        };
        var best = Best(advice);
        return advice with { RecommendedDomain = best?.Domain,
            RecommendationReason = ChoiceReason(advice) };

        void Lead(string domain, string pattern, bool nameOnly = false)
        {
            var hints = facts.Where(fact => (fact.Source == "Name" || !nameOnly && fact.Source == "OUI") && Has(fact.Value, pattern)).ToArray();
            if (hints.Length > 0) AddLead(domain, "Research", hints);
        }
        void AddLead(string domain, string basis, RecognitionEvidence[] evidence)
        {
            int existing = candidates.FindIndex(item => item.Domain == domain);
            if (existing >= 0)
            {
                // Explicit user input can refine a weak lead, but never displace a captured platform.
                if (basis == "Manual" && candidates[existing].Basis == "Research")
                    candidates[existing] = candidates[existing] with { Basis = "Manual", Evidence = evidence };
                return;
            }
            var entry = Integration(domain)!;
            candidates.Add(new HomeAssistantCandidate { Domain = domain, Basis = basis, Evidence = evidence, Requirements = [.. entry.Requirements] });
        }
    }

    private static IEnumerable<RecognitionEvidence> Facts(NetworkDevice device)
    {
        if (device.Identification != null)
        {
            foreach (var fact in (device.Identification.Evidence ?? []).Take(64))
                if (fact != null && fact.Source is "HTTP" or "mDNS" or "SSDP" or "SNMP" or "Name" or "OUI")
                    yield return fact with { Value = Bound(fact.Value, 512), Origin = Bound(fact.Origin, 64) };
            yield break;
        }
        // Externally supplied/legacy fingerprints have no invented capture time. Names, manual
        // types and MAC vendors never become observed protocol support.
        foreach (var (source, value) in new[] { ("Name", device.Hostname), ("OUI", device.Vendor), ("HTTP", device.HttpTitle), ("HTTP", device.HttpServer), ("SSDP", device.SsdpServer), ("SNMP", device.SnmpDescr) })
            if (!string.IsNullOrWhiteSpace(value)) yield return new() { Source = source, Value = Bound(value, 512), Origin = "external" };
        foreach (string value in (device.MdnsServices ?? []).Take(16))
            if (!string.IsNullOrWhiteSpace(value)) yield return new() { Source = "mDNS", Value = Bound(value, 512), Origin = "external" };
    }

    private static string Bound(string? value, int maximum) => new((value ?? "").Take(maximum).Select(c => char.IsControl(c) ? ' ' : c).ToArray());
    private static bool Has(string value, string pattern) => Regex.IsMatch(value, pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static bool Service(string value, string type) => Has(value, @"(?:^|\.)" + Regex.Escape(type) + @"(?:\.local\.?)?$");
    private static bool Matches(string domain, string source, string value)
    {
        if (source == "mDNS") return domain switch
        {
            "esphome" => Service(value, "_esphomelib._tcp"),
            "homekit_controller" => Service(value, "_hap._tcp"),
            "matter" => Service(value, "_matterc._udp"),
            "mqtt" => Service(value, "_mqtt._tcp") || Service(value, "_secure-mqtt._tcp"),
            "hue" => Service(value, "_hue._tcp"),
            "sonos" => Service(value, "_sonos._tcp"),
            "cast" => Service(value, "_googlecast._tcp"),
            "ipp" => Service(value, "_ipp._tcp") || Service(value, "_ipps._tcp"),
            _ => false
        };
        if (source is not ("HTTP" or "SSDP" or "SNMP")) return false;
        return domain switch
        {
            "denonavr" => Has(value, @"\bdenon[ _-]+(?:avr|avc)[ _-]?[a-z0-9]+\b"),
            "reolink" => Has(value, @"\breolink\b"),
            "tuya" => Has(value, @"\b(?:tuya|smart[ _-]?life)\b"),
            "proxmoxve" => Has(value, @"\bproxmox(?:[ _-]+ve)?\b"),
            "goecharger_api2" => Has(value, @"(?:^|[^a-z0-9])go[ _-]?e(?:charger)?(?=$|[^a-z0-9])"),
            "sonoff" => Has(value, @"\bewelink\b"),
            "shelly" => Has(value, @"\bshelly(?:plus|pro|gen[1-4])?(?:[1-4](?:pm)?)?\b"),
            "hue" => Has(value, @"\b(?:philips\s+hue|hue\s+bridge)\b"),
            "esphome" => Has(value, @"\besphome\b"),
            "tasmota" => Has(value, @"\btasmota\b"),
            "tplink" => Has(value, @"\b(?:tapo|kasa)\b"),
            "onvif" => Has(value, @"\bonvif\b"),
            "dlna_dmr" => source == "SSDP" && Has(value, @"\bMediaRenderer\b"),
            "mqtt" => Has(value, @"\bmosquitto\b"),
            "sonos" => Has(value, @"\bsonos\b"),
            "cast" => Has(value, @"\b(?:chromecast|google\s+cast)\b"),
            "roku" => Has(value, @"\broku\b"),
            "samsungtv" => Has(value, @"\b(?:samsung(?:\s+smart)?\s*tv|samsung_dtv)\b"),
            "webostv" => Has(value, @"\b(?:webos|webostv)\b"),
            "synology_dsm" => Has(value, @"\b(?:synology(?:\s+diskstation)?\s+dsm|diskstation\s+manager)\b"),
            _ => false
        };
    }
}
