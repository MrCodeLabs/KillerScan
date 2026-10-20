using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using KillerScan.Features.About;
using KillerScan.Services;
using KillerScan.Shell;

internal static class UpstreamSyncUiChecks
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static void Run(MainWindow window)
    {
        Require(DemoData.Enabled && IsolatedSettings.Enabled, "Sync checks require fabricated data and an isolated settings root.");
        var english = new ResourceDictionary { Source = new Uri("pack://application:,,,/KillerScan;component/Strings/en-US.xaml") };
        var slovenian = new ResourceDictionary { Source = new Uri("pack://application:,,,/KillerScan;component/Strings/sl-SI.xaml") };
        int checkedKeys = 0;
        foreach (string key in english.Keys.OfType<string>())
        {
            if (english[key] is not string source) continue;
            Require(slovenian.Contains(key) && slovenian[key] is string { Length: > 0 }, "Own Slovenian translation missing: " + key);
            string[] Placeholders(string text) => Regex.Matches(text, @"\{\d+(?:[^{}]*)\}").Cast<Match>().Select(match => match.Value).OrderBy(value => value).ToArray();
            Require(Placeholders(source).SequenceEqual(Placeholders((string)slovenian[key])), "Slovenian placeholder contract changed: " + key);
            checkedKeys++;
        }
        var about = (AboutController)typeof(MainWindow).GetField("_about", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        string? previous = IsolatedSettings.Get(AboutController.StartupCheckSetting);
        try
        {
            about.SetStartupCheck(false);
            Require(IsolatedSettings.Get(AboutController.StartupCheckSetting) == "0" && !AboutController.StartupCheckEnabled,
                "The real About handler persists disabled startup checking in isolated settings.");
            about.SetStartupCheck(true);
            Require(IsolatedSettings.Get(AboutController.StartupCheckSetting) == "1" && AboutController.StartupCheckEnabled,
                "The real About handler restores startup checking without querying the network.");
        }
        finally { IsolatedSettings.Set(AboutController.StartupCheckSetting, previous); }
        Console.WriteLine("PASS: " + checkedKeys + " own Slovenian translations/placeholders and actual startup-update setting isolation.");
    }
}
