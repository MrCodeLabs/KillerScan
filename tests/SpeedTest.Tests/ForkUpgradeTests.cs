using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KillerScan;
using KillerScan.Engine;
using KillerScan.Services;

internal static class ForkUpgradeTests
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static Task Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "KillerScan-fork-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Environment.SetEnvironmentVariable("KILLERSCAN_DATA_ROOT", root);
        LocationService.Root = root; DemoData.Enabled = false;
        string a = "11111111111111111111111111111111", b = "22222222222222222222222222222222";
        File.WriteAllText(Path.Combine(root, "locations.json"), JsonSerializer.Serialize(new { Version = 1, SelectedId = a,
            Locations = new[] { new { Id = "legacy", Name = "Legacy fixture" }, new { Id = a, Name = "Site A" }, new { Id = b, Name = "Site B" } } }));
        string stale = "[{\"Name\":\"Stale root backup\",\"Target\":\"192.0.2.1\"}]";
        File.WriteAllText(Path.Combine(root, "profiles.json"), stale);
        foreach (string id in new[] { a, b })
        {
            string folder = Path.Combine(root, "locations", id); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "devices.json"), "{\"Names\":{\"mac:02:00:00:00:00:10\":\"" + id + " device\"},\"Trusted\":[\"mac:02:00:00:00:00:10\"]}");
            File.WriteAllText(Path.Combine(folder, "overrides.json"), "{\"02:00:00:00:00:10\":\"IoT\"}");
            File.WriteAllText(Path.Combine(folder, "profiles.json"), "[{\"Name\":\"Same profile\",\"Target\":\"192.0.2.10\",\"DeepScanAfter\":true,\"InterfaceId\":\"disconnected-fixture-adapter\",\"SourceAddress\":\"192.0.2.2\",\"LocationId\":\"" + id + "\"}]");
            File.WriteAllText(Path.Combine(folder, "history.json"), "[{\"ScannedAt\":\"2026-10-20T12:00:00Z\",\"Target\":\"192.0.2.10\",\"LocationId\":\"" + id + "\",\"Devices\":[{\"Identity\":\"mac:02:00:00:00:00:10\",\"IpAddress\":\"192.0.2.10\",\"MacAddress\":\"02:00:00:00:00:10\",\"Identification\":{\"Evidence\":[{\"Source\":\"HTTP\",\"Value\":\"Tasmota\",\"Reused\":true,\"ObservedAtUtc\":\"2026-10-20T12:00:00Z\"}]},\"Access\":[{\"Protocol\":\"https\",\"Port\":8443,\"Verification\":\"HttpResponse\",\"Authentication\":\"Challenge\"}]}]}]");
            File.WriteAllText(Path.Combine(folder, "survey.json"), "{\"Version\":1,\"LocationId\":\"" + id + "\",\"Entries\":[]}");
        }
        var before = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        LocationService.Load("Unassigned");
        Require(LocationService.LoadError == null && LocationService.Items.Count == 3, "A fork catalog opens without recreating Locations or resurrecting stale root profiles.");
        Require(LocationService.Initial().Info?.Id == a, "The fork's explicit selected Location survives its first upgraded startup.");
        foreach (string id in new[] { a, b })
        {
            var data = LocationService.Get(LocationService.Items.Single(item => item.Id == id));
            Require(data.ReadError == null, "Old fork stores remain readable: " + data.ReadError);
            var device = new NetworkDevice { IpAddress = "192.0.2.10", MacAddress = "02:00:00:00:00:10" }; data.Devices.Apply(device);
            Require(device.Hostname == id + " device" && data.Devices.IsTrusted(device) && data.Overrides.Get(device.MacAddress) == "IoT", "Reused IP/MAC retain separate names, approvals and type overrides.");
            var profile = data.SavedScans.Items.Single(); Require(profile.DeepScanAfter && profile.Adapter == "disconnected-fixture-adapter" && profile.SourceAddress == "192.0.2.2", "A disconnected explicit adapter is preserved instead of silently becoming Automatic.");
            var saved = data.History.Entries.Single().Devices.Single();
            Require(HomeAssistantAdvisor.ForDevice(HomeAssistantText.FromHistory(saved)).RecommendedDomain == "tasmota" && saved.Access![0].Login == AccessLogin.Requested, "Old histories keep their observed HA evidence and authentication uncertainty.");
            Require(saved.Identification!.Evidence.Single().ObservedAtUtc?.Day == 20, "Old observation dates are never renewed by loading history.");
        }
        foreach (var entry in before) Require(entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key)), "Reading an old fork must preserve original file bytes: " + Path.GetFileName(entry.Key));
        var first = LocationService.Get(LocationService.Items.Single(item => item.Id == a));
        var scanned = new NetworkDevice { IpAddress = "192.0.2.11", MacAddress = "02:00:00:00:00:11", HttpTitle = "Tasmota" }; NetworkScanner.IdentifyDevice(scanned);
        first.History.Record(scanned.IpAddress, new[] { scanned }); first.History.Load();
        Require(first.History.Entries.Any(entry => entry.Devices.Any(device => device.HomeAssistant?.RecommendedDomain == "tasmota")), "New observations keep HA snapshots after history reload.");
        Require(LocationService.Get(LocationService.Items.Single(item => item.Id == b)).History.Entries.Count == 1, "A write at one Location cannot append to another Location's history.");
        App.SetSetting("ForkFixture", "čžš"); Require(App.GetSetting("ForkFixture") == "čžš" && File.Exists(Path.Combine(root, "settings.json")), "Isolated settings stay beside the isolated data, not in installed settings.");
        Console.WriteLine("PASS: existing fork catalog, two Locations, profiles, explicit adapters, evidence/history, untouched survey files and isolated settings. " + root);
        return Task.CompletedTask;
    }
}
