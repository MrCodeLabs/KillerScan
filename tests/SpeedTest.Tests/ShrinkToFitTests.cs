using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KillerScan.Controls;

/// <summary>
/// A sidebar row narrower than its text first draws the text a little smaller, down to the
/// shrink limit, and only then trims it.
/// </summary>
internal static class ShrinkToFitTests
{
    public static Task Run()
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { Check(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
        return Task.CompletedTask;
    }

    private static void Check()
    {
        const string text = "12/28/2026 10:58 PM";
        double natural = Natural(text, 13);

        var roomy = Row(text, natural + 10);
        Require(roomy.FontSize == 13, "Text that fits keeps its size.");

        var tight = Row(text, natural * 0.93);
        Require(tight.FontSize < 13 && tight.FontSize >= 13 * 0.85 - 0.01, "Text slightly too wide shrinks instead of trimming.");
        Require(Natural(text, tight.FontSize) <= tight.ActualWidth + 0.5, "Shrunk text fits its row in full.");

        var narrow = Row(text, natural * 0.6);
        Require(Math.Abs(narrow.FontSize - 13 * 0.85) < 0.01, "Text never shrinks past 85% of its size.");
        Require(Natural(text, narrow.FontSize) > narrow.ActualWidth, "Past the shrink limit the text is trimmed.");

        // Widening again restores the full size.
        var host = (Border)narrow.Parent;
        host.Width = natural + 10;
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();
        Require(narrow.FontSize == 13, "Room to spare returns the text to its full size.");

        // The unshrunk size stays readable, so widths are never worked out from shrunk text.
        var shrunk = Row(text, natural * 0.6);
        Require(ShrinkToFit.BaseSize(shrunk) == 13 && shrunk.FontSize < 13, "The base size survives shrinking.");

        // A shrunk size never outranks a later style: it is a current value, not a local one.
        var source = DependencyPropertyHelper.GetValueSource(shrunk, TextBlock.FontSizeProperty);
        Require(source.IsCurrent && source.BaseValueSource == BaseValueSource.Local,
            "Shrinking leaves the declared font size in place underneath.");

        // A discarded row must be collectable: nothing static may keep it alive.
        var weak = Discarded(text, natural * 0.6);
        for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        Require(!weak.IsAlive, "A discarded row is released, not held by the shrink behavior.");

        // Setting the limit twice attaches once: one layout pass still yields one fit.
        var twice = Row(text, natural * 0.93);
        ShrinkToFit.SetMinScale(twice, 1.0);
        ShrinkToFit.SetMinScale(twice, 0.85);
        Require(twice.FontSize < 13, "Re-enabling the limit still shrinks.");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference Discarded(string text, double width)
    {
        var block = Row(text, width);
        ((Border)block.Parent).Child = null;
        return new WeakReference(block);
    }

    private static TextBlock Row(string text, double width)
    {
        var block = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        ShrinkToFit.SetMinScale(block, 0.85);
        var host = new Border { Width = width, Child = block };
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();
        return block;
    }

    // Same pixels-per-DIP as the behavior, so both sides measure the same way on a scaled screen.
    private static double Natural(string text, double size) =>
        new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            size, Brushes.Black, VisualTreeHelper.GetDpi(new TextBlock()).PixelsPerDip).WidthIncludingTrailingWhitespace;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
