using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillerScan.Engine;
using KillerScan.Services;

internal static class ReportLogoTests
{
    public static Task Run()
    {
        string html = ReportExport.BuildHtml([new NetworkDevice { IpAddress = "192.0.2.10" }], "192.0.2.0/24", "dark");
        string[] lines = html.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        string Line(string prefix) => lines.Single(line => line.StartsWith(prefix, StringComparison.Ordinal));

        // Runs the report's own logo script under Windows Script Host against a minimal page: the
        // image reports load the way a browser does, firing onload only when its src changes.
        string script = string.Join("\r\n",
            "var wm={style:{display:''}};",
            "var img={style:{display:'none'},src:undefined,loaded:undefined,",
            "  getAttribute:function(n){return this.src},removeAttribute:function(n){this.src=undefined}};",
            "var document={documentElement:{className:''},getElementById:function(id){return id=='logo'?img:wm}};",
            Line("var CUSTOM_ACCENT="),
            Line("var LOGO="),
            Line("function updateLogo(){"),
            "function show(t){document.documentElement.className='theme-'+t;updateLogo();",
            "  if(img.src!==undefined&&img.src!==img.loaded){img.loaded=img.src;img.style.display='';wm.style.display='none';}",
            "  if(img.src===undefined)img.loaded=undefined;",
            "  return (img.style.display!=='none')+','+(wm.style.display!=='none');}",
            "WScript.Echo(show('blood')+'|'+show('se98')+'|'+show('blood'));");
        string path = Path.Combine(Path.GetTempPath(), "KillerScan-ReportLogo-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(path, script);
        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cscript.exe"),
                "//nologo //E:jscript \"" + path + "\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Report logo script failed: " + error);
            if (output != "true,false|false,true|true,false")
                throw new InvalidOperationException(
                    "The report logo must hide for 98SE and come back when a logo theme returns. Got: " + output);
        }
        finally { File.Delete(path); }
        return Task.CompletedTask;
    }
}
