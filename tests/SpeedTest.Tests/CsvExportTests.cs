using System;
using System.Threading.Tasks;
using KillerScan.Engine;
using KillerScan.Services;

internal static class CsvExportTests
{
    public static Task Run()
    {
        foreach (var (input, expected) in new (string Input, string Expected)[]
        {
            ("=1+1", "'=1+1"), ("+1", "'+1"), ("-1", "'-1"), ("@SUM(1)", "'@SUM(1)"),
            (" \t=1+1", "' \t=1+1"), ("printer", "printer"), ("a,\"b", "a,\"\"b")
        })
        {
            var device = new NetworkDevice { IpAddress = "192.0.2.10", Hostname = input };
            string csv = ReportExport.BuildCsv([device]);
            if (!csv.Contains("\"" + expected + "\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Device CSV did not encode hostname safely: " + input);
        }
        return Task.CompletedTask;
    }
}
