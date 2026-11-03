using System.Windows;

namespace KillerScan.Services;

internal static class SurveyText
{
    internal static readonly string[] Keys = ["Title", "Scan", "Add", "Manual", "Edit", "Remove", "ConfirmRemove", "Name", "Room", "Firmware", "Connection", "Functions", "Notes", "Save", "Cancel", "Required", "Notice", "Observation", "NoObservation", "Error", "Link", "ConfirmLink", "New", "IpWarning", "Hints"];
    internal static string L(string key) => Application.Current?.TryFindResource("Str_Survey_" + key) as string ?? key;
}
