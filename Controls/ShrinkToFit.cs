#nullable enable
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KillerScan.Controls
{
    /// <summary>
    /// Lets a single-line TextBlock draw a little smaller before it trims. Set MinScale (0.85 is
    /// "up to 15% smaller"): when the row is narrower than the text, the font steps down just far
    /// enough to fit, never below MinScale of its own size, and TextTrimming takes over past that.
    /// Widening the row brings the text back to full size.
    /// </summary>
    public static class ShrinkToFit
    {
        public static readonly DependencyProperty MinScaleProperty = DependencyProperty.RegisterAttached(
            "MinScale", typeof(double), typeof(ShrinkToFit), new PropertyMetadata(1.0, OnMinScaleChanged));

        public static double GetMinScale(DependencyObject element) => (double)element.GetValue(MinScaleProperty);
        public static void SetMinScale(DependencyObject element, double value) => element.SetValue(MinScaleProperty, value);

        // The size the text was given, before any shrinking.
        private static readonly DependencyProperty BaseSizeProperty = DependencyProperty.RegisterAttached(
            "BaseSize", typeof(double), typeof(ShrinkToFit), new PropertyMetadata(double.NaN));

        private static readonly DependencyPropertyDescriptor TextDescriptor =
            DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        private static readonly DependencyPropertyDescriptor FamilyDescriptor =
            DependencyPropertyDescriptor.FromProperty(TextBlock.FontFamilyProperty, typeof(TextBlock));

        private static void OnMinScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock text || e.OldValue is double old && old < 1) return;
            text.SizeChanged += (_, _) => Fit(text);
            TextDescriptor.AddValueChanged(text, (_, _) => Fit(text));
            FamilyDescriptor.AddValueChanged(text, (_, _) => Fit(text));
        }

        private static void Fit(TextBlock text)
        {
            double baseSize = (double)text.GetValue(BaseSizeProperty);
            if (double.IsNaN(baseSize))
            {
                baseSize = text.FontSize;
                text.SetValue(BaseSizeProperty, baseSize);
            }
            double width = text.ActualWidth;
            if (width <= 0 || string.IsNullOrEmpty(text.Text)) return;

            double natural = new FormattedText(text.Text, CultureInfo.CurrentCulture, text.FlowDirection,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                baseSize, Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip).WidthIncludingTrailingWhitespace;
            // Half a unit of slack so a shrunk line is not trimmed by its own rounding.
            double scale = natural <= width ? 1 : Math.Max(Math.Min(1, GetMinScale(text)), (width - 0.5) / natural);
            double size = baseSize * scale;
            if (Math.Abs(text.FontSize - size) > 0.01) text.FontSize = size;
        }
    }
}
