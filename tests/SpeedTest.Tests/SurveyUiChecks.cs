using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KillerScan.Controls;
using KillerScan.Engine;
using KillerScan.Services;

internal static class SurveyUiChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static int _checks;
    private static void Require(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }
    private static System.Collections.Generic.IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        { if (child is T item) yield return item; foreach (var next in Children<T>(child)) yield return next; }
    }
    internal static void Run(Action<FrameworkElement, string> capture)
    {
        Require(DemoData.Enabled && IsolatedSettings.Enabled, "Survey UI tests require isolated demo data.");
        var location = LocationData.Demo(new LocationInfo { Id = "survey-fixture", Name = "Synthetic survey" });
        var store = location.Survey!;
        var originalIds = store.Load().Select(entry => entry.Id).ToArray();
        var beforeHistory = ScanHistory.Entries.ToArray();
        var device = new NetworkDevice { IpAddress = "192.0.2.180", MacAddress = "02:00:00:00:00:B4", Hostname = "Synthetic survey fixture" };
        var temporary = new HomeAssistantOverviewWindow(new[] { device }, _ => { }, _ => { }, LocationData.Temporary());
        Require(temporary.Rows.Count == 1 && !Children<SurveyPanel>((DependencyObject)temporary.Content).Any(),
            "Temporary scans still offer advice but do not create an inventory in another Location.");
        temporary.Close();
        var overview = new HomeAssistantOverviewWindow(new[] { device }, _ => throw new Exception("No browser allowed"), _ => { }, location);
        var tabs = Children<TabControl>((DependencyObject)overview.Content).Single();
        Require(tabs.Items.Count == 2 && overview.Rows.Count == 1, "HA retains current scan and adds a separate location inventory tab.");
        var survey = Children<SurveyPanel>((DependencyObject)overview.Content).Single();
        Require(store.Load().Count == originalIds.Length, "Opening HA does not add any found device.");
        overview.Grid.SelectAll();
        var add = Children<Button>((DependencyObject)overview.Content).Single(button => button.Tag as string == "survey-add");
        Require(add.IsEnabled, "Explicit multi-selection enables adding found devices.");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(store.Load().Count == originalIds.Length + 1, "Only the explicitly selected scan device is persisted.");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(store.Load().Count == originalIds.Length + 1, "Adding the same MAC does not create another entry.");
        tabs.SelectedIndex = 1;
        var entry = store.Load().Single(item => item.LinkedMac == SurveyStore.Mac(device.MacAddress));
        var editor = new SurveyEditorWindow(entry, store.Save, _ => throw new Exception("No network allowed"), _ => { });
        editor.Inputs["Room"].Text = "Cancelled room"; editor.Inputs["Notes"].Text = "Cancelled note"; editor.Close();
        Require(store.Load().Single(item => item.Id == entry.Id).Room == "", "Closing the editor discards manual changes.");
        editor = new SurveyEditorWindow(entry, store.Save, _ => { }, _ => { });
        editor.Inputs["Room"].Text = "Living room"; editor.Inputs["Functions"].Text = "Temperature and dimming";
        var advice = editor.CreateAdvice(); advice.IdentityInput.Text = "Tasmota";
        advice.ProtocolInput.SelectedItem = advice.ProtocolInput.Items.Cast<ComboBoxItem>().Single(item => item.Tag as string == "zha");
        advice.GoalInput.SelectedItem = advice.GoalInput.Items.Cast<ComboBoxItem>().Single(item => item.Tag as string == "Control");
        Children<Button>((DependencyObject)advice.Content).Single(button => button.Tag as string == "survey-save").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(store.Load().Single(item => item.Id == entry.Id).ManufacturerModel == "", "Accepting advisor hints alone does not save the parent editor.");
        Require(editor.TrySave(), "An explicit editor save persists manual data and advisor hints."); editor.Close();
        entry = store.Load().Single(item => item.Id == entry.Id);
        Require(entry.Room == "Living room" && entry.DesiredFunctions.Contains("dimming") && entry.Protocol == "zha" && entry.Goal == "Control" && entry.ManufacturerModel == "Tasmota", "All manual fields and HA context survive reopening.");
        advice = new HomeAssistantAdviceWindow(device, _ => { }, _ => { }, surveyStore: store);
        Require(advice.IdentityInput.Text == "Tasmota" && advice.CurrentAdvice.Candidates.All(candidate => candidate.Basis == "Manual"), "Existing HA actions reuse saved manual hints with manual provenance."); advice.Close();
        var manualDraft = new SurveyEntry { Name = "Manual Zigbee sensor", Manual = true };
        var manualEditor = new SurveyEditorWindow(manualDraft, store.AddManual, _ => { }, _ => { });
        Require(manualEditor.TrySave(), "Manual creation requires only a name, without any IP or MAC."); manualEditor.Close();
        survey.Reload(); survey.Grid.SelectedItem = survey.Grid.Items.Cast<SurveyPanel.Row>().Single(row => row.Entry.Id == manualDraft.Id);
        int confirmations = 0; survey.RemoveSelected(_ => { confirmations++; return false; });
        Require(confirmations == 1 && store.Load().Any(item => item.Id == manualDraft.Id), "Removal always asks for confirmation and cancel keeps the record.");
        survey.RemoveSelected(_ => true);
        Require(!store.Load().Any(item => item.Id == manualDraft.Id) && ScanHistory.Entries.SequenceEqual(beforeHistory), "Confirmed removal does not delete scan history.");
        foreach (Locale locale in Enum.GetValues(typeof(Locale)))
        {
            typeof(LocaleManager).GetMethod("ApplyInternal", Static)!.Invoke(null, new object[] { locale });
            var dictionary = Application.Current.Resources.MergedDictionaries.Single(resource =>
                resource.Source?.OriginalString.EndsWith("/Strings/" + LocaleManager.LanguageTag + ".xaml", StringComparison.OrdinalIgnoreCase) == true);
            foreach (string key in SurveyText.Keys) Require(dictionary.Contains("Str_Survey_" + key), locale + " has own survey translation: " + key);
            var localized = new SurveyEditorWindow(entry, _ => { }, _ => { }, _ => { });
            capture((FrameworkElement)localized.Content, "Survey-" + locale + ".png"); localized.Close();
        }
        typeof(LocaleManager).GetMethod("ApplyInternal", Static)!.Invoke(null, new object[] { Locale.SlSI });
        foreach (Theme theme in Enum.GetValues(typeof(Theme)))
        {
            typeof(ThemeManager).GetMethod("LoadDict", Static)!.Invoke(null, new object[] { theme, Accent.Blue });
            foreach (double scale in new[] { 1.25, 2.0 })
            {
                var themed = new SurveyEditorWindow(entry, _ => { }, _ => { }, _ => { });
                var root = (FrameworkElement)themed.Content; root.LayoutTransform = new ScaleTransform(scale, scale);
                capture(root, "Survey-" + theme + "-" + scale + ".png");
                Require(Children<ScrollViewer>(root).Any(scroll => scroll.ViewportHeight > 0) && Children<Button>(root).Single(button => button.Tag as string == "save").ActualHeight > 0, "Scrollable editor and save action remain available at " + scale + " in " + theme);
                themed.Close();
            }
        }
        capture((FrameworkElement)overview.Content, "Survey-Inventory.png"); overview.Close();
        foreach (var added in store.Load().Where(item => !originalIds.Contains(item.Id))) store.Remove(added.Id);
        typeof(ThemeManager).GetMethod("LoadDict", Static)!.Invoke(null, new object[] { Theme.Black, Accent.Orange });
        typeof(LocaleManager).GetMethod("ApplyInternal", Static)!.Invoke(null, new object[] { Locale.EnUS });
        Console.WriteLine("PASS: " + _checks + " survey UI assertions, all 20 locales, 13 themes and 125%/200% scaling; no network actions.");

    }
}
