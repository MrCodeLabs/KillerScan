using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using KillerScan.Engine;
using KillerScan.Services;

internal static class SurveyTests
{
    private static int _checks;
    private static void Require(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Refuses(Action action, string message)
    { bool refused = false; try { action(); } catch (Exception) { refused = true; } Require(refused, message); }
    private static readonly DateTimeOffset First = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private static NetworkDevice Device(string ip = "192.0.2.20", string mac = "02:00:00:00:00:20", int day = 0) => new()
    {
        IpAddress = ip, MacAddress = mac, Hostname = "fixture", HttpTitle = "Tasmota", OpenPorts = new() { 80 },
        Identification = new DeviceIdentification { ObservationCompletedAtUtc = First.AddDays(day),
            Evidence = new[] { new RecognitionEvidence { Source = "HTTP", Value = "Tasmota", Origin = "fixture", ObservedAtUtc = First.AddDays(day) } } }
    };
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "KillerScan-survey-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "fixture.txt"), "Synthetic survey test");
        foreach (string phase in new[] { "create", "restart" })
        {
            using var process = Process.Start(new ProcessStartInfo(typeof(SurveyTests).Assembly.Location, "--survey-phase " + phase + " \"" + root + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
            string output = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
            Require(process.WaitForExit(30000) && process.ExitCode == 0, "Fresh-process survey phase " + phase + ": " + error); Console.Write(output);
        }
        var a = new SurveyStore(Path.Combine(root, "a", "survey.json"), "site-a");
        var original = a.Load().Single(entry => !entry.Manual);
        a.Refresh(new[] { Device("192.0.2.99", day: 2) });
        var refreshed = a.Load().Single(entry => entry.Id == original.Id);
        Require(refreshed.Observation!.Device.IpAddress == "192.0.2.99" && refreshed.Room == "Kuhinja čžš <&>" && refreshed.DesiredFunctions == "Temperature\nControl", "MAC refresh changes IP without changing manual fields or stable ID.");
        a.Save(original with { Notes = "Saved from an older editor" });
        Require(a.Load().Single(entry => entry.Id == original.Id).Observation!.Device.IpAddress == "192.0.2.99", "Saving an older editor preserves the latest scan snapshot.");
        a.Refresh(Array.Empty<NetworkDevice>());
        Require(a.Load().Single(entry => entry.Id == original.Id).Observation!.LastSeenUtc == First.AddDays(2), "Absent devices retain their last observation.");
        a.AddObserved(Device("192.0.2.1", day: 1), _ => throw new Exception("Unexpected IP prompt"));
        Require(a.Load().Single(entry => entry.Id == original.Id).Observation!.LastSeenUtc == First.AddDays(2), "Adding an older observation does not roll the last sighting backwards.");
        var noMac = a.AddObserved(Device("192.0.2.50", ""), _ => throw new Exception())!;
        byte[] before = File.ReadAllBytes(Path.Combine(root, "a", "survey.json")); int prompts = 0;
        Require(a.AddObserved(Device("192.0.2.50", ""), candidates => { prompts++; Require(candidates.Single().Id == noMac.Id, "IP choice lists only explicit candidates."); return null; }) == null, "Canceling an IP match adds nothing.");
        Require(prompts == 1 && before.SequenceEqual(File.ReadAllBytes(Path.Combine(root, "a", "survey.json"))), "IP match requires confirmation and cancellation preserves disk bytes.");
        a.AddObserved(Device("192.0.2.50", ""), _ => noMac.Id);
        Require(a.Load().Count(entry => entry.Observation?.Device.IpAddress == "192.0.2.50") == 1, "Confirmed IP-only match keeps the stable entry.");
        Refuses(() => a.AddObserved(Device("192.0.2.50", ""), _ => noMac.Id, new SurveyEntry { Name = "New draft" }), "A new draft cannot overwrite existing manual fields while linking.");
        a.AddObserved(Device("192.0.2.50", ""), _ => "");
        Require(a.Load().Count(entry => entry.Observation?.Device.IpAddress == "192.0.2.50") == 2, "The user can instead create a separate IP-only entry.");
        var manual = a.Load().Single(entry => entry.Manual);
        Require(manual.Observation == null && manual.LinkedMac == "", "Manual device needs no IP or MAC.");
        a.AddObserved(Device("192.0.2.77", "02:00:00:00:00:77"), _ => "");
        Require(a.Load().Single(entry => entry.Id == manual.Id).Observation == null, "Adding network results never merges a manual entry.");
        a.Link(manual.Id, Device("192.0.2.78", "02:00:00:00:00:78"));
        a.Refresh(new[] { Device("192.0.2.79", "02:00:00:00:00:78", 3) });
        Require(a.Load().Single(entry => entry.Id == manual.Id).Observation!.Device.IpAddress == "192.0.2.79", "An explicitly linked manual entry can refresh its observation.");
        Refuses(() => a.Link(manual.Id, Device()), "Explicit linking refuses a MAC already attached elsewhere.");
        var lead = new SurveyEntry { Manual = true, Name = "No IP", ManufacturerModel = "Tasmota", Protocol = "zha", Goal = "Control" };
        var advised = HomeAssistantAdvisor.Assess(SurveyStore.AdviceDevice(lead), lead.Context);
        Require(advised.Status == "Research" && advised.Candidates.All(candidate => candidate.Basis == "Manual" && candidate.Evidence.Length == 0 && candidate.Requirements.Length > 0), "Manual leads stay proposals requiring verification, never observed facts or confirmed compatibility.");
        var cached = SurveyStore.AdviceDevice(refreshed);
        Require(cached.Identification!.Evidence.All(fact => fact.Reused && fact.ObservedAtUtc == First.AddDays(2)), "Saved evidence retains its original date and is marked reused.");
        Require(!refreshed.Observation.Device.Identification!.Evidence[0].Reused, "Advisory rendering cannot mutate stored evidence.");
        var edited = refreshed with { Name = "Changed", Notes = "Пример 日本語 🔌\n<&> =value", Observation = null, LinkedMac = "" };
        a.Save(edited);
        Require(a.Load().Single(entry => entry.Id == edited.Id).Observation != null && a.Load().Single(entry => entry.Id == edited.Id).LinkedMac != "", "Manual editor cannot replace network evidence/binding.");
        Require(a.Load().Single(entry => entry.Id == edited.Id).Notes == edited.Notes, "Special characters and multiline text survive persistence.");
        Refuses(() => a.AddManual(new SurveyEntry { Name = " " }), "Only name is required and cannot be blank.");
        Refuses(() => a.Save(edited with { Name = "" }), "Invalid edits are rejected before publication.");
        a.Remove(noMac.Id); Require(!a.Load().Any(entry => entry.Id == noMac.Id), "Remove affects only the selected survey entry.");
        Require(File.Exists(Path.Combine(root, "a", "survey.json.bak")), "Atomic updates retain the previous version as backup.");
        var emptyPath = Path.Combine(root, "empty", "survey.json");
        var empty = new SurveyStore(emptyPath, "empty"); empty.Refresh(new[] { Device() });
        var wrongLocation = Device(); wrongLocation.LocationId = "different";
        Refuses(() => empty.AddObserved(wrongLocation, _ => ""), "A foreign-location observation cannot be attached to this inventory.");
        Require(empty.Load().Count == 0 && !File.Exists(emptyPath), "A scan never creates an inventory or adds an unknown device.");
        var longName = Device(); longName.Hostname = new string('a', 159) + "🔌.example";
        var named = empty.AddObserved(longName, _ => throw new Exception());
        Require(empty.Load().Single().Name == new string('a', 159) && named?.Name.Length == 159,
            "Long observed hostnames fit the survey name limit without splitting a Unicode character.");
        Corruption(root);
        var memory = new SurveyStore(Path.Combine(root, "memory.json"), "demo", true); memory.AddManual(new SurveyEntry { Name = "Demo" });
        Require(memory.Load().Count == 1 && !File.Exists(Path.Combine(root, "memory.json")), "Demo storage does not write persistent data.");
        Console.WriteLine("PASS: " + _checks + " survey persistence, isolation, matching, stale-evidence and failure assertions; two fresh processes.");
        Console.WriteLine("Synthetic survey artifacts: " + root);
    }
    internal static void Phase(string phase, string root)
    {
        Require(Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(), "KillerScan-survey-"), StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(root, "fixture.txt")), "Only this test's synthetic root is accepted.");
        foreach (string id in new[] { "a", "b" })
        {
            var store = new SurveyStore(Path.Combine(root, id, "survey.json"), "site-" + id);
            if (phase == "create")
            {
                var device = Device(); var entry = store.AddObserved(device, _ => throw new Exception())!;
                device.OpenPorts.Clear(); Require(store.Load().Single().Observation!.Device.OpenPorts.Count == 1, "Stored snapshots do not alias live devices.");
                store.Save(entry with { Room = id == "a" ? "Kuhinja čžš <&>" : "Other room", ManufacturerModel = "User model", DesiredFunctions = "Temperature\nControl", Firmware = "1.2", Connection = "Wi-Fi", Notes = "Keep" });
                store.AddManual(new SurveyEntry { Name = "Zigbee sensor", Protocol = "zha", Goal = "Monitor" });
                store.AddObserved(Device("192.0.2.21", "02-00-00-00-00-20", 1), _ => throw new Exception("MAC must match without a prompt"));
            }
            else Require(phase == "restart", "Known fresh-process phase.");
            var found = store.Load(); Require(found.Count == 2 && found.Single(entry => !entry.Manual).Room == (id == "a" ? "Kuhinja čžš <&>" : "Other room"), "Same IP/MAC remain isolated across locations and fresh processes.");
            Require(found.Single(entry => !entry.Manual).ManufacturerModel == "User model", "Re-adding a MAC preserves manually entered model.");
        }
        var catalog = new LocationCatalog(Path.Combine(root, "catalog"));
        catalog.Load("Unassigned");
        Require(!LocationData.StoreFiles.Contains("survey.json"), "Survey data never enters automatic legacy migration.");
        foreach (string name in new[] { "Synthetic A", "Synthetic B" })
        {
            LocationInfo? location;
            if (phase == "create") Require(catalog.Create(name, out location), "Create a real Location.");
            else location = catalog.Items.Single(item => item.Name == name);
            var scoped = catalog.Open(location!).Survey!;
            if (phase == "create") scoped.AddObserved(Device(), _ => throw new Exception());
            Require(scoped.Load().Single().Observation!.Device.LocationId == location!.Id, "Real location catalog scopes same-IP/MAC survey snapshots after restart.");
        }
        Console.WriteLine("PASS: survey " + phase + " (" + _checks + " assertions).");
    }
    private static void Corruption(string root)
    {
        string path = Path.Combine(root, "broken.json");
        foreach (string text in new[] { "{", "null", "{\"Version\":2,\"LocationId\":\"correct\",\"Entries\":[]}", "{\"Version\":1,\"LocationId\":\"wrong\",\"Entries\":[]}",
            "{\"Version\":1,\"LocationId\":\"correct\"}", "{\"Version\":1,\"LocationId\":\"correct\",\"Entries\":[{\"Name\":\"Missing ID\"}]}" })
        {
            File.WriteAllText(path, text); var store = new SurveyStore(path, "correct");
            Refuses(() => store.Load(), "Corrupt/version/location mismatch is reported.");
            Refuses(() => store.AddManual(new SurveyEntry { Name = "Keep original" }), "A failed read cannot be overwritten by saving.");
            Require(File.ReadAllText(path) == text && !File.Exists(path + ".bak"), "Unreadable original is preserved byte-for-byte.");
        }
        string blocked = Path.Combine(root, "blocked.json"); var blockedStore = new SurveyStore(blocked, "correct");
        blockedStore.AddManual(new SurveyEntry { Name = "Before" }); byte[] before = File.ReadAllBytes(blocked);
        using (new FileStream(blocked, FileMode.Open, FileAccess.Read, FileShare.Read))
            Refuses(() => blockedStore.AddManual(new SurveyEntry { Name = "After" }), "A locked destination reports write failure.");
        Require(before.SequenceEqual(File.ReadAllBytes(blocked)) && blockedStore.Load().Count == 1, "Failed publication keeps disk and subsequent reads consistent.");
        Require(!Directory.GetFiles(root, "*.tmp").Any(), "Failed writes clean only their own temporary files.");
    }
}
