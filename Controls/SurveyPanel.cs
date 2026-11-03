using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using KillerScan.Services;

namespace KillerScan.Controls;

internal sealed class SurveyPanel : DockPanel
{
    internal static void Theme(Window window) => window.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("pack://application:,,,/KillerScan;component/Controls/SurveyStyles.xaml") });
    private readonly SurveyStore _store;
    private readonly Action<string> _open, _copy;
    private readonly NetworkDevice[] _devices;
    internal readonly DataGrid Grid = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
        SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column, MinRowHeight = 44 };
    private readonly TextBlock _status = Text("");
    private readonly List<Button> _actions = [];
    internal sealed class Row(SurveyEntry entry)
    {
        internal SurveyEntry Entry { get; } = entry;
        public string Name => Entry.Name;
        public string Room => Entry.Room;
        public string Functions => Entry.DesiredFunctions;
        public string Observation => ObservationText(Entry);
    }
    internal SurveyPanel(SurveyStore store, IEnumerable<NetworkDevice> devices, Action<string> open, Action<string> copy)
    {
        _store = store; _devices = devices.ToArray(); _open = open; _copy = copy;
        var notice = Text(SurveyText.L("Notice")); SetDock(notice, Dock.Top); Children.Add(notice);
        SetDock(_status, Dock.Top); Children.Add(_status);
        var actions = new WrapPanel(); SetDock(actions, Dock.Bottom); Children.Add(actions);
        void Action(string key, Action run)
        { var button = Button(SurveyText.L(key), run, key.ToLowerInvariant()); actions.Children.Add(button); _actions.Add(button); }
        Action("Manual", () => Edit(new SurveyEntry { Manual = true }, true));
        Action("Edit", () => { if (Selected is { } entry) Edit(entry, false); });
        Action("Remove", () =>
        {
            RemoveSelected(_ => MessageBox.Show(Window.GetWindow(this), SurveyText.L("ConfirmRemove"), SurveyText.L("Title"),
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes);
        });
        Action("Link", Link);
        Grid.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush"); Grid.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        Grid.SetResourceReference(DataGrid.RowStyleProperty, "DarkDataGridRow"); Grid.SetResourceReference(DataGrid.CellStyleProperty, "DarkDataGridCell");
        Grid.SetResourceReference(DataGrid.ColumnHeaderStyleProperty, "ThemedDataGridColumnHeader");
        foreach (var (key, path, width) in new[] { ("Name", "Name", 180d), ("Room", "Room", 130d), ("Functions", "Functions", 240d), ("Observation", "Observation", 420d) })
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6)));
            Grid.Columns.Add(new DataGridTextColumn { Header = SurveyText.L(key), Binding = new Binding(path), Width = width, ElementStyle = style });
        }
        Grid.MouseDoubleClick += (_, args) => { if (ItemsControl.ContainerFromElement(Grid, args.OriginalSource as DependencyObject) is DataGridRow && Selected is { } entry) Edit(entry, false); };
        Children.Add(Grid); Reload();
    }
    private SurveyEntry? Selected => (Grid.SelectedItem as Row)?.Entry;
    internal void RemoveSelected(Func<SurveyEntry, bool> confirm)
    { if (Selected is { } entry && confirm(entry)) Run(() => _store.Remove(entry.Id)); }
    internal void Reload()
    {
        try
        {
            Grid.ItemsSource = _store.Load().Select(entry => new Row(entry)).ToArray(); _status.Text = "";
            foreach (var action in _actions) action.IsEnabled = true;
        }
        catch (Exception) { _status.Text = SurveyText.L("Error"); Grid.ItemsSource = null; foreach (var action in _actions) action.IsEnabled = false; }
    }
    internal bool Run(Action action)
    {
        try { action(); Reload(); return true; }
        catch (Exception) { _status.Text = SurveyText.L("Error"); return false; }
    }
    internal void AddSelected(IEnumerable<NetworkDevice> selected)
    {
        foreach (var device in selected)
        {
            SurveyEntry? added = null;
            if (!Run(() => added = _store.AddObserved(device, candidates => Choose(Window.GetWindow(this), candidates))) || added == null) break;
        }
    }
    internal static string? Choose(Window owner, IReadOnlyList<SurveyEntry> candidates)
    {
        var choices = new ComboBox { MinWidth = 280, Margin = new Thickness(8), DisplayMemberPath = "Name", ItemsSource = candidates, SelectedIndex = 0 };
        string? result = null;
        choices.SetResourceReference(StyleProperty, "DarkComboBox");
        var dialog = Prompt(owner, SurveyText.L("IpWarning"), choices, buttons =>
        {
            buttons.Children.Add(Button(SurveyText.L("Link"), () => { result = (choices.SelectedItem as SurveyEntry)?.Id; Window.GetWindow(buttons).Close(); }));
            buttons.Children.Add(Button(SurveyText.L("New"), () => { result = ""; Window.GetWindow(buttons).Close(); }));
        });
        dialog.ShowDialog(); return result;
    }
    private void Link()
    {
        if (Selected is not { } entry) return;
        var choices = new ComboBox { MinWidth = 280, Margin = new Thickness(8), ItemsSource = _devices.Select((device, index) => new { Name = device.IpAddress + " · " + device.Hostname, Index = index }).ToArray(), DisplayMemberPath = "Name", SelectedIndex = _devices.Length > 0 ? 0 : -1 };
        choices.SetResourceReference(StyleProperty, "DarkComboBox");
        var dialog = Prompt(Window.GetWindow(this), SurveyText.L("ConfirmLink") + "\n" + entry.Name, choices, buttons =>
        {
            var link = Button(SurveyText.L("Link"), () =>
            {
                if (choices.SelectedIndex >= 0 && Run(() => _store.Link(entry.Id, _devices[choices.SelectedIndex]))) Window.GetWindow(buttons).Close();
            }); link.IsEnabled = _devices.Length > 0; buttons.Children.Add(link);
        }); dialog.ShowDialog();
    }
    private static Window Prompt(Window owner, string message, FrameworkElement choices, Action<WrapPanel> add)
    {
        var dialog = new Window { Title = SurveyText.L("Title"), Width = 560, Height = 300, MinWidth = 360, MinHeight = 240,
            Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme(dialog);
        dialog.SetResourceReference(Window.BackgroundProperty, "BackgroundBrush"); dialog.SetResourceReference(Window.ForegroundProperty, "TextBrush");
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(Text(message)); panel.Children.Add(choices);
        var buttons = new WrapPanel(); add(buttons); var cancel = Button(SurveyText.L("Cancel"), dialog.Close); cancel.IsCancel = true; buttons.Children.Add(cancel); panel.Children.Add(buttons);
        dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; return dialog;
    }
    private void Edit(SurveyEntry entry, bool create)
    {
        var dialog = new SurveyEditorWindow(entry, updated => { if (create) _store.AddManual(updated); else _store.Save(updated); }, _open, _copy)
            { Owner = Window.GetWindow(this) };
        dialog.ShowDialog(); Reload();
    }
    internal static string ObservationText(SurveyEntry entry) => entry.Observation is not { } observation ? SurveyText.L("NoObservation") :
        SurveyText.L("Observation") + ": " + HomeAssistantText.Time(observation.LastSeenUtc) + "\n" + observation.Device.IpAddress + " · " + observation.Device.MacAddress;
    internal static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) };
    internal static Button Button(string label, Action action, string tag = "")
    {
        var button = new Button { Content = label, Tag = tag, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 8, 8, 8) };
        button.SetResourceReference(StyleProperty, "OutlineButton"); button.Click += (_, _) => action(); return button;
    }
}
