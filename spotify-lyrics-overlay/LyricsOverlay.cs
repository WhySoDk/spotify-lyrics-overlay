using System.Runtime.InteropServices;

namespace spotify_lyrics_overlay
{
    public class LyricsOverlay : Form
    {
        private string currentLyrics;
        private Func<bool> isStartedProvider;
        private System.Windows.Forms.Timer updateTimer;
        private LyricsFactory lyricsFactory = new LyricsFactory();

        public LyricsOverlay(Func<bool> isStartedProvider)
        {
            this.isStartedProvider = isStartedProvider;
            currentLyrics = "";
            initializeOverlay();
            updateLyrics();
            applyConfig();
            setupTimer();

        }

        private void initializeOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            this.DoubleBuffered = true;

            var config = ConfigManager.Instance.LoadConfig();
            var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == config.screenName)
                         ?? Screen.PrimaryScreen;
            this.Bounds = screen.Bounds;

            int initialStyle = NativeMethods.GetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(this.Handle, NativeMethods.GWL_EXSTYLE,
                initialStyle | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);

        }

        private void applyConfig()
        {
            // Redraw with new config
            this.Invalidate();
        }

        private void setupTimer()
        {
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 200;
            updateTimer.Tick += (s, e) =>
            {
                if (isStartedProvider())
                {
                    if (!this.Visible)
                        this.Show();
                    applyConfig();
                    updateLyrics();
                }
                else
                {
                    if (this.Visible)
                        this.Hide();
                }
            };
            updateTimer.Start();
        }

        public async Task updateLyrics()
        {
            currentLyrics = await lyricsFactory.getLyricsAsync();
            this.Invalidate();
        }

        //TODO: Added second monitor support (fix it)
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var config = ConfigManager.Instance.LoadConfig();

            var style = FontStyle.Regular;
            if (config.bold) style |= FontStyle.Bold;
            if (config.italic) style |= FontStyle.Italic;

            using var font = new Font(config.fontName ?? "Arial", config.fontSize, style);
            using var brush = new SolidBrush(ColorTranslator.FromHtml(config.fontColorHex));
            using var shadowBrush = (config.dropShadow) ? new SolidBrush(Color.FromArgb(200, 1, 1, 1)) : new SolidBrush(Color.FromArgb(0, 0, 0, 0));

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            var lines = currentLyrics.Split(new[] { '\n' }, StringSplitOptions.None);

            float totalHeight = lines.Sum(line => g.MeasureString(line, font).Height);
            float y = this.Height / 2f - totalHeight / 2f - config.yOffset;

            foreach (var line in lines)
            {
                var textSize = g.MeasureString(line, font);
                float x = this.Width / 2f - textSize.Width / 2f + config.xOffset;

                g.DrawString(line, font, shadowBrush, new PointF(x + 2, y + 2));

                g.DrawString(line, font, brush, new PointF(x, y));

                y += textSize.Height;
            }
        }

        private static class NativeMethods
        {
            public const int GWL_EXSTYLE = -20;
            public const int WS_EX_LAYERED = 0x80000;
            public const int WS_EX_TRANSPARENT = 0x20;

            [DllImport("user32.dll")]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        }
    }
}
