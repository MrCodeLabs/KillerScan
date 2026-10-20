using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KillerScan.Controls
{
    public partial class ConfirmDialog : Window
    {
        public bool Confirmed { get; private set; }

        /// <summary>True when the user ticked "Install for all users". Always false for the
        /// reusable variant below, which hides the checkbox.</summary>
        public bool AllUsers => AllUsersCheck.IsChecked == true;

        /// <summary>The reused dialog's optional checkbox, read back after ShowDialog returns.</summary>
        public bool Check1Checked => Check1.IsChecked == true;

        public ConfirmDialog()
        {
            InitializeComponent();
            var titleFrames = BitmapDecoder.Create(new System.Uri("pack://application:,,,/Resources/ks-icon.ico"),
                BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames;
            TitleIcon.Source = titleFrames[0];
            TaskbarIdentity.Track(this);

            // Already installed machine-wide (by an admin, winget, choco or an RMM)? Then the
            // only sane move is to update that copy in place: tick the box and lock it. Offering
            // a per-user install here would either leave two copies on the PC, or let one user
            // delete the app for every other account on a shared machine.
            if (App.MachineInstallExists())
            {
                AllUsersCheck.IsChecked = true;
                AllUsersCheck.IsEnabled = false;
                AllUsersNote.Text = Loc("Str_Install_AlreadyAllUsers");
            }

            Loaded += (_, _) => Anim.FadeIn(RootBorder);
            Loaded += (_, _) => ApplyNotice();
            SourceInitialized += (_, _) =>
            {
                if (Owner == null)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    ShowInTaskbar = true;
                }
            };
        }

        // Configurable variant for reusing the themed dialog beyond the install prompt
        // (e.g. the self-update confirmation). detail may contain newlines for multiple lines.
        // cancelText has no English default any more: a defaulted literal is how the self-update
        // dialog kept an English Cancel button next to three translated ones. Callers pass
        // Loc("Str_Btn_Cancel"), which is what the default was trying to be.
        public ConfirmDialog(string heading, string detail, string confirmText, string cancelText)
            : this()
        {
            HeadingText.Text = heading;
            DetailText.Text = detail;
            DetailText.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
            OkButton.Content = confirmText;
            CancelButton.Content = cancelText;
            CancelButton.Visibility = string.IsNullOrEmpty(cancelText) ? Visibility.Collapsed : Visibility.Visible;
            // The all-users choice belongs to the install prompt only, not to reused dialogs
            // like the self-update confirmation.
            AllUsersCheck.Visibility = Visibility.Collapsed;
            AllUsersNote.Visibility = Visibility.Collapsed;
        }

        // The reusable variant with one checkbox under the detail, such as "Always check on startup".
        public ConfirmDialog(string heading, string detail, string confirmText, string cancelText,
                             string check1Label, bool check1Initial)
            : this(heading, detail, confirmText, cancelText)
        {
            Check1.Content    = new System.Windows.Controls.TextBlock { Text = check1Label, TextWrapping = TextWrapping.Wrap };
            Check1.IsChecked  = check1Initial;
            Check1.Visibility = Visibility.Visible;
        }

        // The family setup dialog's notice ring: an amber "!" on a confirmation and the accent "i" on a
        // notice with a single button. The amber darkens on a light card so it keeps its contrast.
        private void ApplyNotice()
        {
            FitHeading();
            bool confirmation = CancelButton.Visibility == Visibility.Visible;
            System.Windows.Media.Brush brush;
            if (confirmation)
            {
                bool light = TryFindResource("BackgroundBrush") is System.Windows.Media.SolidColorBrush card && card.Color.R * 0.299 + card.Color.G * 0.587 + card.Color.B * 0.114 > 140;
                brush = new System.Windows.Media.SolidColorBrush(light
                    ? System.Windows.Media.Color.FromRgb(176, 112, 0)
                    : System.Windows.Media.Color.FromRgb(255, 190, 80));
            }
            else
            {
                brush = TryFindResource("PrimaryBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gray;
            }
            NoticeGlyph.Text = confirmation ? "!" : "i";
            NoticeGlyph.Foreground = brush;
            NoticeRing.BorderBrush = brush;
        }

        // A heading is one short line. Text after its first line break moves into the detail, and
        // a first line too long for a heading stays body-sized instead of shouting.
        private void FitHeading()
        {
            string text = HeadingText.Text ?? "";
            int lineBreak = text.IndexOf('\n');
            if (lineBreak >= 0)
            {
                string rest = text[(lineBreak + 1)..].Trim();
                HeadingText.Text = text[..lineBreak].TrimEnd('\r');
                if (rest.Length > 0)
                {
                    string existing = DetailText.Visibility == Visibility.Visible ? DetailText.Text : "";
                    DetailText.Text = string.IsNullOrEmpty(existing) ? rest : rest + "\n\n" + existing;
                    DetailText.Visibility = Visibility.Visible;
                }
            }
            if ((HeadingText.Text ?? "").Length > 90)
            {
                HeadingText.ClearValue(System.Windows.Controls.TextBlock.FontFamilyProperty);
                HeadingText.ClearValue(System.Windows.Media.TextOptions.TextFormattingModeProperty);
                HeadingText.FontSize = 13;
            }
        }

        internal static void ShowNotice(string message, Window? owner = null)
        {
            var dialog = new ConfirmDialog(message, string.Empty, Loc("Str_Sys_Close"), string.Empty);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }

        // Ticking the box changes where the app lands, so the heading follows. The note under it
        // stays put either way - it describes what ticking would do, which is worth reading
        // before you tick, and a note that appears on tick makes the dialog jump.
        private void AllUsers_Changed(object sender, RoutedEventArgs e)
            => HeadingText.Text = Loc(AllUsers ? "Str_Install_HeadAll" : "Str_Install_HeadUser");

        private static string Loc(string key) =>
            Application.Current.TryFindResource(key) as string ?? key;

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();
    }
}
