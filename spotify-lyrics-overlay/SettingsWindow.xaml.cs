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
        // checked every few seconds, only a change starts or stops the overlay so Start / Stop by hand still wins
        private bool spotifyRunning = isSpotifyRunning();
        private readonly System.Windows.Threading.DispatcherTimer spotifyTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        private bool isInitializing = true;
        private LyricsOverlay? overlay;
        private readonly TrayIcon tray = new();
        // closing the window only hides it to the tray until Exit is picked there
        private bool exiting;
        // clicking the tray icon takes the focus away from the window before the click arrives
        private DateTime lastDeactivated = DateTime.MinValue;
        private Color fontColor;
        private Color backgroundColor = Color.Black;

        //FollowsPrimary is the entry that moves with the Windows main display, Screen is the current one
        private record MonitorItem(Screen Screen, string Label, bool FollowsPrimary = false);

        //Name is stored in the config, empty for no secondary font
        private record FontChoice(string Name, string Label, string Preview);

        public SettingsWindow()
        {
            InitializeComponent();

            tray.OpenRequested += ShowFromTray;
            tray.Clicked += toggleFromTray;
            Deactivated += (s, e) => lastDeactivated = DateTime.UtcNow;
            tray.RunToggled += () => setStarted(!isStarted);
            tray.ExitRequested += exitApp;
            System.Windows.Application.Current.SessionEnding += (s, e) => exiting = true;

            var config = ConfigManager.Instance.LoadConfig();

            fontComboBox.ItemsSource = GdiFontFamily.Families;
            secondaryFontComboBox.ItemsSource = GdiFontFamily.Families
                .Select(f => new FontChoice(f.Name, f.Name, f.Name))
                .Prepend(new FontChoice("", "None", "Segoe UI"))
                .ToList();
            transitionComboBox.ItemsSource = LyricsTransitions.All.Select(mode => mode.DisplayName).ToList();
            monitorComboBox.ItemsSource = monitorItems();

            if (config.newlyGenerated)
            {
                selectFont("arial");
                selectSecondaryFont("");
                fontSizeBox.Value = 27;
                selectMonitor(null, followPrimary: true);
                setFontColor(ColorHelper.FromHex(DefaultFontColor, Color.Gold));
                albumColorCheckBox.IsChecked = true;
                setBackgroundColor(Color.Black);
                backgroundOpacitySlider.Value = 70;
                backgroundSpreadBox.Value = 12;
                transitionComboBox.SelectedIndex = LyricsTransitions.IndexOf(LyricsTransitions.DefaultId);
                debugCacheHitsCheckBox.IsChecked = true;
                songPillCheckBox.IsChecked = true;
                followSpotifyCheckBox.IsChecked = true;
            }
            else
            {
                selectFont(config.fontName);
                selectSecondaryFont(config.secondaryFontName);
                fontSizeBox.Value = config.fontSize;

                boldCheckBox.IsChecked = config.bold;
                italicCheckBox.IsChecked = config.italic;
                dropShadowCheckBox.IsChecked = config.dropShadow;

                selectMonitor(config.screenName, config.followPrimaryScreen);
                xOffsetBox.Value = config.xOffset;
                yOffsetBox.Value = config.yOffset;

                setFontColor(ColorHelper.FromHex(config.fontColorHex, ColorHelper.FromHex(DefaultFontColor, Color.Gold)));
                albumColorCheckBox.IsChecked = config.albumColor;

                clientIdBox.Password = config.apiKey;
                rememberClientIdCheckBox.IsChecked = config.rememberApiKey;
                rememberRunStateCheckBox.IsChecked = config.rememberRunState;
                followSpotifyCheckBox.IsChecked = config.followSpotify;

                backgroundCheckBox.IsChecked = config.backgroundEnabled;
                setBackgroundColor(ColorHelper.FromHex(config.backgroundColorHex, backgroundColor));
                backgroundOpacitySlider.Value = Math.Clamp(config.backgroundOpacity, 0, 100);
                backgroundSpreadBox.Value = config.backgroundSpread;

                transitionComboBox.SelectedIndex = LyricsTransitions.IndexOf(config.transitionMode);
                songPillCheckBox.IsChecked = config.songPill;
                songPillWindowsMediaCheckBox.IsChecked = config.songPillWindowsMedia;

                debugCheckBox.IsChecked = config.debugEnabled;
                debugCacheHitsCheckBox.IsChecked = config.debugShowCacheHits;
            }

            startWithWindowsCheckBox.IsChecked = WindowsStartup.IsEnabled;

            showRunState();

            // the overlay needs the Client ID to log in, so it's only started when one is remembered
            bool autoStart = !config.newlyGenerated && clientIdBox.Password.Length > 0
                && (config.followSpotify ? spotifyRunning : config.rememberRunState && config.wasStarted);

            isInitializing = false;
            updateConfig();

            // the window may stay hidden in the tray, so don't wait for it to load
            if (autoStart) Dispatcher.BeginInvoke(() => setStarted(true));

            spotifyTimer.Tick += (s, e) => checkSpotify();
            spotifyTimer.Start();

            // monitors plugged in or out, or another one made the main display
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += onDisplaySettingsChanged;
            Closed += (s, e) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= onDisplaySettingsChanged;
        }

        private static List<MonitorItem> monitorItems()
        {
            var items = Screen.AllScreens
                .Select((screen, i) => new MonitorItem(screen,
                    $"Display {i + 1} — {screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? " (primary)" : "")}"))
                .ToList();
            var primary = Screen.PrimaryScreen ?? items[0].Screen;
            items.Insert(0, new MonitorItem(primary,
                $"Main display — {primary.Bounds.Width} × {primary.Bounds.Height} (follows Windows)", FollowsPrimary: true));
            return items;
        }

        //raised on a system thread
        private void onDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(refreshMonitors);

        //list the monitors again and keep the choice, the font size and offsets follow the new screen size
        private void refreshMonitors()
        {
            var selected = monitorComboBox.SelectedItem as MonitorItem;
            bool wasInitializing = isInitializing;
            isInitializing = true;
            monitorComboBox.ItemsSource = monitorItems();
            isInitializing = wasInitializing;
            selectMonitor(selected?.Screen.DeviceName, selected?.FollowsPrimary ?? true);
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

        //a monitor that is gone falls back to the main display
        private void selectMonitor(string? deviceName, bool followPrimary)
        {
            var monitors = (List<MonitorItem>)monitorComboBox.ItemsSource;
            int index = followPrimary ? -1 : monitors.FindIndex(m => !m.FollowsPrimary && m.Screen.DeviceName == deviceName);
            monitorComboBox.SelectedIndex = Math.Max(0, index);
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
            // the boxes change one by one, don't save the half switched values under the new screen size
            bool wasInitializing = isInitializing;
            isInitializing = true;
            applyOffsetLimits();
            restoreScreenLayout();
            isInitializing = wasInitializing;
            updateConfig();
        }

        private static string screenKey(Screen screen) => $"{screen.Bounds.Width}x{screen.Bounds.Height}";

        //puts back the font size and offsets last used on a screen of this size
        private void restoreScreenLayout()
        {
            if (monitorComboBox.SelectedItem is not MonitorItem monitor) return;
            if (!ConfigManager.Instance.LoadConfig().screenLayouts.TryGetValue(screenKey(monitor.Screen), out var layout)) return;

            fontSizeBox.Value = layout.fontSize;
            xOffsetBox.Value = layout.xOffset;
            yOffsetBox.Value = layout.yOffset;
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
                config.followPrimaryScreen = monitor.FollowsPrimary;
            }
            config.xOffset = xOffsetBox.Value;
            config.yOffset = yOffsetBox.Value;
            if (monitorComboBox.SelectedItem is MonitorItem layoutMonitor)
            {
                config.screenLayouts[screenKey(layoutMonitor.Screen)] = new ScreenLayout(config.fontSize, config.xOffset, config.yOffset);
            }

            config.fontColorHex = ColorHelper.ToHex(fontColor);
            config.albumColor = albumColorCheckBox.IsChecked == true;

            config.rememberApiKey = rememberClientIdCheckBox.IsChecked == true;
            config.apiKey = config.rememberApiKey ? clientIdBox.Password : "";
            config.rememberRunState = rememberRunStateCheckBox.IsChecked == true;
            config.wasStarted = isStarted;
            config.followSpotify = followSpotifyCheckBox.IsChecked == true;

            config.backgroundEnabled = backgroundCheckBox.IsChecked == true;
            config.backgroundColorHex = ColorHelper.ToHex(backgroundColor);
            config.backgroundOpacity = (int)backgroundOpacitySlider.Value;
            config.backgroundSpread = backgroundSpreadBox.Value;

            if (transitionComboBox.SelectedIndex >= 0)
            {
                config.transitionMode = LyricsTransitions.All[transitionComboBox.SelectedIndex].Id;
            }

            config.songPill = songPillCheckBox.IsChecked == true;
            config.songPillWindowsMedia = songPillWindowsMediaCheckBox.IsChecked == true;

            config.debugEnabled = debugCheckBox.IsChecked == true;
            config.debugShowCacheHits = debugCacheHitsCheckBox.IsChecked == true;
        }

        private static bool isSpotifyRunning()
        {
            var processes = Process.GetProcessesByName("Spotify");
            foreach (var process in processes) process.Dispose();
            return processes.Length > 0;
        }

        private void checkSpotify()
        {
            bool running = isSpotifyRunning();
            if (running == spotifyRunning) return;
            spotifyRunning = running;

            // without a Client ID starting would only fail to log in
            if (followSpotifyCheckBox.IsChecked == true && (!running || clientIdBox.Password.Length > 0))
            {
                Log.Write(running ? "Spotify opened, starting the overlay" : "Spotify closed, stopping the overlay");
                setStarted(running);
            }
        }

        private void followSpotifyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            updateConfig();
            if (followSpotifyCheckBox.IsChecked == true && clientIdBox.Password.Length > 0) setStarted(spotifyRunning);
        }

        private void startWithWindowsCheckBox_Click(object sender, RoutedEventArgs e)
        {
            WindowsStartup.Set(startWithWindowsCheckBox.IsChecked == true);
            startWithWindowsCheckBox.IsChecked = WindowsStartup.IsEnabled;
        }

        private void runButton_Click(object sender, RoutedEventArgs e) => setStarted(!isStarted);

        private void setStarted(bool started)
        {
            isStarted = started;
            showRunState();
            updateConfig();

            if (isStarted && overlay == null)
            {
                overlay = new LyricsOverlay(() => isStarted);
                overlay.Show();
            }
        }

        private void refetchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isStarted || overlay?.Lyrics.RefetchCurrentSong() != true) showNothingPlaying();
        }

        private void setLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            var lyrics = overlay?.Lyrics;
            // the song is kept, the lyrics go to it even if the next one starts while typing
            var song = isStarted ? lyrics?.CurrentSong : null;
            if (lyrics == null || song == null)
            {
                showNothingPlaying();
                return;
            }

            var window = new LyricsLinkWindow($"{song.TrackName} — {song.TrackArtists}",
                link => LrcLibLyricsProvider.Instance.SetFromLinkAsync(song.TrackName ?? "", song.TrackArtists ?? "", song.TrackLength, link))
            {
                Owner = this
            };
            if (window.ShowDialog() == true)
            {
                lyrics.ReloadSong(song.TrackId);
            }
        }

        private void pasteLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            var lyrics = overlay?.Lyrics;
            // the song is kept, the lyrics go to it even if the next one starts while pasting
            var song = isStarted ? lyrics?.CurrentSong : null;
            if (lyrics == null || song == null)
            {
                showNothingPlaying();
                return;
            }

            var window = new LyricsPasteWindow($"{song.TrackName} — {song.TrackArtists}",
                lrc => LrcLibLyricsProvider.Instance.SetSyncedLyrics(song.TrackName ?? "", song.TrackArtists ?? "", song.TrackLength, lrc))
            {
                Owner = this
            };
            if (window.ShowDialog() == true)
            {
                lyrics.ReloadSong(song.TrackId);
            }
        }

        private void showNothingPlaying()
        {
            System.Windows.MessageBox.Show(this, "Start the overlay and play a song on Spotify first.", Title);
        }

        private void showRunState()
        {
            runButton.Content = isStarted ? "Stop" : "Start";
            runButton.Background = isStarted ? StopBrush : StartBrush;
            tray.ShowRunState(isStarted);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        public void ShowFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        //hides the window when it was in front, otherwise brings it up
        private void toggleFromTray()
        {
            bool inFront = IsVisible && WindowState != WindowState.Minimized
                && (IsActive || DateTime.UtcNow - lastDeactivated < TimeSpan.FromMilliseconds(500));
            if (inFront) Hide();
            else ShowFromTray();
        }

        private void exitApp()
        {
            exiting = true;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            updateConfig();
            ConfigManager.Instance.SaveConfig();

            if (!exiting)
            {
                e.Cancel = true;
                Hide();
                tray.ShowStillRunningTip();
                return;
            }

            spotifyTimer.Stop();
            overlay?.Close();
            tray.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
