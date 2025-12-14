using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

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

            RenderLayeredWindow();
            updateLyrics();
            setupTimer();
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


            // transparency via the alpha channel of the Bitmap
            var config = ConfigManager.Instance.LoadConfig();
            var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == config.screenName)
                         ?? Screen.PrimaryScreen;

            this.Bounds = screen.Bounds;
        }

        private void applyConfig()
        {
            //call custom render method 
            RenderLayeredWindow();
        }

        private void setupTimer()
        {
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 200;
            updateTimer.Tick += (s, e) =>
            {
                if (isStartedProvider())
                {
                    if (!this.Visible) this.Show();

                    // check if config changed before calling this every 200ms to save CPU
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

        public async Task updateLyrics()
        {
            string newLyrics = await lyricsFactory.getLyricsAsync();

            //re-render if text actually changed
            if (currentLyrics != newLyrics)
            {
                currentLyrics = newLyrics;
                RenderLayeredWindow();
            }
        }

        //write to Bitmap memory instead of the screen.
        private void RenderLayeredWindow()
        {
            // Safety check
            if (this.Width <= 0 || this.Height <= 0) return;

            // 1. Create a bitmap of the form size
            using (Bitmap bitmap = new Bitmap(this.Width, this.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // High Quality settings for smooth text
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // Clear with 100% transparent background
                    g.Clear(Color.Transparent);

                    var config = ConfigManager.Instance.LoadConfig();

                    var style = FontStyle.Regular;
                    if (config.bold) style |= FontStyle.Bold;
                    if (config.italic) style |= FontStyle.Italic;

                    using var font = new Font(config.fontName ?? "Arial", config.fontSize, style);
                    using var brush = new SolidBrush(ColorTranslator.FromHtml(config.fontColorHex));

                    // Using 200 Alpha for shadow, or fully transparent if disabled
                    using var shadowBrush = (config.dropShadow)
                        ? new SolidBrush(Color.FromArgb(200, 1, 1, 1))
                        : new SolidBrush(Color.Transparent);

                    var lines = currentLyrics.Split(new[] { '\n' }, StringSplitOptions.None);

                    float totalHeight = lines.Sum(line => g.MeasureString(line, font).Height);
                    float y = this.Height / 2f - totalHeight / 2f - config.yOffset;

                    foreach (var line in lines)
                    {
                        var textSize = g.MeasureString(line, font);
                        float x = this.Width / 2f - textSize.Width / 2f + config.xOffset;

                        // Draw Shadow
                        if (config.dropShadow)
                        {
                            g.DrawString(line, font, shadowBrush, new PointF(x + 2, y + 2));
                        }

                        // Draw Text
                        g.DrawString(line, font, brush, new PointF(x, y));

                        y += textSize.Height;
                    }
                }

                // 2. Push the bitmap to the window using Win32 API
                SetLayeredWindowBitmap(bitmap);
            }
        }

        //Helper to interface with Windows API
        private void SetLayeredWindowBitmap(Bitmap bitmap)
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
                Point topPos = new Point(this.Left, this.Top);

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