#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace KillerScan.Controls
{
    /// <summary>
    /// Lets a single-line TextBlock draw a little smaller before it trims. Set MinScale (0.85 is
    /// "up to 15% smaller"): when the row is narrower than the text, the font steps down just far
    /// enough to fit, never below MinScale of its own size, and TextTrimming takes over past that.
    /// Widening the row brings the text back to full size.
    /// </summary>
    /// <remarks>
    /// Text and font changes are watched through bindings on the element itself, never through
    /// DependencyPropertyDescriptor, whose static table would keep every discarded list row alive.
    /// The shrunk size is a current value, so the declared FontSize and any style stay underneath.
    /// </remarks>
    public static class ShrinkToFit
    {
        public static readonly DependencyProperty MinScaleProperty = DependencyProperty.RegisterAttached(
            "MinScale", typeof(double), typeof(ShrinkToFit), new PropertyMetadata(1.0, OnMinScaleChanged));

        public static double GetMinScale(DependencyObject element) => (double)element.GetValue(MinScaleProperty);
        public static void SetMinScale(DependencyObject element, double value) => element.SetValue(MinScaleProperty, value);

        /// <summary>The size the text was given before any shrinking.</summary>
        public static double BaseSize(TextBlock text)
        {
            double stored = (double)text.GetValue(BaseSizeProperty);
            return double.IsNaN(stored) ? text.FontSize : stored;
        }

        private static readonly DependencyProperty BaseSizeProperty = DependencyProperty.RegisterAttached(
            "BaseSize", typeof(double), typeof(ShrinkToFit), new PropertyMetadata(double.NaN));

        private static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached(
            "Attached", typeof(bool), typeof(ShrinkToFit), new PropertyMetadata(false));

        // Bound to the element's own Text and FontFamily, so a change refits without any static hook.
        private static readonly DependencyProperty TextWatchProperty = DependencyProperty.RegisterAttached(
            "TextWatch", typeof(object), typeof(ShrinkToFit), new PropertyMetadata(null, OnWatchedChanged));
        private static readonly DependencyProperty FamilyWatchProperty = DependencyProperty.RegisterAttached(
            "FamilyWatch", typeof(object), typeof(ShrinkToFit), new PropertyMetadata(null, OnWatchedChanged));

        private static void OnMinScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock text) return;
            if (!(bool)text.GetValue(AttachedProperty))
            {
                text.SetValue(AttachedProperty, true);
                text.SizeChanged += (_, _) => Fit(text);
                BindingOperations.SetBinding(text, TextWatchProperty,
                    new Binding(nameof(TextBlock.Text)) { Source = text, Mode = BindingMode.OneWay });
                BindingOperations.SetBinding(text, FamilyWatchProperty,
                    new Binding(nameof(TextBlock.FontFamily)) { Source = text, Mode = BindingMode.OneWay });
            }
            Fit(text);
        }

        private static void OnWatchedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock text) Fit(text);
        }

        private static void Fit(TextBlock text)
        {
            if (double.IsNaN((double)text.GetValue(BaseSizeProperty)))
                text.SetValue(BaseSizeProperty, text.FontSize);
            double baseSize = BaseSize(text);
            double width = text.ActualWidth;
            double scale = 1;
            if (width > 0 && !string.IsNullOrEmpty(text.Text))
            {
                double natural = new FormattedText(text.Text, CultureInfo.CurrentCulture, text.FlowDirection,
                    new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                    baseSize, Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip).WidthIncludingTrailingWhitespace;
                // Half a unit of slack so a shrunk line is not trimmed by its own rounding.
                if (natural > width) scale = Math.Max(Math.Min(1, GetMinScale(text)), (width - 0.5) / natural);
            }
            double size = baseSize * scale;
            if (Math.Abs(text.FontSize - size) > 0.01) text.SetCurrentValue(TextBlock.FontSizeProperty, size);
        }
    }
}
