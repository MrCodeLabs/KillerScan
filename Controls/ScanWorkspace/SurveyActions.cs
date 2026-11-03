using System.Windows;
using KillerScan.Services;

namespace KillerScan.Controls;

public partial class ScanWorkspace
{
    private void RefreshSurvey(LocationData location, IEnumerable<NetworkDevice> observed)
    {
        if (_disposed) return;
        try { location.Survey?.Refresh(observed); }
        catch (Exception)
        {
            // A survey write error must not turn a completed network scan into a failed scan.
            if (!_disposed) MessageBox.Show(Window.GetWindow(this), SurveyText.L("Error"), SurveyText.L("Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
