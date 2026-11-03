using System.Windows;
using System.Windows.Controls;
using KillerScan.Engine;
using KillerScan.Services;


namespace KillerScan.Controls;

internal sealed class HomeAssistantAdviceWindow : Window
{
    private readonly NetworkDevice _device;
    private readonly Action<string> _open;
    private readonly Action<string> _copy;
    internal readonly TextBox IdentityInput = new() { MaxLength = 160 };
    internal readonly ComboBox ProtocolInput = new();
    internal readonly ComboBox GoalInput = new();
    internal readonly StackPanel Results = new();
    internal HomeAssistantAdvice CurrentAdvice { get; private set; }
    private HomeAssistantContext CurrentContext => new()
    { ManufacturerModel = IdentityInput.Text, Protocol = ((ComboBoxItem?)ProtocolInput.SelectedItem)?.Tag as string ?? "",
        Goal = ((ComboBoxItem?)GoalInput.SelectedItem)?.Tag as string ?? "Monitor" };

    internal HomeAssistantAdviceWindow(NetworkDevice device, Action<string> open, Action<string> copy,
        HomeAssistantContext? initialContext = null, Action<HomeAssistantContext>? apply = null, string? observation = null,
        SurveyStore? surveyStore = null)
    {
        _device = HomeAssistantText.Freeze(device);
        SurveyEntry? savedEntry = null;
        if (apply == null && surveyStore != null)
        {
            try
            {
                string identity = SurveyStore.Mac(_device.MacAddress);
                savedEntry = identity == "" ? null : surveyStore.Load().SingleOrDefault(entry => entry.LinkedMac == identity);
                initialContext ??= savedEntry?.Context;
            }
            catch (Exception) { observation = SurveyText.L("Error"); }
        }
        _open = open; _copy = copy; CurrentAdvice = HomeAssistantAdvisor.ForDevice(_device);
        Title = HomeAssistantText.L("Title"); Width = 920; Height = 760; MinWidth = 540; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Loaded += (_, _) => KillerFamily.Windowing.WorkAreaFit.Fit(this);
        SetResourceReference(BackgroundProperty, "BackgroundBrush"); SetResourceReference(ForegroundProperty, "TextBrush"); SetResourceReference(FontFamilyProperty, "UiFont");
        var panel = new DockPanel { Margin = new Thickness(18) };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var catalogue = Button(HomeAssistantText.L("Docs")); catalogue.Tag = HomeAssistantAdvisor.CatalogueUrl;
        catalogue.Click += (_, _) => _open(HomeAssistantAdvisor.CatalogueUrl);
        var copyPlan = Button(HomeAssistantText.L("Copy")); copyPlan.Tag = "copy-plan";
        copyPlan.Click += (_, _) => _copy(HomeAssistantText.Describe(_device, CurrentAdvice, CurrentContext));
        var close = Button(Application.Current.TryFindResource("Str_Sys_Close") as string ?? "Close"); close.Click += (_, _) => Close();
        buttons.Children.Add(catalogue); buttons.Children.Add(copyPlan); buttons.Children.Add(close);
        var save = Button(apply == null ? SurveyText.L("Add") : SurveyText.L("Hints")); save.Tag = "survey-save";
        save.Click += (_, _) =>
        {
            if (apply != null) { apply(CurrentContext); Close(); return; }
            try
            {
                var store = surveyStore ?? throw new InvalidOperationException();
                SurveyEntry? target = savedEntry;
                string? confirmedIpChoice = null;
                if (target == null)
                {
                    string mac = SurveyStore.Mac(_device.MacAddress);
                    var candidates = store.Load().Where(entry => !entry.Manual && _device.IpAddress.Length > 0 &&
                        entry.Observation?.Device.IpAddress == _device.IpAddress && (entry.LinkedMac == "" || mac == "")).ToArray();
                    if (candidates.Length > 0)
                    {
                        confirmedIpChoice = SurveyPanel.Choose(this, candidates);
                        if (confirmedIpChoice == null) return;
                        target = candidates.SingleOrDefault(entry => entry.Id == confirmedIpChoice);
                    }
                }
                bool manual = savedEntry == null && _device.IpAddress.Length == 0 && SurveyStore.Mac(_device.MacAddress).Length == 0;
                var draft = (target ?? new SurveyEntry { Name = manual ? "" : SurveyStore.DeviceName(_device), Manual = manual,
                    Observation = manual ? null : new SurveyObservation { Device = _device, LastSeenUtc = _device.Identification?.ObservationCompletedAtUtc } }) with
                    { ManufacturerModel = target?.ManufacturerModel ?? CurrentContext.ManufacturerModel,
                        Protocol = target?.Protocol ?? CurrentContext.Protocol, Goal = target?.Goal ?? CurrentContext.Goal };
                if (savedEntry != null) draft = draft with { ManufacturerModel = CurrentContext.ManufacturerModel, Protocol = CurrentContext.Protocol, Goal = CurrentContext.Goal };
                var editor = new SurveyEditorWindow(draft, entry =>
                {
                    if (savedEntry != null) { store.Save(entry); savedEntry = entry; return; }
                    if (manual) { store.AddManual(entry); savedEntry = entry; return; }
                    var saved = store.AddObserved(_device, candidates => confirmedIpChoice ?? SurveyPanel.Choose(this, candidates), entry);
                    if (saved == null) throw new OperationCanceledException();
                    savedEntry = saved;
                }, _open, _copy) { Owner = this };
                editor.ShowDialog();
            }
            catch (Exception) { MessageBox.Show(this, SurveyText.L("Error"), SurveyText.L("Title"), MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        if (apply != null || surveyStore != null) buttons.Children.Insert(0, save);
        DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var header = new StackPanel();
        string deviceLabel = (device.IpAddress + "  " + device.Hostname).Trim();
        header.Children.Add(Text(deviceLabel.Length == 0 ? HomeAssistantText.L("Workflow") : deviceLabel, 18));
        if (observation != null) header.Children.Add(Text(observation));
        var inputs = new StackPanel();
        inputs.Children.Add(Text(HomeAssistantText.L("Identity"))); IdentityInput.SetResourceReference(StyleProperty, "DarkTextBox");
        System.Windows.Automation.AutomationProperties.SetName(IdentityInput, HomeAssistantText.L("Identity")); inputs.Children.Add(IdentityInput);
        ProtocolInput.SetResourceReference(StyleProperty, "DarkComboBox");
        GoalInput.SetResourceReference(StyleProperty, "DarkComboBox");
        var fields = new WrapPanel();
        fields.Children.Add(Field(HomeAssistantText.L("Protocol"), ProtocolInput));
        ProtocolInput.Items.Add(new ComboBoxItem { Content = Application.Current.TryFindResource("Str_Access_Unknown") as string ?? "Unknown", Tag = "" });
        foreach (string domain in new[] { "matter", "homekit_controller", "mqtt", "dlna_dmr", "rest", "snmp", "zha", "zwave_js", "bluetooth" })
            ProtocolInput.Items.Add(new ComboBoxItem { Content = HomeAssistantAdvisor.Integration(domain)!.Name, Tag = domain });
        ProtocolInput.SelectedIndex = 0;
        fields.Children.Add(Field(HomeAssistantText.L("Goal"), GoalInput));
        foreach (var (id, label) in new[] { ("Monitor", "Monitor"), ("Control", "ControlGoal"), ("Media", "MediaGoal") })
            GoalInput.Items.Add(new ComboBoxItem { Content = HomeAssistantText.L(label), Tag = id });
        GoalInput.SelectedIndex = 0; inputs.Children.Add(fields);
        if (initialContext != null)
        {
            IdentityInput.Text = initialContext.ManufacturerModel;
            ProtocolInput.SelectedItem = ProtocolInput.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Tag as string == initialContext.Protocol) ?? ProtocolInput.Items[0];
            GoalInput.SelectedItem = GoalInput.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Tag as string == initialContext.Goal) ?? GoalInput.Items[0];
        }
        header.Children.Add(Expand(HomeAssistantText.L("Inputs"), inputs));
        DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
        panel.Children.Add(new ScrollViewer { Content = Results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = panel;
        IdentityInput.TextChanged += (_, _) => Refresh(); ProtocolInput.SelectionChanged += (_, _) => Refresh(); GoalInput.SelectionChanged += (_, _) => Refresh();
        Refresh();
    }
    private static FrameworkElement Field(string name, ComboBox input)
    {
        var field = new StackPanel { MinWidth = 230, Margin = new Thickness(0, 0, 14, 10) }; field.Children.Add(Text(name)); field.Children.Add(input);
        System.Windows.Automation.AutomationProperties.SetName(input, name); return field;
    }
    private void Refresh()
    {
        var context = CurrentContext;
        CurrentAdvice = context.ManufacturerModel.Length == 0 && context.Protocol.Length == 0 && context.Goal == "Monitor"
            ? HomeAssistantAdvisor.ForDevice(_device) : HomeAssistantAdvisor.Assess(_device, context);
        Results.Children.Clear();
        Results.Children.Add(Text(HomeAssistantText.L("BestChoice"), 14));
        Results.Children.Add(Text(HomeAssistantText.Choice(CurrentAdvice), 24));
        Results.Children.Add(Text(HomeAssistantText.Reason(CurrentAdvice)));
        var best = HomeAssistantAdvisor.Best(CurrentAdvice) ?? HomeAssistantAdvisor.ResearchChoice(CurrentAdvice);
        if (best != null)
        {
            var entry = HomeAssistantAdvisor.Integration(best.Domain)!;
            Results.Children.Add(Text(HomeAssistantText.L(best.Basis)));
            Results.Children.Add(Text(HomeAssistantText.L("Requirements") + ": " + string.Join("; ", best.Requirements.Select(HomeAssistantText.L))));
            Results.Children.Add(Text(HomeAssistantText.Next(CurrentAdvice)));
            if (CurrentAdvice.Steps.Contains("Control")) Results.Children.Add(Text(HomeAssistantText.L("Control")));
            var docs = Button(HomeAssistantText.L("Docs") + " — " + entry.Name); docs.Tag = entry.DocumentationUrl;
            docs.Click += (_, _) => _open(entry.DocumentationUrl); Results.Children.Add(docs);
        }
        Results.Children.Add(Text(HomeAssistantText.Catalogue(CurrentAdvice)));
        Results.Children.Add(Text(HomeAssistantText.L("Note")));
        var details = new StackPanel();
        details.Children.Add(Text(HomeAssistantText.L("Fresh")));
        foreach (var candidate in CurrentAdvice.Candidates)
        {
            var entry = HomeAssistantAdvisor.Integration(candidate.Domain);
            if (entry == null) continue;
            details.Children.Add(Text(entry.Name + " — " + HomeAssistantText.L(candidate.Basis), 17));
            foreach (var fact in candidate.Evidence)
                details.Children.Add(Text(fact.Source + " · " + HomeAssistantText.Time(fact.ObservedAtUtc) +
                    (fact.Reused ? " [" + IdentificationText.L("Cached") + "]" : "") + "\n" + fact.Value));
            details.Children.Add(Text(HomeAssistantText.L("Requirements") + ": " + string.Join("; ", candidate.Requirements.Select(HomeAssistantText.L))));
            if (candidate.Domain != best?.Domain)
            {
                var docs = Button(HomeAssistantText.L("Docs") + " — " + entry.Name); docs.Tag = entry.DocumentationUrl;
                docs.Click += (_, _) => _open(entry.DocumentationUrl); details.Children.Add(docs);
            }
        }
        details.Children.Add(Text(HomeAssistantText.L("Workflow"), 18));
        for (int index = 0; index < CurrentAdvice.Steps.Length; index++) details.Children.Add(Text((index + 1) + ". " + HomeAssistantText.L(CurrentAdvice.Steps[index])));
        var expander = Expand(HomeAssistantText.L("Details"), details); expander.IsExpanded = best == null; Results.Children.Add(expander);
    }
    private static Expander Expand(string header, object content)
    {
        var expander = new Expander { Header = header, Content = content, Margin = new Thickness(0, 8, 0, 10) };
        expander.SetResourceReference(ForegroundProperty, "TextBrush"); return expander;
    }
    private static TextBlock Text(string value, double size = 13) => new()
    { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 7) };
    private static Button Button(string text)
    {
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 10, 10), Padding = new Thickness(10, 5, 10, 5) };
        button.SetResourceReference(StyleProperty, "OutlineButton"); return button;
    }
}
