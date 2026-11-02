using System.IO;
namespace KillerScan.Services;
internal static class DevelopmentData
{
    internal static string Root => Environment.GetEnvironmentVariable("KILLERSCAN_DATA_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KillerScan");
    internal static void WriteAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(pending, text); if (File.Exists(path)) File.Replace(pending, path, path + ".bak"); else File.Move(pending, path); }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
}
