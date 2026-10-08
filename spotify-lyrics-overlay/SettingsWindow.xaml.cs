using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using spotify_lyrics_overlay.Transitions;
using Brush = System.Windows.Media.Brush;
using Color = System.Drawing.Color;
using GdiFontFamily = System.Drawing.FontFamily;
using GdiFontStyle = System.Drawing.FontStyle;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace spotify_lyrics_overlay
{
    //main settings window, every change goes straight into the config so the overlay updates live
    public partial class SettingsWindow : Window
    {
        private static readonly Brush StartBrush = frozenBrush(0x1E, 0x8E, 0x3E);
        private static readonly Brush StopBrush = frozenBrush(0xC4, 0x2B, 0x1C);
        private const string DefaultFontColor = "#F3CE32";

        private bool isStarted = false;
        private bool isInitializing = true;
        private LyricsOverlay? overlay;
        private Color fontColor;
        private Color backgroundColor = Color.Black;

        private record MonitorItem(Screen Screen, string Label);

        //Name is stored in the config, empty for no secondary font
        private record FontChoice(string Name, string Label, string Preview);

        public SettingsWindow()
        {
            InitializeComponent();

            var config = ConfigManager.Instance.LoadConfig();

            fontComboBox.ItemsSource = GdiFontFamily.Families;
            secondaryFontComboBox.ItemsSource = GdiFontFamily.Families
                .Select(f => new FontChoice(f.Name, f.Name, f.Name))
                .Prepend(new FontChoice("", "None", "Segoe UI"))
                .ToList();
            transitionComboBox.ItemsSource = LyricsTransitions.All.Select(mode => mode.DisplayName).ToList();
            monitorComboBox.ItemsSource = Screen.AllScreens
                .Select((screen, i) => new MonitorItem(screen,
                    $"Display {i + 1} — {screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? " (primary)" : "")}"))
                .ToList();

            if (config.newlyGenerated)
            {
                selectFont("arial");
                selectSecondaryFont("");
                fontSizeBox.Value = 27;
                selectMonitor(Screen.PrimaryScreen?.DeviceName);
                setFontColor(ColorHelper.FromHex(DefaultFontColor, Color.Gold));
                albumColorCheckBox.IsChecked = true;
                setBackgroundColor(Color.Black);
                backgroundOpacitySlider.Value = 70;
                backgroundSpreadBox.Value = 12;
                transitionComboBox.SelectedIndex = LyricsTransitions.IndexOf(LyricsTransitions.DefaultId);
                debugCacheHitsCheckBox.IsChecked = true;
            }
            else
            {
                selectFont(config.fontName);
                selectSecondaryFont(config.secondaryFontName);
                fontSizeBox.Value = config.fontSize;

                boldCheckBox.IsChecked = config.bold;
                italicCheckBox.IsChecked = config.italic;
                dropShadowCheckBox.IsChecked = config.dropShadow;

                selectMonitor(config.screenName);
                xOffsetBox.Value = config.xOffset;
                yOffsetBox.Value = config.yOffset;

                setFontColor(ColorHelper.FromHex(config.fontColorHex, ColorHelper.FromHex(DefaultFontColor, Color.Gold)));
                albumColorCheckBox.IsChecked = config.albumColor;

                clientIdBox.Password = config.apiKey;
                rememberClientIdCheckBox.IsChecked = config.rememberApiKey;

                backgroundCheckBox.IsChecked = config.backgroundEnabled;
                setBackgroundColor(ColorHelper.FromHex(config.backgroundColorHex, backgroundColor));
                backgroundOpacitySlider.Value = Math.Clamp(config.backgroundOpacity, 0, 100);
                backgroundSpreadBox.Value = config.backgroundSpread;

                transitionComboBox.SelectedIndex = LyricsTransitions.IndexOf(config.transitionMode);

                debugCheckBox.IsChecked = config.debugEnabled;
                debugCacheHitsCheckBox.IsChecked = config.debugShowCacheHits;
            }

            showRunState();

            isInitializing = false;
            updateConfig();
        }

        private static Brush frozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static Brush toBrush(Color color) => frozenBrush(color.R, color.G, color.B);

        private void selectFont(string name)
        {
            var font = ((GdiFontFamily[])fontComboBox.ItemsSource).FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (font != null)
                fontComboBox.SelectedItem = font;
        }

        private void selectSecondaryFont(string name)
        {
            var fonts = (List<FontChoice>)secondaryFontComboBox.ItemsSource;
            secondaryFontComboBox.SelectedIndex = Math.Max(0, fonts.FindIndex(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }

        private void selectMonitor(string? deviceName)
        {
            var monitors = (List<MonitorItem>)monitorComboBox.ItemsSource;
            int index = monitors.FindIndex(m => m.Screen.DeviceName == deviceName);
            monitorComboBox.SelectedIndex = index != -1 ? index : monitors.FindIndex(m => m.Screen.Primary);
        }

        //offsets are measured from the center, so half the screen size each way
        private void applyOffsetLimits()
        {
            if (monitorComboBox.SelectedItem is not MonitorItem monitor) return;

            var bounds = monitor.Screen.Bounds;
            xOffsetBox.SetRange(-bounds.Width / 2, bounds.Width / 2);
            yOffsetBox.SetRange(-bounds.Height / 2, bounds.Height / 2);
        }

        private void setting_Changed(object sender, EventArgs e) => updateConfig();

        private void fontComboBox_SelectionChanged(object sender, EventArgs e)
        {
            if (fontComboBox.SelectedItem is GdiFontFamily font)
            {
                boldCheckBox.IsEnabled = font.IsStyleAvailable(GdiFontStyle.Bold);
                italicCheckBox.IsEnabled = font.IsStyleAvailable(GdiFontStyle.Italic);
            }
            updateConfig();
        }

        private void monitorComboBox_SelectionChanged(object sender, EventArgs e)
        {
            applyOffsetLimits();
            updateConfig();
        }

        private void backgroundOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (backgroundOpacityText == null) return;
            backgroundOpacityText.Text = $"{(int)backgroundOpacitySlider.Value}%";
            updateConfig();
        }

        private void setFontColor(Color color)
        {
            fontColor = color;
            colorSwatch.Background = toBrush(color);
            colorHexBox.Text = ColorHelper.ToHex(color);
            updateConfig();
        }

        private void setBackgroundColor(Color color)
        {
            backgroundColor = color;
            backgroundSwatch.Background = toBrush(color);
            backgroundHexBox.Text = ColorHelper.ToHex(color);
            updateConfig();
        }

        //an invalid hex code puts the previous color back
        private void colorHexBox_Commit(object sender, EventArgs e) => setFontColor(ColorHelper.FromHex(colorHexBox.Text, fontColor));

        private void backgroundHexBox_Commit(object sender, EventArgs e) => setBackgroundColor(ColorHelper.FromHex(backgroundHexBox.Text, backgroundColor));

        private void colorHexBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) colorHexBox_Commit(sender, e);
        }

        private void backgroundHexBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) backgroundHexBox_Commit(sender, e);
        }

        private void colorPicker_Click(object sender, EventArgs e) => pickColor(fontColor, setFontColor);

        private void backgroundPicker_Click(object sender, EventArgs e) => pickColor(backgroundColor, setBackgroundColor);

        //apply is called while picking so the overlay previews the color, cancel puts the old one back
        private void pickColor(Color current, Action<Color> apply)
        {
            var picker = new ColorPickerWindow(current) { Owner = this };
            picker.ColorChanged += apply;
            if (picker.ShowDialog() != true)
            {
                apply(current);
            }
        }

        private void updateConfig()
        {
            if (isInitializing) return;

            var config = ConfigManager.Instance.LoadConfig();

            config.newlyGenerated = false;

            if (fontComboBox.SelectedItem is GdiFontFamily font)
            {
                config.fontName = font.Name;
            }
            if (secondaryFontComboBox.SelectedItem is FontChoice secondaryFont)
            {
                config.secondaryFontName = secondaryFont.Name;
            }
            config.fontSize = fontSizeBox.Value;

            config.bold = boldCheckBox.IsChecked == true;
            config.italic = italicCheckBox.IsChecked == true;
            config.dropShadow = dropShadowCheckBox.IsChecked == true;

            if (monitorComboBox.SelectedItem is MonitorItem monitor)
            {
                config.screenName = monitor.Screen.DeviceName;
            }
            config.xOffset = xOffsetBox.Value;
            config.yOffset = yOffsetBox.Value;

            config.fontColorHex = ColorHelper.ToHex(fontColor);
            config.albumColor = albumColorCheckBox.IsChecked == true;

            config.rememberApiKey = rememberClientIdCheckBox.IsChecked == true;
            config.apiKey = config.rememberApiKey ? clientIdBox.Password : "";

            config.backgroundEnabled = backgroundCheckBox.IsChecked == true;
            config.backgroundColorHex = ColorHelper.ToHex(backgroundColor);
            config.backgroundOpacity = (int)backgroundOpacitySlider.Value;
            config.backgroundSpread = backgroundSpreadBox.Value;

            if (transitionComboBox.SelectedIndex >= 0)
            {
                config.transitionMode = LyricsTransitions.All[transitionComboBox.SelectedIndex].Id;
            }

            config.debugEnabled = debugCheckBox.IsChecked == true;
            config.debugShowCacheHits = debugCacheHitsCheckBox.IsChecked == true;
        }

        private void runButton_Click(object sender, RoutedEventArgs e)
        {
            isStarted = !isStarted;
            showRunState();

            if (isStarted && overlay == null)
            {
                overlay = new LyricsOverlay(() => isStarted);
                overlay.Show();
            }
        }

        private void showRunState()
        {
            runButton.Content = isStarted ? "Stop" : "Start";
            runButton.Background = isStarted ? StopBrush : StartBrush;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            updateConfig();
            ConfigManager.Instance.SaveConfig();
        }
    }
}
