using System.Net;
using KillerScan.Engine;

internal static class MulticastDiscoveryTests
{
    private sealed class ArpOnly : NetworkScanner.ScanProbes
    {
        internal int Calls;
        internal override string DiscoverMac(IPAddress address) { Interlocked.Increment(ref Calls); return address.ToString() == "192.0.2.10" ? "00:00:5E:00:53:10" : ""; }
    }
    private sealed class RouteFixture : INetworkPlatform
    {
        internal string Hop = "192.0.2.1";
        internal int Resolves;
        public IReadOnlyDictionary<string, string> ReadNeighborCache() => new Dictionary<string, string>();
        public string ResolveMac(IPAddress address) { Resolves++; return "00:00:5E:00:53:10"; }
        public void FlushDnsCache() { }
        public (string Interface, string NextHop)? BestRoute(IPAddress address) => ("Fixture", Hop);
    }
    internal static async Task ArpOnlyDiscovery()
    {
        var options = new ScanOptions().Without(string.Join(",", ScanOptions.Features)) with { Arp = true };
        var probes = new ArpOnly();
        var found = await new NetworkScanner(probes).ScanSubnetAsync("192.0.2.10-11", CancellationToken.None, options, false);
        Require(found.Single().IpAddress == "192.0.2.10" && found[0].MacAddress == "00:00:5E:00:53:10" && probes.Calls == 2,
            "Explicit ARP can discover an on-link host without ping, TCP, cache or multicast, retaining its resolved MAC.");
        var off = new ArpOnly();
        Require((await new NetworkScanner(off).ScanSubnetAsync("192.0.2.10", CancellationToken.None, options with { Arp = false }, false)).Count == 0 && off.Calls == 0,
            "ARP off prevents the new discovery path as well as enrichment.");
        var previous = NetworkPlatform.Current;
        try
        {
            var route = new RouteFixture(); NetworkPlatform.Current = route;
            var io = new NetworkScanner.ScanProbes();
            Require(io.DiscoverMac(IPAddress.Parse("192.0.2.10")) == "" && route.Resolves == 0, "A routed next hop is never used to discover a remote device by ARP.");
            route.Hop = "";
            Require(io.DiscoverMac(IPAddress.Parse("192.0.2.10")) == "00:00:5E:00:53:10" && route.Resolves == 1, "A direct on-link route permits ARP discovery.");
        }
        finally { NetworkPlatform.Current = previous; }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Responders : NetworkScanner.ScanProbes
    {
        internal bool FailMdns;
        internal override Task<Dictionary<string, MulticastDiscovery.MdnsInfo>> Mdns(CancellationToken ct)
        {
            if (FailMdns) return Task.FromException<Dictionary<string, MulticastDiscovery.MdnsInfo>>(new InvalidOperationException("Fixture failure"));
            var device = new MulticastDiscovery.MdnsInfo { Name = "fixture-speaker" };
            device.Services.Add("living-room._sonos._tcp.local");
            return Task.FromResult(new Dictionary<string, MulticastDiscovery.MdnsInfo> { ["192.0.2.10"] = device, ["192.0.2.12"] = new(), ["198.51.100.10"] = device });
        }
        internal override Task<Dictionary<string, string>> Ssdp(CancellationToken ct) => Task.FromResult(new Dictionary<string, string>
        { ["192.0.2.11"] = "Roku/fixture", ["198.51.100.11"] = "Roku/outside-target" });
        internal override Task<int?> Ping(IPAddress address, int timeout) => throw new InvalidOperationException("Disabled ICMP");
        internal override Task<bool> TcpAlive(IPAddress address) => throw new InvalidOperationException("Disabled TCP liveness");
        internal override IReadOnlyDictionary<string, string> ReadNeighbors() => throw new InvalidOperationException("Disabled cache");
        internal override string ResolveMac(IPAddress address) => throw new InvalidOperationException("Disabled ARP");
    }
    internal static async Task SilentHosts()
    {
        var options = new ScanOptions().Without(string.Join(",", ScanOptions.Features)) with { Mdns = true, Ssdp = true };
        foreach (bool full in new[] { false, true })
        {
            var scanner = new NetworkScanner(new Responders());
            var emitted = new List<NetworkDevice>(); scanner.DeviceFound += device => { lock (emitted) emitted.Add(device); };
            var devices = await scanner.ScanSubnetAsync("192.0.2.10-12", CancellationToken.None, options, full);
            Require(devices.Count == 2 && emitted.Count == 2, "mDNS/SSDP-only hosts are discovered in full and quick scans.");
            Require(devices.All(device => device.IpAddress.StartsWith("192.0.2.", StringComparison.Ordinal)), "Out-of-target responders are never admitted.");
            Require(devices[0].Hostname == "fixture-speaker" && devices[0].Identification!.Evidence.Any(fact => fact.Source == "mDNS" && fact.ObservedAtUtc != null), "Quick and full paths retain actual multicast evidence/times.");
            Require(devices[0].HomeAssistant!.RecommendedDomain == "sonos" && devices[1].HomeAssistant!.RecommendedDomain == "roku", "Discovered devices receive their own direct integration choices.");
        }
        var partial = await new NetworkScanner(new Responders { FailMdns = true }).ScanSubnetAsync("192.0.2.10-11", CancellationToken.None, options);
        Require(partial.Single().IpAddress == "192.0.2.11", "Failure of one collector does not lose the other.");
        var disabled = await new NetworkScanner(new Responders()).ScanSubnetAsync("192.0.2.10-11", CancellationToken.None, options with { Mdns = false, Ssdp = false });
        Require(disabled.Count == 0, "Disabled collectors neither run nor create live hosts.");
    }
    internal static Task SsdpTypes()
    {
        var map = new Dictionary<string, string>();
        var mdns = new Dictionary<string, MulticastDiscovery.MdnsInfo>();
        MulticastDiscovery.ParseResponse(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, "192.0.2.10", mdns);
        MulticastDiscovery.ParseResponse(new byte[] { 0, 0, 128, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, "192.0.2.11", mdns);
        Require(mdns.Count == 0, "Queries and empty DNS responses do not become phantom discoveries.");
        MulticastDiscovery.RecordSsdp(map, "192.0.2.10", "HTTP/1.1 200 OK\r\nSERVER: Linux UPnP/1.0\r\nST: upnp:rootdevice\r\n\r\n");
        MulticastDiscovery.RecordSsdp(map, "192.0.2.10", "HTTP/1.1 200 OK\r\nSERVER: Linux UPnP/1.0\r\nST: urn:schemas-upnp-org:device:MediaRenderer:1\r\n\r\n");
        Require(map.Single().Value.Contains("MediaRenderer") && map.Single().Value.Contains("rootdevice"), "A later specific type is retained alongside the generic first response.");
        var advice = HomeAssistantAdvisor.Assess(new NetworkDevice { SsdpServer = map.Single().Value });
        Require(advice.RecommendedDomain == "dlna_dmr", "The real SSDP type reaches DLNA choice even with a generic SERVER header.");
        MulticastDiscovery.RecordSsdp(map, "192.0.2.11", "NOTIFY * HTTP/1.1\r\nSERVER: Roku\r\n\r\n");
        Require(map.Count == 1, "Unsolicited request text is not accepted as a discovery response.");
        for (int index = 0; index < 100; index++) MulticastDiscovery.RecordSsdp(map, "192.0.2.10", "HTTP/1.1 200 OK\r\nST: " + new string('x', 1000) + index + "\r\n\r\n");
        Require(map["192.0.2.10"].Length <= 1024, "Repeated SSDP responses have a bounded evidence budget.");
        return Task.CompletedTask;
    }
}
