using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Drawing.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace spotify_lyrics_overlay
{
    //dark color picker: saturation/brightness square, hue strip, preset swatches and a hex box.
    //ColorChanged fires while picking so the overlay can preview the color live
    public partial class ColorPickerWindow : Window
    {
        private static readonly string[] Presets =
        {
            "#f3ce32", "#ffffff", "#1ed760", "#4fc3f7", "#b388ff", "#ff5c8a", "#ff9f43", "#000000"
        };

        public Color Color { get; private set; }
        public event Action<Color>? ColorChanged;

        // kept separately so the hue survives picking a gray or black
        private float hue, saturation, value;
        private readonly Color initialColor;
        private bool updatingHex;

        public ColorPickerWindow(Color initial)
        {
            InitializeComponent();
            initialColor = initial;

            foreach (var hex in Presets)
            {
                var preset = ColorHelper.FromHex(hex, Color.White);
                var swatch = new Border
                {
                    Style = (Style)Resources["Swatch"],
                    Background = toBrush(preset),
                    Height = 28,
                    Margin = new Thickness(0, 0, hex == Presets[^1] ? 0 : 6, 0),
                    ToolTip = hex.ToUpperInvariant(),
                };
                swatch.MouseLeftButtonUp += (s, e) => setColor(preset);
                presetPanel.Children.Add(swatch);
            }

            oldColorSwatch.Background = toBrush(initial);
            setColor(initial);
        }

        private static SolidColorBrush toBrush(Color color) => new(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));

        private void Window_Loaded(object sender, RoutedEventArgs e) => updateMarkers();

        private void setColor(Color color, bool updateHsv = true, bool updateHexBox = true)
        {
            if (updateHsv)
            {
                var (h, s, v) = ColorHelper.ToHsv(color);
                // grays have no hue, keep the one the user had
                if (s > 0) hue = h;
                saturation = s;
                value = v;
            }

            Color = color;
            newColorSwatch.Background = toBrush(color);
            updateMarkers();

            if (updateHexBox)
            {
                updatingHex = true;
                hexBox.Text = ColorHelper.ToHex(color);
                updatingHex = false;
            }

            ColorChanged?.Invoke(color);
        }

        private void updateMarkers()
        {
            var pureHue = ColorHelper.FromHsv(hue, 1f, 1f);
            hueStop.Color = System.Windows.Media.Color.FromRgb(pureHue.R, pureHue.G, pureHue.B);

            Canvas.SetLeft(saturationValueMarker, saturation * saturationValueBox.ActualWidth - saturationValueMarker.Width / 2);
            Canvas.SetTop(saturationValueMarker, (1f - value) * saturationValueBox.ActualHeight - saturationValueMarker.Height / 2);
            Canvas.SetLeft(hueMarker, hue / 360f * hueSlider.ActualWidth - hueMarker.Width / 2);
        }

        private void pickSaturationValue(MouseEventArgs e)
        {
            var p = e.GetPosition(saturationValueBox);
            saturation = (float)Math.Clamp(p.X / saturationValueBox.ActualWidth, 0, 1);
            value = 1f - (float)Math.Clamp(p.Y / saturationValueBox.ActualHeight, 0, 1);
            setColor(ColorHelper.FromHsv(hue, saturation, value), updateHsv: false);
        }

        private void pickHue(MouseEventArgs e)
        {
            var p = e.GetPosition(hueSlider);
            hue = (float)Math.Clamp(p.X / hueSlider.ActualWidth, 0, 1) * 360f;
            setColor(ColorHelper.FromHsv(hue, saturation, value), updateHsv: false);
        }

        private void saturationValueBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            saturationValueBox.CaptureMouse();
            pickSaturationValue(e);
        }

        private void saturationValueBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (saturationValueBox.IsMouseCaptured) pickSaturationValue(e);
        }

        private void hueSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            hueSlider.CaptureMouse();
            pickHue(e);
        }

        private void hueSlider_MouseMove(object sender, MouseEventArgs e)
        {
            if (hueSlider.IsMouseCaptured) pickHue(e);
        }

        private void pickArea_MouseUp(object sender, MouseButtonEventArgs e) => ((UIElement)sender).ReleaseMouseCapture();

        private void oldColorSwatch_Click(object sender, MouseButtonEventArgs e) => setColor(initialColor);

        private void hexBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (updatingHex || !ColorHelper.IsValidHex(hexBox.Text)) return;
            setColor(ColorHelper.FromHex(hexBox.Text, Color), updateHexBox: false);
        }

        private void okButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
