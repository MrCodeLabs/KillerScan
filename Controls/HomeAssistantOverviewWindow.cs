using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using KillerScan.Engine;
using KillerScan.Services;

namespace KillerScan.Controls;

public sealed class HomeAssistantChoiceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value is NetworkDevice device ? HomeAssistantAdvisor.ForDevice(device) : value) is HomeAssistantAdvice advice
        ? HomeAssistantText.Choice(advice) : HomeAssistantText.L("NoChoice");
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>One frozen row per found device; does not launch network discovery or HA setup.</summary>
internal sealed class HomeAssistantOverviewWindow : Window
{
    internal sealed class Row(NetworkDevice device)
    {
        internal NetworkDevice Device { get; } = device;
        internal HomeAssistantAdvice Advice { get; } = HomeAssistantAdvisor.ForDevice(device);
        public string Address => Device.IpAddress;
        public string Name => Device.Hostname.Length > 0 ? Device.Hostname : Device.Identification?.ReportedModel ?? "";
        public string Choice => HomeAssistantText.Choice(Advice);
        public string Reason => HomeAssistantText.Reason(Advice);
        public string Requirements => (HomeAssistantAdvisor.Best(Advice) ?? HomeAssistantAdvisor.ResearchChoice(Advice)) is { } best
            ? string.Join("; ", best.Requirements.Select(HomeAssistantText.L)) : HomeAssistantAdvisor.FirmwareResearch(Advice) ? HomeAssistantText.L("FirmwareRoute") : HomeAssistantText.L("Identify");
    }
    internal IReadOnlyList<Row> Rows { get; }
    internal readonly DataGrid Grid = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
        SelectionMode = DataGridSelectionMode.Extended, HeadersVisibility = DataGridHeadersVisibility.Column,
        RowHeaderWidth = 0, MinRowHeight = 54, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Action<string> _open, _copy;
    private readonly SurveyStore? _survey;

    internal HomeAssistantOverviewWindow(IEnumerable<NetworkDevice> devices, Action<string> open, Action<string> copy, LocationData? location = null)
    {
        _open = open; _copy = copy;
        _survey = location?.Survey;
        SurveyPanel.Theme(this);
        Rows = devices.Select(HomeAssistantText.Freeze).OrderBy(device => device.IpSortKey).Select(device => new Row(device)).ToArray();
        Title = HomeAssistantText.L("AllTitle"); Width = 1180; Height = 720; MinWidth = 660; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Loaded += (_, _) => KillerFamily.Windowing.WorkAreaFit.Fit(this);
        SetResourceReference(BackgroundProperty, "BackgroundBrush"); SetResourceReference(ForegroundProperty, "TextBrush"); SetResourceReference(FontFamilyProperty, "UiFont");
        Grid.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush"); Grid.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        Grid.SetResourceReference(DataGrid.RowStyleProperty, "DarkDataGridRow"); Grid.SetResourceReference(DataGrid.CellStyleProperty, "DarkDataGridCell");
        Grid.SetResourceReference(DataGrid.ColumnHeaderStyleProperty, "ThemedDataGridColumnHeader");
        Grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal; Grid.AlternationCount = 2; Grid.BorderThickness = new Thickness(0);
        Grid.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty, "PaneBorderBrush");
        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = Title, FontSize = 22, Margin = new Thickness(0, 0, 0, 8) });
        header.Children.Add(new TextBlock { Text = string.Format(CultureInfo.CurrentCulture, HomeAssistantText.L("AllSummary"), Rows.Count,
            Rows.Count(row => HomeAssistantAdvisor.Best(row.Advice) != null || HomeAssistantAdvisor.ResearchChoice(row.Advice) != null || HomeAssistantAdvisor.FirmwareResearch(row.Advice))), Margin = new Thickness(0, 0, 0, 8) });
        header.Children.Add(new TextBlock { Text = Rows.Count == 0 ? HomeAssistantText.L("NoDevices") : HomeAssistantText.L("Note"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        Button AddButton(string label, string tag, Action action)
        {
            var button = new Button { Content = label, Tag = tag, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(8, 12, 0, 0) };
            button.SetResourceReference(StyleProperty, "OutlineButton"); button.Click += (_, _) => action(); buttons.Children.Add(button); return button;
        }
        var detail = AddButton(HomeAssistantText.L("Details"), "detail", OpenSelected); detail.IsEnabled = false;
        var copyAll = AddButton(HomeAssistantText.L("Copy"), "copy-all", () => _copy(Describe()));
        AddButton(Application.Current.TryFindResource("Str_Sys_Close") as string ?? "Close", "close", Close);
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        void Column(string title, string path, double width)
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(5, 7, 5, 7)));
            Grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(path), ElementStyle = style,
                Width = new DataGridLength(width), MinWidth = 100 });
        }
        Column(Application.Current.TryFindResource("Str_Col_Ip") as string ?? "IP", "Address", 128);
        Column(Application.Current.TryFindResource("Str_Col_DeviceName") as string ?? "Device", "Name", 145);
        Column(HomeAssistantText.L("BestChoice"), "Choice", 180);
        Column(HomeAssistantText.L("Details"), "Reason", 260);
        Column(HomeAssistantText.L("Requirements"), "Requirements", 350);
        Grid.ItemsSource = Rows;
        Grid.SelectionChanged += (_, _) => detail.IsEnabled = Grid.SelectedItem is Row;
        Grid.MouseDoubleClick += (_, e) => { if (ItemsControl.ContainerFromElement(Grid, e.OriginalSource as DependencyObject) is DataGridRow) OpenSelected(); };
        var scan = new DockPanel();
        var tabs = new TabControl();
        SurveyPanel? survey = null;
        if (_survey != null)
        {
            survey = new SurveyPanel(_survey, Rows.Select(row => row.Device), open, copy);
            var add = SurveyPanel.Button(SurveyText.L("Add"), () =>
            {
                var selected = Grid.SelectedItems.Cast<Row>().Select(row => row.Device).ToArray();
                tabs.SelectedIndex = 1;
                survey.AddSelected(selected);
            }, "survey-add");
            add.IsEnabled = false;
            Grid.SelectionChanged += (_, _) => add.IsEnabled = Grid.SelectedItems.Count > 0;
            DockPanel.SetDock(add, Dock.Bottom);
            scan.Children.Add(add);
        }
        scan.Children.Add(Grid);
        tabs.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush");
        tabs.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        tabs.Items.Add(new TabItem { Header = SurveyText.L("Scan"), Content = scan });
        var unavailable = location?.IsTemporary == true
            ? Application.Current.TryFindResource("Str_Loc_TemporaryNotice") as string ?? SurveyText.L("Error")
            : SurveyText.L("Error");
        tabs.Items.Add(new TabItem
        {
            Header = SurveyText.L("Title") + (location?.Info is { } info ? " · " + info.Name : ""),
            Content = (FrameworkElement?)survey ?? SurveyPanel.Text(unavailable)
        });
        tabs.SelectionChanged += (_, args) =>
        {
            if (!ReferenceEquals(args.Source, tabs)) return;
            detail.IsEnabled = tabs.SelectedIndex == 0 && Grid.SelectedItem is Row;
            copyAll.IsEnabled = tabs.SelectedIndex == 0;
            if (tabs.SelectedIndex == 1) survey?.Reload();
        };
        root.Children.Add(tabs); Content = root;
    }
    internal HomeAssistantAdviceWindow? CreateSelected() => Grid.SelectedItem is Row row
        ? new HomeAssistantAdviceWindow(row.Device, _open, _copy, surveyStore: _survey) : null;
    private void OpenSelected()
    {
        if (CreateSelected() is not { } dialog) return;
        if (PresentationSource.FromVisual(this) != null) dialog.Owner = this;
        dialog.ShowDialog();
    }
    internal string Describe()
    {
        var text = new StringBuilder().AppendLine(HomeAssistantText.L("AllTitle"));
        foreach (var row in Rows) text.AppendLine().AppendLine(HomeAssistantText.Describe(row.Device, row.Advice));
        return text.ToString();
    }
}
