using System.Windows;
using System.Windows.Controls;
using KillerScan.Services;

namespace KillerScan.Controls;

internal sealed class SurveyEditorWindow : Window
{
    internal readonly Dictionary<string, TextBox> Inputs = new();
    private SurveyEntry _draft;
    private readonly Action<SurveyEntry> _save;
    private readonly Action<string> _open, _copy;
    internal SurveyEditorWindow(SurveyEntry draft, Action<SurveyEntry> save, Action<string> open, Action<string> copy)
    {
        _draft = draft; _save = save; _open = open; _copy = copy;
        SurveyPanel.Theme(this);
        Title = SurveyText.L("Edit"); Width = 700; Height = 820; MinWidth = 420; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Loaded += (_, _) => KillerFamily.Windowing.WorkAreaFit.Fit(this);
        SetResourceReference(BackgroundProperty, "BackgroundBrush"); SetResourceReference(ForegroundProperty, "TextBrush"); SetResourceReference(FontFamilyProperty, "UiFont");
        var root = new DockPanel { Margin = new Thickness(18) };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(SurveyPanel.Button(SurveyText.L("Save"), () => { if (TrySave()) DialogResult = true; }, "save"));
        var cancel = SurveyPanel.Button(SurveyText.L("Cancel"), Close, "cancel"); cancel.IsCancel = true; buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var fields = new StackPanel();
        fields.Children.Add(SurveyPanel.Text(SurveyText.L("Notice")));
        void Field(string key, string value, string? label = null, bool multiline = false)
        {
            fields.Children.Add(SurveyPanel.Text(label ?? SurveyText.L(key)));
            var input = new TextBox { Text = value, MaxLength = key == "Name" ? 160 : 8000, MinHeight = multiline ? 65 : 30,
                AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            input.SetResourceReference(StyleProperty, "DarkTextBox");
            System.Windows.Automation.AutomationProperties.SetName(input, label ?? SurveyText.L(key));
            Inputs[key] = input; fields.Children.Add(input);
        }
        Field("Name", draft.Name); Field("Room", draft.Room);
        Field("ManufacturerModel", draft.ManufacturerModel, HomeAssistantText.L("Identity"));
        Field("Firmware", draft.Firmware); Field("Connection", draft.Connection);
        Field("Functions", draft.DesiredFunctions, multiline: true); Field("Notes", draft.Notes, multiline: true);
        var hints = SurveyPanel.Text("");
        void UpdateHints() => hints.Text = HomeAssistantText.L("Protocol") + ": " +
            (HomeAssistantAdvisor.Integration(_draft.Protocol)?.Name ?? Application.Current.TryFindResource("Str_Access_Unknown") as string ?? "Unknown") + " · " + HomeAssistantText.L("Goal") + ": " +
            HomeAssistantText.L(_draft.Goal == "Control" ? "ControlGoal" : _draft.Goal == "Media" ? "MediaGoal" : "Monitor");
        fields.Children.Add(hints); UpdateHints();
        fields.Children.Add(SurveyPanel.Button(HomeAssistantText.L("Title"), () =>
        {
            var dialog = CreateAdvice();
            dialog.Owner = this; dialog.ShowDialog();
            UpdateHints();
        }, "advice"));
        fields.Children.Add(SurveyPanel.Text(SurveyPanel.ObservationText(draft)));
        root.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
    }
    internal HomeAssistantAdviceWindow CreateAdvice()
    {
        _draft = Draft();
        return new HomeAssistantAdviceWindow(SurveyStore.AdviceDevice(_draft), _open, _copy, _draft.Context, context =>
        {
            _draft = _draft with { ManufacturerModel = context.ManufacturerModel, Protocol = context.Protocol, Goal = context.Goal };
            Inputs["ManufacturerModel"].Text = context.ManufacturerModel;
        }, SurveyPanel.ObservationText(_draft));
    }
    internal SurveyEntry Draft() => _draft with { Name = Inputs["Name"].Text.Trim(), Room = Inputs["Room"].Text,
        ManufacturerModel = Inputs["ManufacturerModel"].Text, Firmware = Inputs["Firmware"].Text,
        Connection = Inputs["Connection"].Text, DesiredFunctions = Inputs["Functions"].Text, Notes = Inputs["Notes"].Text };
    internal bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(Inputs["Name"].Text)) { MessageBox.Show(this, SurveyText.L("Required"), Title); return false; }
        try { _save(Draft()); return true; }
        catch (OperationCanceledException) { return false; }
        catch (Exception) { MessageBox.Show(this, SurveyText.L("Error"), Title, MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
}
