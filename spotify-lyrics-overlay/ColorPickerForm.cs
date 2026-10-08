using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace spotify_lyrics_overlay
{
    //dark color picker: saturation/brightness square, hue strip, preset swatches and a hex box.
    //ColorChanged fires while picking so the overlay can preview the color live
    internal class ColorPickerForm : Form
    {
        private static readonly Color WindowColor = Color.FromArgb(27, 43, 52);
        private static readonly Color FieldColor = Color.FromArgb(40, 60, 72);
        private static readonly Color BorderColor = Color.FromArgb(70, 95, 110);

        private static readonly string[] Presets =
        {
            "#f3ce32", "#ffffff", "#1ed760", "#4fc3f7", "#b388ff", "#ff5c8a", "#ff9f43", "#000000"
        };

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color Color { get; private set; }
        public event Action<Color>? ColorChanged;

        // kept separately so the hue survives picking a gray or black
        private float hue, saturation, value;
        private readonly Color initialColor;
        private bool updatingHex;

        private readonly SaturationValueBox saturationValueBox = new();
        private readonly HueSlider hueSlider = new();
        private readonly Panel newColorPanel = new();
        private readonly TextBox hexBox = new();

        public ColorPickerForm(Color initial)
        {
            initialColor = initial;

            Text = "Pick a color";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = WindowColor;
            Font = new Font("Segoe UI", 12F);
            ClientSize = new Size(260, 316);

            saturationValueBox.SetBounds(12, 12, 236, 150);
            saturationValueBox.Picked += (s, e) =>
            {
                saturation = saturationValueBox.Saturation;
                value = saturationValueBox.Value;
                setColor(ColorHelper.FromHsv(hue, saturation, value), updateHsv: false);
            };

            hueSlider.SetBounds(12, 170, 236, 18);
            hueSlider.Picked += (s, e) =>
            {
                hue = hueSlider.Hue;
                setColor(ColorHelper.FromHsv(hue, saturation, value), updateHsv: false);
            };

            Controls.Add(saturationValueBox);
            Controls.Add(hueSlider);

            // preset swatches
            for (int i = 0; i < Presets.Length; i++)
            {
                var preset = ColorHelper.FromHex(Presets[i], Color.White);
                var swatch = createSwatch(preset, new Rectangle(12 + i * 30, 198, 26, 26));
                swatch.Click += (s, e) => setColor(preset);
                Controls.Add(swatch);
            }

            // old color (click to go back to it) and new color
            var oldColorPanel = createSwatch(initial, new Rectangle(12, 232, 36, 29));
            oldColorPanel.Click += (s, e) => setColor(initialColor);
            Controls.Add(oldColorPanel);

            newColorPanel.SetBounds(48, 232, 36, 29);
            newColorPanel.Paint += (s, e) => drawBorder(e.Graphics, newColorPanel.ClientRectangle);
            Controls.Add(newColorPanel);

            hexBox.SetBounds(92, 232, 156, 29);
            hexBox.BackColor = FieldColor;
            hexBox.ForeColor = SystemColors.ControlLight;
            hexBox.BorderStyle = BorderStyle.FixedSingle;
            hexBox.PlaceholderText = "#RRGGBB";
            hexBox.TextChanged += (s, e) =>
            {
                if (updatingHex || !ColorHelper.IsValidHex(hexBox.Text)) return;
                setColor(ColorHelper.FromHex(hexBox.Text, Color), updateHexBox: false);
            };
            Controls.Add(hexBox);

            var cancelButton = createButton("Cancel", new Rectangle(12, 272, 112, 32), DialogResult.Cancel);
            var okButton = createButton("OK", new Rectangle(136, 272, 112, 32), DialogResult.OK);
            okButton.BackColor = Color.Green;
            Controls.Add(cancelButton);
            Controls.Add(okButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            setColor(initial);
        }

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

            saturationValueBox.Hue = hue;
            saturationValueBox.Saturation = saturation;
            saturationValueBox.Value = value;
            saturationValueBox.Invalidate();
            hueSlider.Hue = hue;
            hueSlider.Invalidate();
            newColorPanel.BackColor = color;

            if (updateHexBox)
            {
                updatingHex = true;
                hexBox.Text = ColorHelper.ToHex(color);
                updatingHex = false;
            }

            ColorChanged?.Invoke(color);
        }

        private static Panel createSwatch(Color color, Rectangle bounds)
        {
            var swatch = new Panel { BackColor = color, Cursor = Cursors.Hand };
            swatch.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            swatch.Paint += (s, e) => drawBorder(e.Graphics, swatch.ClientRectangle);
            return swatch;
        }

        private static Button createButton(string text, Rectangle bounds, DialogResult result)
        {
            var button = new Button
            {
                Text = text,
                DialogResult = result,
                FlatStyle = FlatStyle.Flat,
                BackColor = FieldColor,
                ForeColor = SystemColors.ControlLight,
            };
            button.FlatAppearance.BorderColor = BorderColor;
            button.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            return button;
        }

        private static void drawBorder(Graphics g, Rectangle bounds)
        {
            using var pen = new Pen(BorderColor);
            g.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
        }

        //dark title bar to match the window, ignored on Windows versions without it
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int enabled = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        //saturation from left to right, brightness from bottom to top
        private class SaturationValueBox : Control
        {
            public float Hue, Saturation, Value;
            public event EventHandler? Picked;

            public SaturationValueBox()
            {
                DoubleBuffered = true;
                Cursor = Cursors.Cross;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var rect = ClientRectangle;
                // gradient brushes wrap around at their edge, make them a bit bigger than the area
                var brushRect = Rectangle.Inflate(rect, 1, 1);

                using (var saturationBrush = new LinearGradientBrush(brushRect, Color.White, ColorHelper.FromHsv(Hue, 1f, 1f), LinearGradientMode.Horizontal))
                    g.FillRectangle(saturationBrush, rect);
                using (var valueBrush = new LinearGradientBrush(brushRect, Color.Transparent, Color.Black, LinearGradientMode.Vertical))
                    g.FillRectangle(valueBrush, rect);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                float x = Saturation * (Width - 1);
                float y = (1f - Value) * (Height - 1);
                using var outer = new Pen(Color.White, 2f);
                using var inner = new Pen(Color.Black, 1f);
                g.DrawEllipse(outer, x - 6, y - 6, 12, 12);
                g.DrawEllipse(inner, x - 7.5f, y - 7.5f, 15, 15);
            }

            protected override void OnMouseDown(MouseEventArgs e) => pick(e);

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) pick(e);
            }

            private void pick(MouseEventArgs e)
            {
                Saturation = Math.Clamp(e.X / (float)(Width - 1), 0f, 1f);
                Value = 1f - Math.Clamp(e.Y / (float)(Height - 1), 0f, 1f);
                Invalidate();
                Picked?.Invoke(this, EventArgs.Empty);
            }
        }

        //hue from left (0) to right (360)
        private class HueSlider : Control
        {
            public float Hue;
            public event EventHandler? Picked;

            public HueSlider()
            {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var rect = ClientRectangle;

                using (var brush = new LinearGradientBrush(Rectangle.Inflate(rect, 1, 0), Color.Red, Color.Red, LinearGradientMode.Horizontal))
                {
                    brush.InterpolationColors = new ColorBlend
                    {
                        Colors = Enumerable.Range(0, 7).Select(i => ColorHelper.FromHsv(i * 60f, 1f, 1f)).ToArray(),
                        Positions = Enumerable.Range(0, 7).Select(i => i / 6f).ToArray(),
                    };
                    g.FillRectangle(brush, rect);
                }

                float x = Hue / 360f * (Width - 1);
                using var outer = new Pen(Color.Black, 1f);
                using var inner = new Pen(Color.White, 2f);
                g.DrawRectangle(inner, x - 2, 1, 4, Height - 3);
                g.DrawRectangle(outer, x - 4, 0, 8, Height - 1);
            }

            protected override void OnMouseDown(MouseEventArgs e) => pick(e);

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) pick(e);
            }

            private void pick(MouseEventArgs e)
            {
                Hue = Math.Clamp(e.X / (float)(Width - 1), 0f, 1f) * 360f;
                Invalidate();
                Picked?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
