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

        // text color from the album cover, null uses the configured color
        private Color? albumColor;
        // shown while the album color is still being picked
        private static readonly Color AlbumColorPlaceholder = Color.FromArgb(120, 200, 200, 200);

        // background box, glides toward the size of the finished layout instead of snapping
        private const float BoxSmoothingSeconds = 0.06f;
        private const float MaxBoxFrameSeconds = 0.025f;
        private RectangleF? boxRect;
        private bool boxAnimating;
        private bool snapBox = true;
        private readonly System.Diagnostics.Stopwatch boxClock = new();

        // karaoke, opacity of the part of the line that is not sung yet
        private const float KaraokeUnfilledOpacity = 0.4f;

        // debug info box above the lyrics
        private string? debugText;
        private static readonly Font DebugFont = new("Segoe UI", 10f);
        private static readonly Color DebugBoxColor = Color.FromArgb(150, 60, 60, 60);
        private static readonly Color DebugTextColor = Color.FromArgb(230, 230, 230, 230);
        private const float DebugPadding = 4f;
        private const float DebugGap = 6f;

        // used to measure text before the bitmap size is known
        private readonly Graphics measureGraphics = createMeasureGraphics();

        private static Graphics createMeasureGraphics()
        {
            var g = Graphics.FromImage(new Bitmap(1, 1));
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            return g;
        }

        internal LyricsFactory Lyrics => lyricsFactory;

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
                // WS_EX_TOOLWINDOW (0x80) keeps it out of Alt+Tab,
                // WS_EX_NOACTIVATE (0x8000000) keeps it from taking focus
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000;
                return cp;
            }
        }

        //don't steal focus from the active app when the overlay appears
        protected override bool ShowWithoutActivation => true;

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

            var color = !config.albumColor ? null
                : lyricsFactory.AlbumColorPending ? AlbumColorPlaceholder
                : lyricsFactory.AlbumColor;
            bool colorChanged = color != albumColor;
            albumColor = color;

            string? debug = config.debugEnabled ? DebugStatus.Current(config.debugShowCacheHits) : null;
            bool debugChanged = debug != debugText;
            debugText = debug;

            if (needsRender || modeChanged || colorChanged || debugChanged || boxAnimating)
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
            using var secondaryFont = createSecondaryFont(config.secondaryFontName, config.fontSize, style);
            FontFallback.Configure(font, secondaryFont);
            var textColor = albumColor ?? ColorTranslator.FromHtml(config.fontColorHex);

            // 1. Layout the text, positions are relative to the selected screen
            float centerY = screenBounds.Height / 2f - config.yOffset;
            var items = transition.Layout(measureGraphics, font, centerY);
            foreach (var item in items)
            {
                item.X = screenBounds.Width / 2f - item.Size.Width / 2f + config.xOffset + item.OffsetX;
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

            // debug info sits right above the lyrics, or above the center line when there are none
            RectangleF debugBox = RectangleF.Empty;
            if (debugText != null)
            {
                var size = measureGraphics.MeasureString(debugText, DebugFont);
                float bottom = (hasText ? content.Top : centerY) - DebugGap;
                debugBox = new RectangleF(
                    screenBounds.Width / 2f + config.xOffset - size.Width / 2f - DebugPadding,
                    bottom - size.Height - DebugPadding * 2,
                    size.Width + DebugPadding * 2,
                    size.Height + DebugPadding * 2);
                content = content.IsEmpty ? debugBox : RectangleF.Union(content, debugBox);
            }
            bool hasContent = hasText || !debugBox.IsEmpty;

            // 2. Only the area with content becomes the window, clamped to the screen
            var area = Rectangle.Intersect(Rectangle.Ceiling(content),
                new Rectangle(0, 0, screenBounds.Width, screenBounds.Height));
            if (!hasContent || area.Width <= 0 || area.Height <= 0)
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

                    if (hasContent)
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

                        if (!debugBox.IsEmpty)
                        {
                            using var debugBoxBrush = new SolidBrush(DebugBoxColor);
                            using var debugTextBrush = new SolidBrush(DebugTextColor);
                            g.FillRectangle(debugBoxBrush, debugBox);
                            g.DrawString(debugText, DebugFont, debugTextBrush, debugBox.X + DebugPadding, debugBox.Y + DebugPadding);
                        }
                    }
                }

                // 3. Push the bitmap to the window using Win32 API
                SetLayeredWindowBitmap(bitmap, new Point(screenBounds.X + area.X, screenBounds.Y + area.Y));
            }
        }

        //null when no secondary font is set or it isn't installed, styles the font lacks are left out
        private static Font? createSecondaryFont(string name, float size, FontStyle style)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            FontFamily family;
            try
            {
                family = new FontFamily(name);
            }
            catch (ArgumentException)
            {
                return null;
            }

            using var _ = family;
            foreach (var candidate in new[] { style, style & ~FontStyle.Italic, style & ~FontStyle.Bold, FontStyle.Regular })
            {
                if (family.IsStyleAvailable(candidate)) return new Font(family, size, candidate);
            }
            return null;
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

            var lineFont = item.Font ?? font;
            using var scaledFont = item.Scale == 1f ? null : new Font(lineFont.FontFamily, lineFont.Size * item.Scale, lineFont.Style);
            var itemFont = scaledFont ?? lineFont;

            if (item.Fill is not float fill)
            {
                drawText(g, item, itemFont, textColor, dropShadow, item.Opacity);
                return;
            }

            // karaoke, the part right of the split is dimmed, the part left of it in full color
            float split = item.X + item.Size.Width * fill;
            float top = item.Y - item.Size.Height;
            float bottom = item.Y + item.Size.Height * 2;
            var state = g.Save();

            g.SetClip(RectangleF.FromLTRB(split, top, item.X + item.Size.Width + item.Size.Height, bottom));
            drawText(g, item, itemFont, textColor, dropShadow, item.Opacity * KaraokeUnfilledOpacity);

            g.SetClip(RectangleF.FromLTRB(item.X - item.Size.Height, top, split, bottom));
            drawText(g, item, itemFont, textColor, dropShadow, item.Opacity);

            g.Restore(state);
        }

        private static void drawText(Graphics g, TextItem item, Font font, Color textColor, bool dropShadow, float opacity)
        {
            // Using 200 Alpha for shadow
            if (dropShadow)
            {
                using var shadowBrush = new SolidBrush(Color.FromArgb((int)(200 * opacity), 1, 1, 1));
                g.DrawString(item.Text, font, shadowBrush, new PointF(item.X + 2, item.Y + 2));
            }

            using var brush = new SolidBrush(Color.FromArgb((int)(textColor.A * opacity), textColor));
            g.DrawString(item.Text, font, brush, new PointF(item.X, item.Y));
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