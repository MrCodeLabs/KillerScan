using System.Windows;
using System.Windows.Controls;
using KillerScan.Services;
namespace KillerScan.Controls;
public partial class ScanWorkspace
{
    private void HomeAssistantAll_Click(object sender, RoutedEventArgs e) { var window = CreateHomeAssistantOverview(); window.Owner = Window.GetWindow(this); window.ShowDialog(); }
    internal HomeAssistantOverviewWindow CreateHomeAssistantOverview() => new(ActiveDevices, WebLink.Open, CopyDeviceText);
    internal HomeAssistantAdviceWindow CreateHomeAssistantAdvice(NetworkDevice? device = null) => new(device ?? GetSelectedDevice() ?? new NetworkDevice(), WebLink.Open, CopyDeviceText);
    private void HomeAssistant_Click(object sender, RoutedEventArgs e) { var window = CreateHomeAssistantAdvice(); window.Owner = Window.GetWindow(this); window.ShowDialog(); }
    private void HomeAssistantRow_Click(object sender, RoutedEventArgs e)
    { if ((sender as FrameworkElement)?.DataContext is NetworkDevice device) { var window = CreateHomeAssistantAdvice(device); window.Owner = Window.GetWindow(this); window.ShowDialog(); } }
}
