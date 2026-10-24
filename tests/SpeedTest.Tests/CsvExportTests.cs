using System;
using System.Threading.Tasks;
using KillerScan.Engine;
using KillerScan.Features.Cli;
using KillerScan.Services;

internal static class CsvExportTests
{
    public static Task Run()
    {
        foreach (var (input, expected) in new (string Input, string Expected)[]
        {
            ("=1+1", "'=1+1"), ("+1", "'+1"), ("-1", "'-1"), ("@SUM(1)", "'@SUM(1)"),
            (" \t=1+1", "' \t=1+1"), ("printer", "printer"), ("a,\"b", "a,\"\"b"), ("-", "-"), (" + ", " + ")
        })
        {
            var device = new NetworkDevice { IpAddress = "192.0.2.10", Hostname = input };
            string csv = ReportExport.BuildCsv([device]);
            if (!csv.Contains("\"" + expected + "\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Device CSV did not encode hostname safely: " + input);
        }
        string noPorts = ReportExport.BuildCsv([new NetworkDevice { IpAddress = "192.0.2.11" }]);
        if (!noPorts.Contains(",\"-\"", StringComparison.Ordinal) || noPorts.Contains("'-", StringComparison.Ordinal))
            throw new InvalidOperationException("A host with no open ports must export a bare dash.");

        string cli = CliRunner.Csv([new NetworkDevice { IpAddress = "192.0.2.12", Hostname = "=1+1" },
            new NetworkDevice { IpAddress = "192.0.2.13", Hostname = "a,\"b" }], header: false);
        string[] rows = cli.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        if (rows.Length != 2 || !rows[0].StartsWith("192.0.2.12,'=1+1,", StringComparison.Ordinal) || !rows[0].Contains(",-,", StringComparison.Ordinal))
            throw new InvalidOperationException("CLI CSV must neutralize formulas and keep a bare dash for no ports: " + cli);
        if (!rows[1].StartsWith("192.0.2.13,\"a,\"\"b\",", StringComparison.Ordinal))
            throw new InvalidOperationException("CLI CSV must quote fields holding commas or quotes: " + cli);
        return Task.CompletedTask;
    }
}
