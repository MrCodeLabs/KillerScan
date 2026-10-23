using System;
using System.IO;
using System.Threading.Tasks;
using KillerScan.Services;

internal static class CommandLinePathTests
{
    public static Task Run()
    {
        string install = Path.Combine(Path.GetTempPath(), "KillerScan-PathTest");
        string previous = Environment.CurrentDirectory;
        Directory.CreateDirectory(install);
        try
        {
            // The uninstaller can run from the install folder. A relative entry that happens to
            // resolve there belongs to the user, not to KillerScan.
            Environment.CurrentDirectory = install;
            Require(CommandLinePath.WithoutEntry(@".;C:\Tools", install) == @".;C:\Tools",
                "Removing the install folder keeps relative PATH entries.");
            Require(CommandLinePath.WithEntry(".", install) == ".;" + install,
                "A relative entry does not count as the install folder when adding it.");

            Require(CommandLinePath.WithEntry(@"%SystemRoot%\system32", install) == @"%SystemRoot%\system32;" + install,
                "Adding keeps unexpanded entries as written.");
            Require(CommandLinePath.WithEntry(@"C:\Tools;" + install.ToUpperInvariant() + @"\", install) == @"C:\Tools;" + install.ToUpperInvariant() + @"\",
                "Adding is skipped when another spelling of the folder is present.");
            Require(CommandLinePath.WithoutEntry(@"%SystemRoot%;""" + install + @""";C:\Tools", install) == @"%SystemRoot%;C:\Tools",
                "Removing takes out quoted spellings and keeps other entries in order.");
            Require(CommandLinePath.WithoutEntry(@"C:\Tools", install) == @"C:\Tools",
                "Removing leaves PATH untouched when the folder is absent.");
        }
        finally
        {
            Environment.CurrentDirectory = previous;
            Directory.Delete(install);
        }
        return Task.CompletedTask;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
