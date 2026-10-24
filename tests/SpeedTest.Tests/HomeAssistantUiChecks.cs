using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using KillerScan.Controls;
using KillerScan.Engine;
using KillerScan.Services;
using KillerScan.Shell;

internal static class HomeAssistantUiChecks
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        { if (child is T item) yield return item; foreach (var next in Children<T>(child)) yield return next; }
    }
    internal static void Run(MainWindow window, Action<FrameworkElement, string> capture)
    {
        Require(DemoData.Enabled && IsolatedSettings.Enabled, "HA GUI tests require isolated demo settings/data.");
        var known = new NetworkDevice { IpAddress = "192.0.2.60", Hostname = "ha-fixture<script>", HttpTitle = "Tasmota <script>fixture</script>" };
        NetworkScanner.IdentifyDevice(known);
        var entries = ScanHistory.Entries.ToArray();
        var stored = ScanHistory.Record(known.IpAddress, new[] { known }).Current.Devices.Single();
        var originalAdvice = known.HomeAssistant!;
        originalAdvice.Candidates[0].Requirements[0] = "changed";
        Require(stored.HomeAssistant!.Candidates[0].Requirements[0] != "changed", "History has an independent advisory snapshot.");
        known.HomeAssistant = HomeAssistantAdvisor.Assess(known); ScanHistory.SeedDemo(entries);
        var restored = JsonSerializer.Deserialize<HistoricalDevice>(JsonSerializer.Serialize(stored))!;
        Require(HomeAssistantAdvisor.ForDevice(HomeAssistantText.FromHistory(restored)).Candidates.Single().Domain == "tasmota", "Saved history keeps the original catalogue match.");
        string html = ReportExport.BuildHtml(new[] { known }, known.IpAddress, "black");
        Require(html.Contains("Home Assistant") && html.Contains("&lt;script&gt;") && !html.Contains("Tasmota <script>"), "HA HTML details escape remote evidence.");
        Require(ReportExport.BuildCsv(new[] { known }).Contains("Home Assistant JSON"), "Device CSV includes structured advice.");
        var workspace = (ScanWorkspace)typeof(MainWindow).GetField("_scanWorkspace", Instance)!.GetValue(window)!;
        var devices = (DataGrid)workspace.FindName("ResultsGrid"); devices.UnselectAll(); devices.CurrentItem = null;
        workspace.SetView("devices");
        var global = (Button)workspace.FindName("HomeAssistantButton");
        Require(global.Visibility == Visibility.Visible && global.IsEnabled, "HA entry is available with no selected or recognized device.");
        var empty = workspace.CreateHomeAssistantAdvice();
        Require(empty.CurrentAdvice.Status == "Unknown", "Per-device advice supports an unidentified device."); empty.Close();
        var all = workspace.CreateHomeAssistantOverview();
        Require(all.Rows.Count == workspace.ScannedDevices.Count, "The top HA button includes every found device regardless of selection."); all.Close();
        Require(workspace.FindName("ColHa") is DataGridTemplateColumn, "Every device row has its direct HA choice.");
        Require(devices.ContextMenu.Items.OfType<MenuItem>().Any(item => item.Header as string == HomeAssistantText.L("Title")), "Device menu exposes HA advice.");
        var serviceMenu = ((DataGrid)workspace.FindName("ServicesGrid")).ContextMenu;
        Require(serviceMenu.Items.OfType<MenuItem>().Any(item => item.Tag as string == "HomeAssistant"), "Service menu exposes HA advice.");
        var history = new HistoryWorkspace();
        var historyMenu = (ContextMenu)typeof(HistoryWorkspace).GetMethod("BuildHistoryMenu", Instance)!.Invoke(history, new object?[] { null })!;
        historyMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
        Require(!historyMenu.Items.OfType<MenuItem>().Single(item => item.Tag as string == "ha").IsEnabled, "Empty history cannot open a stale device advisory.");
        foreach (Locale locale in Enum.GetValues(typeof(Locale)))
        {
            typeof(MainWindow).GetMethod("Lang_Checked", Instance)!.Invoke(window, new object[] { new RadioButton { Tag = locale.ToString() }, new RoutedEventArgs() });
            var resources = Application.Current.Resources.MergedDictionaries[locale == Locale.EnUS ? 1 : 2];
            foreach (var key in HomeAssistantText.English.Keys) Require(resources.Contains("Str_HA_" + key), locale + ": own HA translation " + key);
            int launched = 0; string? copied = null;
            var unknown = new NetworkDevice { IpAddress = "192.0.2.61" };
            var dialog = new HomeAssistantAdviceWindow(unknown, url =>
            { Require(url == HomeAssistantAdvisor.Integration("tasmota")!.DocumentationUrl, "Only the chosen fixed documentation URL opens."); launched++; }, value => copied = value);
            Require(launched == 0 && copied == null && dialog.CurrentAdvice.Candidates.Length == 0, "Opening an unknown-device guide does not connect, copy or invent support.");
            Require(dialog.CurrentAdvice.Steps.Contains("NonIp"), "An empty IPv4 result does not exclude non-IP integrations.");
            capture((FrameworkElement)dialog.Content, "HA-Unknown-" + locale + ".png");
            dialog.IdentityInput.Text = "Tasmota";
            dialog.GoalInput.SelectedItem = dialog.GoalInput.Items.Cast<ComboBoxItem>().Single(item => item.Tag as string == "Control");
            Require(dialog.CurrentAdvice.Candidates.Single().Basis == "Manual" && dialog.CurrentAdvice.Steps.Contains("Control"), "User leads and control prerequisites remain explicit.");
            var buttons = Children<Button>((DependencyObject)dialog.Content).ToArray();
            buttons.Single(item => item.Tag as string == HomeAssistantAdvisor.Integration("tasmota")!.DocumentationUrl).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            buttons.Single(item => item.Tag as string == "copy-plan").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(launched == 1 && copied != null && copied.Contains(HomeAssistantText.L("Manual")) && copied.Contains(HomeAssistantText.L("Control")), "Copy uses the selected locale, user provenance and actionable plan.");
            Require(unknown.HomeAssistant == null && unknown.Identification == null, "Window inputs do not change saved device identity.");
            dialog.Close();
            var observed = new HomeAssistantAdviceWindow(known, _ => throw new InvalidOperationException("No automatic browser call"), _ => { });
            Require(observed.CurrentAdvice.Candidates.Single().Basis == "Observed", "Captured protocol and manual research remain distinct.");
            capture((FrameworkElement)observed.Content, "HA-Observed-" + locale + ".png"); observed.Close();
            string? copiedAll = null;
            var batch = new HomeAssistantOverviewWindow(new[] { known, unknown }, _ => throw new InvalidOperationException("Batch must not connect"), text => copiedAll = text);
            Require(batch.Rows.Count == 2 && batch.Rows.Single(row => row.Address == known.IpAddress).Choice == "Tasmota"
                && batch.Rows.Single(row => row.Address == unknown.IpAddress).Choice == HomeAssistantText.L("NoChoice"), "The batch has a specific choice or a missing-information result for each device.");
            batch.Grid.SelectedItem = batch.Rows.Single(row => row.Address == known.IpAddress);
            var chosen = batch.CreateSelected();
            Require(chosen != null && chosen.CurrentAdvice.RecommendedDomain == "tasmota", "Batch details target their own device."); chosen!.Close();
            Children<Button>((DependencyObject)batch.Content).Single(button => button.Tag as string == "copy-all").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(copiedAll != null && copiedAll.Contains(known.IpAddress) && copiedAll.Contains(unknown.IpAddress) && copiedAll.Contains(HomeAssistantText.L("BestChoice")), "Copy-all includes each frozen device's localized choice and reason.");
            var researchDevice = new NetworkDevice { IpAddress = "192.0.2.62", Vendor = "Tuya Smart Inc.", Hostname = "plug.example" };
            var firmwareDevice = new NetworkDevice { IpAddress = "192.0.2.63", Hostname = "ESP_A1B2C3.example", Vendor = "Espressif Inc." };
            var researchBatch = new HomeAssistantOverviewWindow(new[] { researchDevice, firmwareDevice }, _ => throw new InvalidOperationException("No automatic request"), _ => { });
            Require(researchBatch.Rows.First().Choice.Contains("Tuya") && researchBatch.Rows.First().Advice.RecommendedDomain == null,
                "Name/vendor research survives freezing and remains labeled as a check, not a supported match.");
            Require(researchBatch.Rows.Last().Choice == HomeAssistantText.L("FirmwareChoice"), "ESP modules show a concrete firmware action in every locale.");
            researchBatch.Grid.SelectedItem = researchBatch.Rows.First();
            var researchDialog = researchBatch.CreateSelected()!;
            Require(Children<Button>((DependencyObject)researchDialog.Content).Any(b => b.Tag as string == HomeAssistantAdvisor.Integration("tuya")!.DocumentationUrl), "A research proposal has its direct documentation button.");
            Require(HomeAssistantText.Describe(researchDevice, researchDialog.CurrentAdvice).Contains(HomeAssistantText.L("ResearchNext")), "Copied research gives the same next step as the window.");
            capture((FrameworkElement)researchDialog.Content, "HA-Research-" + locale + ".png"); researchDialog.Close();
            capture((FrameworkElement)researchBatch.Content, "HA-Suggestions-" + locale + ".png"); researchBatch.Close();
            capture((FrameworkElement)batch.Content, "HA-All-" + locale + ".png"); batch.Close();
        }
        Console.WriteLine("PASS: HA direct choices, all-device overview, observed/unknown/manual details and copy-all in 20 languages; history snapshots and safe exports.");
    }
}
