using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using spotify_lyrics_overlay.Transitions;

namespace spotify_lyrics_overlay
{
    public class LyricsOverlay : Form
    {
        private Func<bool> isStartedProvider;
        private System.Windows.Forms.Timer updateTimer;
        private LyricsFactory lyricsFactory = new LyricsFactory();
        private readonly CancellationTokenSource pollingCancellation = new();
        private string lastConfigSnapshot = "";
        private Rectangle screenBounds;

        // line transition mode, picked in the main window
        private LyricsTransitions.Mode? transitionMode;
        private ILyricsTransition transition = new LegacyTransition();

        // background box, glides toward the size of the finished layout instead of snapping
        private const float BoxSmoothingSeconds = 0.06f;
        private const float MaxBoxFrameSeconds = 0.025f;
        private RectangleF? boxRect;
        private bool boxAnimating;
        private bool snapBox = true;
        private readonly System.Diagnostics.Stopwatch boxClock = new();

        // used to measure text before the bitmap size is known
        private readonly Graphics measureGraphics = createMeasureGraphics();

        private static Graphics createMeasureGraphics()
        {
            var g = Graphics.FromImage(new Bitmap(1, 1));
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            return g;
        }

        public LyricsOverlay(Func<bool> isStartedProvider)
        {
            this.isStartedProvider = isStartedProvider;
            initializeOverlay();

            RenderLayeredWindow();
            //Spotify is polled in its own loop, the timer only renders the local state
            _ = lyricsFactory.RunPollingAsync(isStartedProvider, pollingCancellation.Token);
            setupTimer();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pollingCancellation.Cancel();
            updateTimer.Stop();
            base.OnFormClosed(e);
        }

        //treat this form as a layered Window
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_LAYERED (0x80000) | WS_EX_TRANSPARENT (0x20)
                cp.ExStyle |= 0x80000 | 0x20;
                return cp;
            }
        }

        private void initializeOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            // keep our own position instead of the Windows default location
            this.StartPosition = FormStartPosition.Manual;

            // transparency via the alpha channel of the Bitmap
            updateScreenBounds();

            // WinForms resets the window size when showing it, draw again afterwards
            this.VisibleChanged += (s, e) => { if (this.Visible) RenderLayeredWindow(); };
        }

        //track the selected monitor, the window itself only covers the lyrics
        //so Windows doesn't treat it as a fullscreen app (that blocks the auto-hide taskbar)
        private void updateScreenBounds()
        {
            var config = ConfigManager.Instance.LoadConfig();
            var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == config.screenName)
                         ?? Screen.PrimaryScreen!;

            screenBounds = screen.Bounds;
        }

        private void applyConfig()
        {
            // only re-render when the config actually changed to save CPU
            string configSnapshot = ConfigManager.Instance.LoadConfig().ToString();
            if (configSnapshot == lastConfigSnapshot) return;
            lastConfigSnapshot = configSnapshot;

            updateScreenBounds();

            //call custom render method, settings changes resize the box right away
            snapBox = true;
            RenderLayeredWindow();
        }

        private void setupTimer()
        {
            updateTimer = new System.Windows.Forms.Timer();
            // fast enough for smooth transitions, idle ticks only compare state
            updateTimer.Interval = 16;
            updateTimer.Tick += (s, e) =>
            {
                if (isStartedProvider())
                {
                    if (!this.Visible) this.Show();

                    applyConfig();
                    updateLyrics();
                }
                else
                {
                    if (this.Visible) this.Hide();
                }
            };
            updateTimer.Start();
        }

        public void updateLyrics()
        {
            var config = ConfigManager.Instance.LoadConfig();

            // start from a fresh state when the transition mode changes
            var mode = LyricsTransitions.Find(config.transitionMode);
            bool modeChanged = mode != transitionMode;
            if (modeChanged)
            {
                transitionMode = mode;
                transition = mode.Create();
                snapBox = true;
            }

            bool needsRender = transition.Update(lyricsFactory.getLyricsView());
            if (needsRender || modeChanged || boxAnimating)
            {
                RenderLayeredWindow();
            }
        }

        //write to Bitmap memory instead of the screen.
        private void RenderLayeredWindow()
        {
            if (screenBounds.Width <= 0 || screenBounds.Height <= 0) return;

            var config = ConfigManager.Instance.LoadConfig();

            var style = FontStyle.Regular;
            if (config.bold) style |= FontStyle.Bold;
            if (config.italic) style |= FontStyle.Italic;

            using var font = new Font(config.fontName ?? "Arial", config.fontSize, style);
            var textColor = ColorTranslator.FromHtml(config.fontColorHex);

            // 1. Layout the text, positions are relative to the selected screen
            float centerY = screenBounds.Height / 2f - config.yOffset;
            var items = transition.Layout(measureGraphics, font, centerY);
            foreach (var item in items)
            {
                item.X = screenBounds.Width / 2f - item.Size.Width / 2f + config.xOffset;
            }

            bool hasText = items.Any(item => !string.IsNullOrWhiteSpace(item.Text));
            RectangleF content = RectangleF.Empty;
            foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Text)))
            {
                var rect = new RectangleF(item.X, item.Y, item.Size.Width + 2, item.Size.Height + 2);
                content = content.IsEmpty ? rect : RectangleF.Union(content, rect);
            }

            RectangleF box = RectangleF.Empty;
            if (config.backgroundEnabled && hasText)
            {
                // size for the layout after the running animation, e.g. without a line that is fading out
                var target = getBoxRect(transition.LayoutTarget(measureGraphics, font, centerY), config)
                             ?? getBoxRect(items, config)!.Value;
                box = updateBoxRect(target);
                content = RectangleF.Union(content, box);
            }
            else
            {
                boxRect = null;
                boxAnimating = false;
            }
            snapBox = false;

            // 2. Only the area with content becomes the window, clamped to the screen
            var area = Rectangle.Intersect(Rectangle.Ceiling(content),
                new Rectangle(0, 0, screenBounds.Width, screenBounds.Height));
            if (!hasText || area.Width <= 0 || area.Height <= 0)
            {
                area = new Rectangle(screenBounds.Width / 2, screenBounds.Height / 2, 1, 1);
            }

            using (Bitmap bitmap = new Bitmap(area.Width, area.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // High Quality settings for smooth text
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // Clear with 100% transparent background
                    g.Clear(Color.Transparent);

                    if (hasText)
                    {
                        g.TranslateTransform(-area.X, -area.Y);

                        // Draw Background box around the whole text block
                        if (!box.IsEmpty)
                        {
                            var boxColor = ColorHelper.WithOpacity(ColorHelper.FromHex(config.backgroundColorHex, Color.Black), config.backgroundOpacity);
                            using var boxBrush = new SolidBrush(boxColor);
                            g.FillRectangle(boxBrush, box);
                        }

                        foreach (var item in items)
                        {
                            drawTextItem(g, item, font, textColor, config.dropShadow);
                        }
                    }
                }

                // 3. Push the bitmap to the window using Win32 API
                SetLayeredWindowBitmap(bitmap, new Point(screenBounds.X + area.X, screenBounds.Y + area.Y));
            }
        }

        //box around the lines that have text, null when there are none
        private RectangleF? getBoxRect(List<TextItem> items, AppConfig config)
        {
            var visible = items.Where(item => !string.IsNullOrWhiteSpace(item.Text)).ToList();
            if (visible.Count == 0) return null;

            float maxWidth = visible.Max(item => item.Size.Width);
            float spread = config.backgroundSpread;
            float top = visible.Min(item => item.Y);
            float bottom = visible.Max(item => item.Y + item.Size.Height);
            return new RectangleF(
                screenBounds.Width / 2f - maxWidth / 2f + config.xOffset - spread,
                top - spread,
                maxWidth + spread * 2,
                bottom - top + spread * 2);
        }

        //move the drawn box part of the way to the target, frame rate independent
        private RectangleF updateBoxRect(RectangleF target)
        {
            // a long pause between renders must not turn into one big jump
            float dt = Math.Min((float)boxClock.Elapsed.TotalSeconds, MaxBoxFrameSeconds);
            boxClock.Restart();

            if (boxRect == null || snapBox)
            {
                // box just appeared or the settings changed
                boxRect = target;
            }
            else
            {
                float k = 1f - (float)Math.Exp(-dt / BoxSmoothingSeconds);
                var current = boxRect.Value;
                var next = RectangleF.FromLTRB(
                    current.Left + (target.Left - current.Left) * k,
                    current.Top + (target.Top - current.Top) * k,
                    current.Right + (target.Right - current.Right) * k,
                    current.Bottom + (target.Bottom - current.Bottom) * k);

                bool settled = Math.Abs(next.Left - target.Left) < 0.5f && Math.Abs(next.Top - target.Top) < 0.5f
                    && Math.Abs(next.Right - target.Right) < 0.5f && Math.Abs(next.Bottom - target.Bottom) < 0.5f;
                boxRect = settled ? target : next;
            }

            boxAnimating = boxRect.Value != target;
            return boxRect.Value;
        }

        private void drawTextItem(Graphics g, TextItem item, Font font, Color textColor, bool dropShadow)
        {
            if (string.IsNullOrWhiteSpace(item.Text) || item.Opacity <= 0f) return;

            using var scaledFont = item.Scale == 1f ? null : new Font(font.FontFamily, font.Size * item.Scale, font.Style);
            var itemFont = scaledFont ?? font;

            // Using 200 Alpha for shadow
            if (dropShadow)
            {
                using var shadowBrush = new SolidBrush(Color.FromArgb((int)(200 * item.Opacity), 1, 1, 1));
                g.DrawString(item.Text, itemFont, shadowBrush, new PointF(item.X + 2, item.Y + 2));
            }

            using var brush = new SolidBrush(Color.FromArgb((int)(textColor.A * item.Opacity), textColor));
            g.DrawString(item.Text, itemFont, brush, new PointF(item.X, item.Y));
        }

        //Helper to interface with Windows API
        private void SetLayeredWindowBitmap(Bitmap bitmap, Point topPos)
        {
            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

                Size size = new Size(bitmap.Width, bitmap.Height);
                Point pointSource = new Point(0, 0);

                NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION();
                blend.BlendOp = NativeMethods.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = NativeMethods.AC_SRC_ALPHA;

                NativeMethods.UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, NativeMethods.ULW_ALPHA);
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    NativeMethods.SelectObject(memDc, oldBitmap);
                    NativeMethods.DeleteObject(hBitmap);
                }
                NativeMethods.DeleteDC(memDc);
            }
        }

        private static class NativeMethods
        {
            public const int WS_EX_LAYERED = 0x80000;
            public const int WS_EX_TRANSPARENT = 0x20;
            public const byte AC_SRC_OVER = 0x00;
            public const byte AC_SRC_ALPHA = 0x01;
            public const int ULW_ALPHA = 0x00000002;

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct BLENDFUNCTION
            {
                public byte BlendOp;
                public byte BlendFlags;
                public byte SourceConstantAlpha;
                public byte AlphaFormat;
            }

            [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
            public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

            [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("user32.dll", ExactSpelling = true)]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
            public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

            [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
            public static extern bool DeleteDC(IntPtr hdc);

            [DllImport("gdi32.dll", ExactSpelling = true)]
            public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

            [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
            public static extern bool DeleteObject(IntPtr hObject);
        }
    }
}