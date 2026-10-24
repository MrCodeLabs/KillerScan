using System.Text.Json;
using KillerScan.Engine;

internal static class HomeAssistantTests
{
    internal static Task DirectChoices()
    {
        foreach (var (expected, device) in new (string, NetworkDevice)[]
        {
            ("shelly", new() { HttpTitle = "ShellyPlus1PM", MdnsServices = { "fixture._hap._tcp.local" } }),
            ("tasmota", new() { HttpTitle = "Tasmota", MdnsServices = { "broker._mqtt._tcp.local" } }),
            ("sonos", new() { MdnsServices = { "room._sonos._tcp.local" }, SsdpServer = "MediaRenderer" }),
            ("cast", new() { MdnsServices = { "screen._googlecast._tcp.local" } }),
            ("roku", new() { SsdpServer = "Roku / MediaRenderer" }),
            ("samsungtv", new() { SsdpServer = "Samsung_DTV MediaRenderer" }),
            ("webostv", new() { SsdpServer = "WebOS MediaRenderer" }),
            ("synology_dsm", new() { HttpTitle = "Synology DSM" }),
            ("ipp", new() { MdnsServices = { "printer._ipps._tcp.local." } })
        })
        {
            var advice = HomeAssistantAdvisor.Assess(device);
            Require(advice.RecommendedDomain == expected && HomeAssistantAdvisor.Best(advice)?.Domain == expected, "Direct choice for " + expected);
            Require(advice.Version == 2 && advice.RecommendationReason != "Missing", "Versioned choice contains a reason.");
        }
        var conflict = HomeAssistantAdvisor.Assess(new NetworkDevice { HttpTitle = "Samsung TV / WebOS" });
        Require(conflict.Status == "Conflict" && conflict.RecommendedDomain == null && HomeAssistantAdvisor.Best(conflict) == null, "Conflicting native families cannot be resolved by alphabetic order.");
        Require(HomeAssistantAdvisor.Assess(new NetworkDevice { Vendor = "Samsung", DeviceType = "Smart TV" }).RecommendedDomain == null, "A vendor or manually assigned category does not select a model integration.");
        var manual = HomeAssistantAdvisor.Assess(new NetworkDevice(), new HomeAssistantContext { ManufacturerModel = "Tasmota" });
        Require(manual.RecommendedDomain == "tasmota" && manual.RecommendationReason == "Manual" && HomeAssistantAdvisor.Best(manual)?.Evidence.Length == 0, "A user supplied model may guide a choice, explicitly as manual information.");
        return Task.CompletedTask;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static Task WeakAndUnknown()
    {
        foreach (var device in new[]
        {
            new NetworkDevice(),
            new NetworkDevice { Hostname = "shelly-tasmota-esphome", Vendor = "TP-Link / Philips / Espressif", DeviceType = "Camera", OpenPorts = { 80, 443, 554, 1883, 6053 } },
            new NetworkDevice { SsdpServer = "UPnP/1.0 MediaServer", MdnsServices = { "_http._tcp.local", "_thread._udp.local" }, HttpTitle = "TP-Link Router" }
        })
        {
            var result = HomeAssistantAdvisor.Assess(device);
            Require(result.Candidates.All(item => item.Basis == "Research") && result.RecommendedDomain == null, "Names, vendor, type and ports cannot establish observed integration support.");
            Require(result.Steps.Contains("Identify") && result.Steps.Contains("NonIp") && result.Steps.Contains("Community"), "Unknown means guided investigation, not unsupported.");
        }
        var frozen = new NetworkDevice { HttpTitle = "Tasmota", Identification = new DeviceIdentification() };
        Require(HomeAssistantAdvisor.Assess(frozen).Candidates.Length == 0, "Saved recognition evidence is not replaced by later mutable fields.");
        return Task.CompletedTask;
    }

    internal static Task SupportedClues()
    {
        foreach (var (domain, device) in new (string, NetworkDevice)[]
        {
            ("shelly", new() { HttpTitle = "Shelly Pro" }),
            ("hue", new() { HttpServer = "Philips Hue bridge" }),
            ("esphome", new() { MdnsServices = { "_ESPHOMELIB._TCP.LOCAL." } }),
            ("tasmota", new() { HttpTitle = "Tasmota" }),
            ("tplink", new() { HttpTitle = "Tapo device" }),
            ("onvif", new() { SsdpServer = "ONVIF" }),
            ("dlna_dmr", new() { SsdpServer = "urn:schemas-upnp-org:device:MediaRenderer:1" }),
            ("homekit_controller", new() { MdnsServices = { "_hap._tcp.local" } }),
            ("matter", new() { MdnsServices = { "_matterc._udp.local" } }),
            ("mqtt", new() { HttpServer = "mosquitto" })
        })
        {
            var result = HomeAssistantAdvisor.Assess(device);
            var candidate = result.Candidates.Single(item => item.Domain == domain);
            Require(candidate.Basis == "Observed" && candidate.Evidence.Length > 0 && candidate.Evidence.All(fact => fact.ObservedAtUtc == null), "Self-reported clues retain their source without inventing times.");
            Require(candidate.Requirements.Length > 0, "A clue does not waive integration prerequisites.");
        }
        foreach (var entry in HomeAssistantAdvisor.Integrations)
            Require(entry.DocumentationUrl.StartsWith("https://", StringComparison.Ordinal) &&
                (new Uri(entry.DocumentationUrl).Host == "www.home-assistant.io" || entry.Requirements.Contains("CommunitySource") &&
                new[] { "https://github.com/marq24/ha-goecharger-api2", "https://github.com/AlexxIT/SonoffLAN" }.Contains(entry.DocumentationUrl)), "Only fixed official documentation or explicitly marked community project URLs are launched.");
        return Task.CompletedTask;
    }

    internal static Task ProvenanceAndManual()
    {
        var time = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var device = new NetworkDevice { Identification = new DeviceIdentification { Evidence =
            [new() { Source = "HTTP", Value = "Tasmota / Shelly", ObservedAtUtc = time, Reused = true }] } };
        var conflict = HomeAssistantAdvisor.Assess(device);
        Require(conflict.Status == "Conflict" && conflict.Candidates.Length == 2, "Conflicting platforms must not silently choose a best integration.");
        Require(conflict.Candidates.All(item => item.Evidence.Single().ObservedAtUtc == time && item.Evidence.Single().Reused), "Old/cached evidence keeps its original provenance.");
        var unknown = new NetworkDevice();
        var context = new HomeAssistantContext { ManufacturerModel = "Tasmota", Protocol = "mqtt", Goal = "Control" };
        var manual = HomeAssistantAdvisor.Assess(unknown, context);
        Require(manual.Status == "Research" && manual.Candidates.All(item => item.Basis == "Manual" && item.Evidence.Length == 0), "User inputs are research leads, never captured evidence.");
        Require(manual.Steps.Contains("Control") && unknown.HomeAssistant == null && unknown.Identification == null, "Advising never mutates identity or persistent data.");
        Require(HomeAssistantAdvisor.Assess(new NetworkDevice { SnmpDescr = "sysDescr fixture" }).Candidates.Single().Basis == "Research", "SNMP response alone does not provide ready-made HA entities.");
        var hostile = HomeAssistantAdvisor.Assess(new NetworkDevice { HttpTitle = new string('x', 600) + "Tasmota", HttpServer = "<script>Tasmota</script>\r\n\0" });
        Require(hostile.Candidates.Single().Evidence.All(item => item.Value.Length <= 512 && !item.Value.Any(char.IsControl)), "Advice consumes bounded text and strips control characters.");
        var copy = conflict.Snapshot(); conflict.Candidates[0].Requirements[0] = "changed";
        Require(copy.Candidates.All(item => !item.Requirements.Contains("changed")), "Advice snapshots own their nested arrays.");
        var roundtrip = JsonSerializer.Deserialize<HomeAssistantAdvice>(JsonSerializer.Serialize(copy))!;
        Require(roundtrip.CatalogueRevision == HomeAssistantAdvisor.CatalogueRevision && roundtrip.Candidates[0].Evidence[0].Reused, "Advice JSON preserves catalogue and capture provenance.");
        NetworkScanner.IdentifyDevice(unknown);
        Require(unknown.HomeAssistant is { Status: "Unknown" }, "Even an unidentified scanned device has a useful plan.");
        Require(!JsonSerializer.Serialize(new NetworkDevice()).Contains("HomeAssistant"), "Legacy/unassessed optional API contract stays absent.");
        return Task.CompletedTask;
    }

    internal static Task ResearchRecommendations()
    {
        var time = new DateTimeOffset(2025, 5, 6, 7, 8, 9, TimeSpan.Zero);
        foreach (var (expected, source, hint) in new[]
        {
            ("tuya", "OUI", "Tuya Smart Inc."), ("reolink", "OUI", "Reolink Innovation Limited"),
            ("denonavr", "Name", "Denon-AVR-X1800H.example"), ("goecharger_api2", "Name", "go-eCharger_123456.example"),
            ("sonoff", "Name", "sonoff-relay.example"), ("proxmoxve", "Name", "pve-test.example")
        })
        {
            var advice = HomeAssistantAdvisor.Assess(new NetworkDevice { Identification = new DeviceIdentification { Evidence =
                [new() { Source = source, Value = hint, ObservedAtUtc = time, Reused = true }] } });
            Require(advice.Status == "Research" && advice.RecommendedDomain == null && HomeAssistantAdvisor.Best(advice) == null,
                "A name/OUI lead does not claim a supported integration: " + expected);
            var research = HomeAssistantAdvisor.ResearchChoice(advice);
            Require(research?.Domain == expected && research.Evidence.Single().ObservedAtUtc == time && research.Evidence.Single().Reused,
                "Useful research suggestion retains its real provenance: " + expected);
            Require(advice.RecommendationReason == "ResearchChoice", "Exports explain weak recommendations.");
        }
        foreach (var module in new[] { new NetworkDevice { Hostname = "ESP_A1B2C3.example" }, new NetworkDevice { Vendor = "Espressif Inc." } })
        {
            var advice = HomeAssistantAdvisor.Assess(module);
            Require(HomeAssistantAdvisor.FirmwareResearch(advice) && HomeAssistantAdvisor.ResearchChoice(advice) == null && advice.RecommendedDomain == null,
                "Generic ESP hardware gets firmware alternatives, never an arbitrary integration.");
            Require(advice.Candidates.Select(x => x.Domain).OrderBy(x=>x).SequenceEqual(new[] { "esphome", "sonoff", "tasmota" }), "ESP routes include original eWeLink as well as alternative firmware.");
            var refined = HomeAssistantAdvisor.Assess(module, new HomeAssistantContext { ManufacturerModel = "Tasmota" });
            Require(refined.RecommendedDomain == "tasmota" && HomeAssistantAdvisor.Best(refined)?.Basis == "Manual", "Explicit firmware input refines a pre-existing research lead.");
        }
        var tuyaModule = HomeAssistantAdvisor.Assess(new NetworkDevice { Hostname = "ESP_A1B2C3.example", Vendor = "Tuya Smart Inc." });
        Require(!HomeAssistantAdvisor.FirmwareResearch(tuyaModule) && HomeAssistantAdvisor.ResearchChoice(tuyaModule)?.Domain == "tuya", "A known ecosystem lead precedes generic chip routes.");
        Require(HomeAssistantAdvisor.Assess(new NetworkDevice { HttpTitle = "Sonoff Basic - Tasmota", Vendor = "Espressif Inc." }).RecommendedDomain == "tasmota", "Hardware branding does not contradict explicitly identified replacement firmware.");
        var firmware = HomeAssistantAdvisor.Assess(new NetworkDevice { Vendor = "Espressif Inc.", HttpTitle = "Tasmota" });
        Require(firmware.RecommendedDomain == "tasmota" && !HomeAssistantAdvisor.FirmwareResearch(firmware), "Captured firmware wins over chip-level research.");
        foreach (var device in new[] {
            new NetworkDevice { Vendor = "Proxmox Server Solutions GmbH", Hostname = "guest.example", OpenPorts = { 22, 8006 } },
            new NetworkDevice { Vendor = "TP-Link", OpenPorts = { 80 } },
            new NetworkDevice { Vendor = "Samsung", Hostname = "phone.example" },
            new NetworkDevice { Hostname = "notgo-eCharger-malicious.example" } })
            Require(HomeAssistantAdvisor.Assess(device).Candidates.Length == 0, "Virtual NIC vendors, generic brands and ports do not invent a platform.");
        var frozen = new NetworkDevice { Hostname = "go-eCharger_123456.example", Vendor = "Tuya", Identification = new DeviceIdentification() };
        Require(HomeAssistantAdvisor.Assess(frozen).Candidates.Length == 0, "No mutable name/vendor is substituted for a saved evidence snapshot.");
        var conflict = HomeAssistantAdvisor.Assess(new NetworkDevice { Vendor = "Reolink", Identification = new DeviceIdentification {
            ConflictingSignals = true, Evidence = [new() { Source="OUI", Value="Reolink" }] } });
        Require(HomeAssistantAdvisor.ResearchChoice(conflict) == null && !HomeAssistantAdvisor.FirmwareResearch(conflict), "Research never overrides a conflict.");
        foreach (var (expected, banner) in new[] { ("denonavr", "Denon AVR-X1800H"), ("reolink", "Reolink camera"), ("tuya", "Tuya Smart Life"), ("proxmoxve", "Proxmox VE"), ("goecharger_api2", "go-eCharger"), ("sonoff", "eWeLink") })
            Require(HomeAssistantAdvisor.Assess(new NetworkDevice { HttpTitle = banner }).RecommendedDomain == expected, "Specific product response is a captured platform hint: " + expected);
        var roundtrip = JsonSerializer.Deserialize<HomeAssistantAdvice>(JsonSerializer.Serialize(tuyaModule))!;
        Require(roundtrip.RecommendedDomain == null && HomeAssistantAdvisor.ResearchChoice(roundtrip)?.Domain == "tuya", "JSON distinguishes research from supported-protocol selection.");
        return Task.CompletedTask;
    }
}
