using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KillerScan.Engine;

internal static class ForkIntegrationTests
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static async Task Run()
    {
        await HomeAssistantTests.DirectChoices(); await HomeAssistantTests.WeakAndUnknown();
        await HomeAssistantTests.SupportedClues(); await HomeAssistantTests.ProvenanceAndManual(); await HomeAssistantTests.ResearchRecommendations();
        var old = JsonSerializer.Deserialize<NetworkDevice>("{\"IpAddress\":\"192.0.2.10\",\"Identification\":{\"Confidence\":\"Medium\",\"Evidence\":[{\"Source\":\"HTTP\",\"Value\":\"Tasmota\",\"Reused\":true,\"ObservedAtUtc\":\"2026-10-20T12:00:00Z\"}]},\"Access\":[{\"Protocol\":\"https\",\"Port\":8443,\"Verification\":\"HttpResponse\",\"Authentication\":\"Challenge\",\"ObservedAtUtc\":\"2026-10-20T12:00:00Z\"}]}")!;
        Require(HomeAssistantAdvisor.ForDevice(old).RecommendedDomain == "tasmota", "Existing fork evidence still drives HA advice.");
        Require(old.Access![0].Evidence == AccessEvidence.HttpResponse && old.Access[0].Login == AccessLogin.Requested && old.Access[0].ObservedAt?.Day == 20, "Existing fork Access JSON preserves response, login uncertainty and time.");
        var external = new NetworkDevice { HttpTitle = "Tasmota" }; NetworkScanner.IdentifyDevice(external);
        Require(external.Identification!.ObservationCompletedAtUtc == null && external.Identification.Evidence.All(fact => fact.ObservedAtUtc == null), "Assessing imported fields never invents a scan time.");
        var sample = new NetworkDevice { HttpTitle = "Tasmota", Recognition = NetworkScanner.AssessDevice(external, null) };
        ForkRecognition.Capture(sample);
        Require(sample.HomeAssistant?.RecommendedDomain == "tasmota" && sample.Recognition != null, "Upstream recognition and the existing HA contract coexist.");
        await LiveLoopback();
    }
    private static async Task LiveLoopback()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client; try { client = await listener.AcceptTcpClientAsync(); } catch { break; }
                _ = Task.Run(async () =>
                {
                    using (client)
                    {
                        try
                        {
                            using var stream = client.GetStream(); var bytes = new byte[4096]; int n = await stream.ReadAsync(bytes, 0, bytes.Length, stop.Token);
                            if (n < 3 || bytes[0] != (byte)'G') return;
                            byte[] body = Encoding.UTF8.GetBytes("<html><title>Tasmota</title></html>");
                            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                            await stream.WriteAsync(header, 0, header.Length, stop.Token); await stream.WriteAsync(body, 0, body.Length, stop.Token);
                        }
                        catch { }
                    }
                });
            }
        });
        int[]? previous = NetworkScanner.DeepPortsOverride;
        try
        {
            NetworkScanner.DeepPortsOverride = [port];
            var options = new ScanOptions().Without("neighbor-cache,arp,icmp,tcp-liveness,dns,mdns,ssdp,ssh,snmp,netbios,tls") with { ProbeUnknownPorts = true };
            DateTimeOffset began = DateTimeOffset.UtcNow;
            var found = await new NetworkScanner().DeepProbeHostAsync("127.0.0.1", stop.Token, options, flushLocalDnsCache: false);
            Require(found.HomeAssistant?.RecommendedDomain == "tasmota", "The actual upstream deep probe populates the HA advice.");
            Require(found.Recognition != null && found.Identification?.Scope == "deep", "Both recognition contracts survive real loopback probing.");
            Require(found.Identification!.Evidence.Where(fact => fact.Source == "HTTP").Any(fact => !fact.Reused && fact.ObservedAtUtc >= began), "Fresh HTTP evidence has a real capture date, never a manual lead.");
            Require(found.Identification.DisabledFeatures.Contains("mdns"), "Per-run disabled checks remain visible to HA evidence consumers.");
        }
        finally { NetworkScanner.DeepPortsOverride = previous; stop.Cancel(); listener.Stop(); await server; }
    }
}
