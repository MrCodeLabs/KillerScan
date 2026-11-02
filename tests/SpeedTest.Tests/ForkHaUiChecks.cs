using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using KillerScan.Controls;
using KillerScan.Engine;
using KillerScan.Services;

internal static class ForkHaUiChecks
{
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static void Run(KillerScan.Shell.MainWindow window, Action<FrameworkElement, string> capture)
    {
        Require(DemoData.Enabled && IsolatedSettings.Enabled, "Fork UI checks require isolated demo settings/data.");
        var workspace = (ScanWorkspace)typeof(KillerScan.Shell.MainWindow).GetField("_scanWorkspace", Instance)!.GetValue(window)!;
        Require(workspace.FindName("HomeAssistantButton") is Button button && button.Visibility == Visibility.Visible && button.IsEnabled, "The direct HA button survives the upstream title-bar redesign.");
        var overview = workspace.CreateHomeAssistantOverview();
        Require(overview.Rows.Count == workspace.ScannedDevices.Count, "The HA toolbar still includes all found devices.");
        capture((FrameworkElement)overview.Content, "Fork-HA-All.png"); overview.Close();
        var known = new NetworkDevice { IpAddress = "192.0.2.60", HttpTitle = "Tasmota" }; NetworkScanner.IdentifyDevice(known);
        var saved = ScanHistory.Record(known.IpAddress, new[] { known }).Current.Devices.Single();
        Require(HomeAssistantAdvisor.ForDevice(HomeAssistantText.FromHistory(saved)).RecommendedDomain == "tasmota", "The upgraded history preserves fork HA snapshots.");
        foreach (Locale locale in Enum.GetValues(typeof(Locale)))
        {
            typeof(KillerScan.Shell.MainWindow).GetMethod("Lang_Checked", Instance)!.Invoke(window, new object[] { new RadioButton { Tag = locale.ToString() }, new RoutedEventArgs() });
            var dictionary = Application.Current.Resources.MergedDictionaries.Last();
            foreach (string key in HomeAssistantText.English.Keys) Require(Application.Current.TryFindResource("Str_HA_" + key) is string, locale + " retains HA translation " + key);
            var unknown = new HomeAssistantAdviceWindow(new NetworkDevice(), _ => throw new Exception("Opening HA must not browse or query"), _ => { });
            Require(unknown.CurrentAdvice.Status == "Unknown", "Unknown devices do not gain compatibility during upgrade.");
            unknown.IdentityInput.Text = "Tasmota";
            Require(unknown.CurrentAdvice.Candidates.Single().Basis == "Manual", "Manual context remains separate from network facts.");
            capture((FrameworkElement)unknown.Content, "Fork-HA-" + locale + ".png"); unknown.Close();
        }
        Console.WriteLine("PASS: fork HA toolbar, individual advice, old/new history, manual provenance and all 20 languages on the 1.9 UI.");
    }
}
