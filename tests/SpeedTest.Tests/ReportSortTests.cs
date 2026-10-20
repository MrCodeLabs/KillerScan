using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KillerScan.Engine;
using KillerScan.Services;

internal static class ReportSortTests
{
    public static Task Run()
    {
        string html = ReportExport.BuildHtml([new NetworkDevice { IpAddress = "192.0.2.10", Hostname = "printer" }], "192.0.2.0/24", "dark");

        var headers = Regex.Matches(html, "<th(?: [^>]*)?>(.*?)</th>", RegexOptions.Singleline);
        if (headers.Count != 6) throw new InvalidOperationException("Expected six sortable headers, found " + headers.Count);
        foreach (Match header in headers)
        {
            if (!Regex.IsMatch(header.Groups[1].Value, "^<button type='button' class='sortbtn'>[^<]+<span class='arrow' aria-hidden='true'></span></button>$"))
                throw new InvalidOperationException("Header is not a keyboard-operable sort button: " + header.Value);
        }
        if (!html.Contains(".sortbtn:focus-visible{"))
            throw new InvalidOperationException("Sort buttons need a visible focus style.");
        if (!html.Contains("setAttribute('aria-sort',") || !html.Contains("removeAttribute('aria-sort')"))
            throw new InvalidOperationException("Sorting must expose the active direction through aria-sort.");
        return Task.CompletedTask;
    }
}
