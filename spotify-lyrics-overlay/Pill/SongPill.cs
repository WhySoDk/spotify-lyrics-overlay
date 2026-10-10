using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace spotify_lyrics_overlay.Pill
{
    //pill that pops out of the top of the screen when the song changes: cover, title, artist and lyrics status.
    //a window and timer of its own, so it animates apart from the lyrics and only runs while it's on screen.
    //hovering keeps it up, clicking the status chip marks the song as instrumental (or undoes that)
    internal class SongPill : Form
    {
        private readonly LyricsFactory lyrics;
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
        private readonly Stopwatch frameClock = new();
        // animation time, see PillAnimation
        private float animMs;
        private PillPose pose;
        // since the song changed on the pill that was already up, -1 when not swapping
        private float swapMs = -1;

        // picked per song, Prompt for Thai
        private Font titleFont = new(PillStyle.FontName, PillStyle.TitleSize, FontStyle.Bold);
        private Font artistFont = new(PillStyle.FontName, PillStyle.ArtistSize);
        private readonly Font statusFont = new(PillStyle.FontName, PillStyle.StatusSize, FontStyle.Bold);
        private readonly Graphics measureGraphics = createMeasureGraphics();

        // song on the pill, null while hidden
        private PlaybackState? song;
        private Bitmap? cover;
        // counts Play / PlayEarly calls, a cover that finishes loading for an older one is dropped
        private int playId;
        // shown from the Windows media info, Spotify's answer takes it over without starting again
        private bool early;
        private readonly Stopwatch sinceEarly = new();
        private static readonly TimeSpan EarlyTakeOver = TimeSpan.FromSeconds(10);
        private SongLyricsStatus? status;
        private bool setByHand;
        private string statusText = "";
        private Color statusDot;

        // the cursor is over the pill, the window only takes the mouse then
        private bool hovered;
        private bool clickThrough = true;
        // since the cursor left the pill, not running before it was hovered
        private readonly Stopwatch sinceHover = new();
        // the cursor is over the status chip and it can be clicked
        private bool chipHovered;

        // window area at the top center of the screen, the pill is drawn inside it
        private Size area;
        private Point areaPos;

        //layout at full size, origin at the pill's top left. the width glides to the target when the status changes
        private float targetWidth;
        private float width;
        private RectangleF thumbRect, titleRect, artistRect, statusRect;
        // full width of the title, it scrolls when wider than titleRect
        private float titleWidth;
        // marquee time, runs from when the pill is in place, also while it's held
        private float marqueeMs;

        public SongPill(LyricsFactory lyrics)
        {
            this.lyrics = lyrics;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;

            timer.Tick += (s, e) => tick();
            // WinForms resets the window size when showing it, draw again afterwards
            VisibleChanged += (s, e) => { if (Visible) render(); };
        }

        private static Graphics createMeasureGraphics()
        {
            var g = Graphics.FromImage(new Bitmap(1, 1));
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            return g;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_LAYERED | WS_EX_TRANSPARENT (click-through) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, same as the overlay
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000;
                return cp;
            }
        }

        //start the animation for a song on the given screen. a pill that's still up shows the new song in place
        //and stays up longer, so skipping through songs doesn't replay the intro each time.
        //the song already shown from the Windows media info is just filled in
        public void Play(PlaybackState newSong, Rectangle screenBounds)
        {
            if (early && sinceEarly.Elapsed < EarlyTakeOver)
            {
                takeOver(newSong);
                return;
            }
            start(newSong, screenBounds);
            _ = loadCoverAsync(newSong, playId);
        }

        //show the song Windows saw right away, before Spotify confirms it
        public void PlayEarly(MediaTrack track, Rectangle screenBounds)
        {
            start(new PlaybackState { TrackName = track.Title, TrackArtists = track.Artist }, screenBounds);
            early = true;
            sinceEarly.Restart();
            _ = setCoverAsync(track.LoadThumbnailAsync(), playId);
        }

        //Spotify's answer for the song shown early: its title and artist win, the animation carries on
        private void takeOver(PlaybackState newSong)
        {
            early = false;
            if (song == null) return;

            bool renamed = newSong.TrackName != song.TrackName || newSong.TrackArtists != song.TrackArtists;
            song = newSong;
            if (renamed) createFonts(newSong);
            status = null;
            updateStatus();
            if (cover == null) _ = loadCoverAsync(newSong, playId);
        }

        private void createFonts(PlaybackState forSong)
        {
            titleFont.Dispose();
            artistFont.Dispose();
            titleFont = PillFonts.Create(forSong.TrackName ?? "", PillStyle.TitleSize, FontStyle.Bold);
            artistFont = PillFonts.Create(forSong.TrackArtists ?? "", PillStyle.ArtistSize, FontStyle.Regular);
        }

        private void start(PlaybackState newSong, Rectangle screenBounds)
        {
            var newArea = new Size((int)(PillStyle.MaxWidth * 1.3f) + 40, (int)(PillStyle.RestTop + PillStyle.Height * 1.5f) + 40);
            var newAreaPos = new Point(screenBounds.X + (screenBounds.Width - newArea.Width) / 2, screenBounds.Y);
            // once it's shrinking back to the nub it's too late, it starts over
            bool swap = song != null && Visible && animMs < PillAnimation.CollapseAt && newAreaPos == areaPos;

            playId++;
            early = false;
            song = newSong;
            cover?.Dispose();
            cover = null;
            status = null;
            createFonts(newSong);

            if (swap)
            {
                // the cursor may still be on the pill, hover is kept. shown again for the full time from here
                chipHovered = false;
                Cursor = Cursors.Default;
                updateStatus();
                animMs = Math.Min(animMs, PillAnimation.SettledAt);
                marqueeMs = 0;
                swapMs = 0;
                return;
            }

            hovered = chipHovered = false;
            sinceHover.Reset();
            setClickThrough(true);

            area = newArea;
            areaPos = newAreaPos;
            Bounds = new Rectangle(areaPos, area);

            updateStatus();
            swapMs = -1;
            width = targetWidth;
            animMs = 0;
            marqueeMs = 0;
            pose = PillAnimation.At(0, width);

            render();
            if (!Visible) Show();
            frameClock.Restart();
            timer.Start();
        }

        //hide right away, e.g. when the overlay is stopped
        public void Dismiss()
        {
            early = false;
            finish();
        }

        //the early song can still be taken over after this, so Spotify's late answer doesn't show it again
        private void finish()
        {
            if (song == null) return;
            song = null;
            timer.Stop();
            Hide();
            cover?.Dispose();
            cover = null;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            base.OnFormClosed(e);
        }

        private Task loadCoverAsync(PlaybackState forSong, int forPlay)
        {
            return forSong.AlbumImageUrl == null ? Task.CompletedTask : setCoverAsync(AlbumCover.LoadAsync(forSong.AlbumImageUrl), forPlay);
        }

        private async Task setCoverAsync(Task<Bitmap?> loading, int forPlay)
        {
            var bitmap = await loading;
            // the next song may have started while loading
            if (forPlay != playId || song == null || bitmap == null)
            {
                bitmap?.Dispose();
                return;
            }
            cover?.Dispose();
            cover = scaleToThumb(bitmap);
        }

        //the cover shrunk once to the thumb size, a texture brush would sample the full size cover roughly every frame
        private static Bitmap scaleToThumb(Bitmap bitmap)
        {
            int size = (int)Math.Ceiling(PillStyle.ThumbSize);
            var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaled))
            using (var attributes = new ImageAttributes())
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(bitmap, new Rectangle(0, 0, size, size), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
            }
            bitmap.Dispose();
            return scaled;
        }

        private void tick()
        {
            if (song == null) return;

            float dt = Math.Min((float)frameClock.Elapsed.TotalMilliseconds, 100f);
            frameClock.Restart();

            updateHover();
            if (animMs >= PillAnimation.SettledAt) marqueeMs += dt;
            animMs = advance(animMs, dt);
            if (animMs >= PillAnimation.TotalMs)
            {
                finish();
                return;
            }

            updateStatus();
            width += (targetWidth - width) * (1 - MathF.Exp(-dt / 1000f / PillStyle.WidthSmoothingSeconds));

            pose = PillAnimation.At(animMs, width);
            if (swapMs >= 0)
            {
                swapMs += dt;
                pose = PillAnimation.Swapped(pose, swapMs);
                if (swapMs >= PillAnimation.SwapMs) swapMs = -1;
            }
            render();
        }

        //while hovered (and a moment after) the pill holds once it's in place,
        //hovering while the info fades out brings it back
        private float advance(float ms, float dt)
        {
            bool holding = hovered || (sinceHover.IsRunning && sinceHover.ElapsedMilliseconds < PillStyle.HoverGraceMs)
                || (titleScrolls && marqueeMs < marqueeCycleMs && marqueeMs < PillStyle.MarqueeMaxHoldMs);
            if (!holding || ms < PillAnimation.SettledAt || ms >= PillAnimation.CollapseAt) return ms + dt;
            return ms > PillAnimation.ExitAt ? Math.Max(PillAnimation.ExitAt, ms - dt) : ms;
        }

        private void updateHover()
        {
            var cursor = PointToClient(Cursor.Position);
            bool wasHovered = hovered;
            // only the widget counts, not the nub on its way in or out
            hovered = pose.Fill > 0.5f && pillRect.Contains(cursor);
            if (wasHovered && !hovered) sinceHover.Restart();
            setClickThrough(!hovered);

            var chip = statusRect;
            chip.Offset(contentOrigin);
            bool wasChipHovered = chipHovered;
            chipHovered = hovered && chipClickable && chip.Contains(cursor);
            if (chipHovered != wasChipHovered)
            {
                Cursor = chipHovered ? Cursors.Hand : Cursors.Default;
                status = null;
                updateStatus();
            }
        }

        //plain lyrics or none can be marked instrumental, a song marked by hand can be unmarked
        private bool chipClickable => status is SongLyricsStatus.Plain or SongLyricsStatus.NotFound
            || (status == SongLyricsStatus.Instrumental && setByHand);

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || song == null) return;

            updateHover();
            if (!chipHovered) return;

            lyrics.SetInstrumental(song, status != SongLyricsStatus.Instrumental);
            chipHovered = false;
            Cursor = Cursors.Default;
            status = null;
            updateStatus();
        }

        //the window takes the mouse only while the cursor is over the pill, so the shadow and empty space never block clicks
        private void setClickThrough(bool on)
        {
            if (on == clickThrough || !IsHandleCreated) return;
            clickThrough = on;
            long style = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            style = on ? style | NativeMethods.WS_EX_TRANSPARENT : style & ~NativeMethods.WS_EX_TRANSPARENT;
            NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        }

        //the lyrics finish loading while the pill is up, the chip follows
        private void updateStatus()
        {
            var current = lyrics.GetLyricsStatus(song?.TrackId);
            bool manual = lyrics.IsSetByHand(song?.TrackId);
            if (current == status && manual == setByHand) return;
            status = current;
            setByHand = manual;

            (statusText, statusDot) = current switch
            {
                SongLyricsStatus.Synced => ("Synced", Color.FromArgb(30, 215, 96)),
                SongLyricsStatus.Plain => ("Plain", Color.FromArgb(255, 196, 0)),
                SongLyricsStatus.Instrumental => ("Instrumental", Color.FromArgb(120, 160, 255)),
                SongLyricsStatus.NotFound => ("No lyrics", Color.FromArgb(255, 80, 80)),
                SongLyricsStatus.Failed => ("Offline", Color.FromArgb(255, 80, 80)),
                _ => ("Loading", Color.FromArgb(150, 150, 150)),
            };
            // what a click does, shown while the chip is hovered
            if (chipHovered)
            {
                statusText = current == SongLyricsStatus.Instrumental ? "Undo instrumental" : "Mark instrumental";
            }
            layout();
        }

        private void layout()
        {
            string title = song?.TrackName ?? "";
            string artist = song?.TrackArtists ?? "";
            var titleSize = measureGraphics.MeasureString(title, titleFont);
            titleWidth = titleSize.Width;
            var artistSize = measureGraphics.MeasureString(artist, artistFont);
            var statusTextSize = measureGraphics.MeasureString(statusText, statusFont);

            float statusWidth = PillStyle.StatusPaddingX * 2 + PillStyle.StatusDotSize + 5f + statusTextSize.Width;
            float textX = PillStyle.PaddingLeft + PillStyle.ThumbSize + PillStyle.ThumbGap;
            float fixedWidth = textX + PillStyle.StatusGap + statusWidth + PillStyle.PaddingRight;
            targetWidth = Math.Clamp(fixedWidth + Math.Max(titleSize.Width, artistSize.Width), PillStyle.MinWidth, PillStyle.MaxWidth);
            float textWidth = targetWidth - fixedWidth;

            thumbRect = new RectangleF(PillStyle.PaddingLeft, (PillStyle.Height - PillStyle.ThumbSize) / 2, PillStyle.ThumbSize, PillStyle.ThumbSize);

            float textHeight = titleSize.Height + PillStyle.LineGap + artistSize.Height;
            float textTop = (PillStyle.Height - textHeight) / 2;
            titleRect = new RectangleF(textX, textTop, textWidth, titleSize.Height);
            artistRect = new RectangleF(textX, textTop + titleSize.Height + PillStyle.LineGap, textWidth, artistSize.Height);

            statusRect = new RectangleF(targetWidth - PillStyle.PaddingRight - statusWidth, (PillStyle.Height - PillStyle.StatusHeight) / 2,
                statusWidth, PillStyle.StatusHeight);
        }

        //top left of the full size layout on the bitmap, the info is centered in the pill as it grows
        private PointF contentOrigin => new(area.Width / 2f - width / 2, pose.Top + (pose.Height - PillStyle.Height) / 2);

        private RectangleF pillRect => new(area.Width / 2f - pose.Width / 2, pose.Top, pose.Width, pose.Height);

        private void render()
        {
            if (area.Width <= 0 || area.Height <= 0) return;

            using var bitmap = new Bitmap(area.Width, area.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                if (song != null && pose.Width > 0 && pose.Height > 0)
                {
                    drawPill(g);
                }
            }

            setLayeredBitmap(bitmap);
        }

        private void drawPill(Graphics g)
        {
            var pill = pillRect;
            float radius = Math.Min(PillStyle.CornerRadius ?? pose.Height / 2, Math.Min(pose.Width, pose.Height) / 2);

            int shadowAlpha = (int)(PillStyle.ShadowAlpha * pose.Shadow);
            for (int i = PillStyle.ShadowLayers; i > 0 && shadowAlpha > 0; i--)
            {
                var shadow = pill;
                shadow.Inflate(i, i);
                shadow.Offset(0, PillStyle.ShadowOffsetY);
                using var shadowPath = roundedRect(shadow, radius + i);
                using var shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }

            using var path = roundedRect(pill, radius);
            using (var background = new SolidBrush(lerpColor(PillStyle.Nub, PillStyle.Background, pose.Fill)))
            {
                g.FillPath(background, path);
            }
            if (pose.Border > 0)
            {
                using var border = new Pen(withAlpha(PillStyle.Border, pose.Border), 1f);
                g.DrawPath(border, path);
            }

            //info is laid out for the full widget, centered in the current pill and clipped to it
            var state = g.Save();
            g.SetClip(path);
            var origin = contentOrigin;
            g.TranslateTransform(origin.X, origin.Y);

            drawPiece(g, thumbRect, pose.Thumb, p => drawThumb(g, p));
            drawPiece(g, titleRect, pose.Title, p =>
            {
                if (titleScrolls) drawMarquee(g, song?.TrackName ?? "", p);
                else drawText(g, song?.TrackName ?? "", titleFont, PillStyle.Title, titleRect, p);
            });
            drawPiece(g, artistRect, pose.Artist, p => drawText(g, song?.TrackArtists ?? "", artistFont, PillStyle.Artist, artistRect, p));
            drawPiece(g, statusRect, pose.Status, p => drawStatus(g, p));

            g.Restore(state);
        }

        //grows and rises into place while fading in
        private static void drawPiece(Graphics g, RectangleF rect, float progress, Action<float> draw)
        {
            if (progress <= 0) return;
            var state = g.Save();
            float scale = PillAnimation.lerp(PillStyle.ContentStartScale, 1, progress);
            float cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
            g.TranslateTransform(cx, cy + PillAnimation.lerp(PillStyle.ContentStartOffsetY, 0, progress));
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-cx, -cy);
            draw(progress);
            g.Restore(state);
        }

        private void drawThumb(Graphics g, float opacity)
        {
            using var thumbPath = roundedRect(thumbRect, PillStyle.ThumbRadius);
            if (cover == null)
            {
                using var placeholder = new SolidBrush(withAlpha(PillStyle.ThumbPlaceholder, opacity));
                g.FillPath(placeholder, thumbPath);
                return;
            }

            // filled with a texture instead of clipped, clipping isn't anti-aliased and leaves the circle jagged
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
            using var brush = new TextureBrush(cover, new Rectangle(0, 0, cover.Width, cover.Height), attributes) { WrapMode = WrapMode.Clamp };
            brush.TranslateTransform(thumbRect.X, thumbRect.Y);
            brush.ScaleTransform(thumbRect.Width / cover.Width, thumbRect.Height / cover.Height);
            g.FillPath(brush, thumbPath);
        }

        private bool titleScrolls => titleWidth > titleRect.Width;

        //one wait plus scrolling by the title and the gap
        private float marqueeCycleMs => PillStyle.MarqueeDelayMs + (titleWidth + PillStyle.MarqueeGap) / PillStyle.MarqueeSpeed * 1000;

        //the title drawn twice (title, gap, title) so the loop is seamless, faded at the edges
        private void drawMarquee(Graphics g, string text, float opacity)
        {
            float t = marqueeMs % marqueeCycleMs;
            float offset = t < PillStyle.MarqueeDelayMs ? 0 : (t - PillStyle.MarqueeDelayMs) * PillStyle.MarqueeSpeed / 1000;

            int w = (int)Math.Ceiling(titleRect.Width), h = (int)Math.Ceiling(titleRect.Height);
            if (w <= 0 || h <= 0) return;
            using var strip = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var sg = Graphics.FromImage(strip))
            {
                sg.TextRenderingHint = TextRenderingHint.AntiAlias;
                using var brush = new SolidBrush(PillStyle.Title);
                using var format = new StringFormat(StringFormatFlags.NoWrap);
                sg.DrawString(text, titleFont, brush, -offset, 0, format);
                sg.DrawString(text, titleFont, brush, -offset + titleWidth + PillStyle.MarqueeGap, 0, format);
            }
            // the left edge only fades while the text is moved off it
            fadeEdges(strip, offset > 0, true);

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
            g.DrawImage(strip, new Rectangle((int)titleRect.X, (int)titleRect.Y, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attributes);
        }

        private static void fadeEdges(Bitmap bitmap, bool left, bool right)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    float factor = 1f;
                    if (left) factor = Math.Min(factor, x / PillStyle.MarqueeFade);
                    if (right) factor = Math.Min(factor, (bitmap.Width - 1 - x) / PillStyle.MarqueeFade);
                    if (factor >= 1f) continue;
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        int alpha = y * data.Stride + x * 4 + 3;
                        pixels[alpha] = (byte)(pixels[alpha] * Math.Max(0f, factor));
                    }
                }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void drawText(Graphics g, string text, Font font, Color color, RectangleF rect, float opacity)
        {
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            using var brush = new SolidBrush(withAlpha(color, opacity));
            g.DrawString(text, font, brush, rect, format);
        }

        private void drawStatus(Graphics g, float opacity)
        {
            using (var chipPath = roundedRect(statusRect, statusRect.Height / 2))
            using (var chipBrush = new SolidBrush(withAlpha(chipHovered ? PillStyle.StatusHoverBackground : PillStyle.StatusBackground, opacity)))
            {
                g.FillPath(chipBrush, chipPath);
            }
            var dot = new RectangleF(statusRect.X + PillStyle.StatusPaddingX, statusRect.Y + (statusRect.Height - PillStyle.StatusDotSize) / 2,
                PillStyle.StatusDotSize, PillStyle.StatusDotSize);
            using (var dotBrush = new SolidBrush(withAlpha(statusDot, opacity)))
            {
                g.FillEllipse(dotBrush, dot);
            }
            var textRect = RectangleF.FromLTRB(dot.Right + 5f, statusRect.Y, statusRect.Right, statusRect.Bottom);
            using var format = new StringFormat(StringFormatFlags.NoWrap) { LineAlignment = StringAlignment.Center };
            using var brush = new SolidBrush(withAlpha(PillStyle.Title, opacity));
            g.DrawString(statusText, statusFont, brush, textRect, format);
        }

        private static Color withAlpha(Color c, float opacity) => Color.FromArgb((int)(c.A * Math.Clamp(opacity, 0f, 1f)), c);

        private static Color lerpColor(Color a, Color b, float p)
        {
            p = Math.Clamp(p, 0f, 1f);
            int mix(int x, int y) => (int)(x + (y - x) * p);
            return Color.FromArgb(mix(a.A, b.A), mix(a.R, b.R), mix(a.G, b.G), mix(a.B, b.B));
        }

        private static GraphicsPath roundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void setLayeredBitmap(Bitmap bitmap)
        {
            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

                var size = new Size(bitmap.Width, bitmap.Height);
                var source = new Point(0, 0);
                var pos = areaPos;
                var blend = new NativeMethods.BLENDFUNCTION
                {
                    BlendOp = NativeMethods.AC_SRC_OVER,
                    SourceConstantAlpha = 255,
                    AlphaFormat = NativeMethods.AC_SRC_ALPHA,
                };

                NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref source, 0, ref blend, NativeMethods.ULW_ALPHA);
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
            public const byte AC_SRC_OVER = 0x00;
            public const byte AC_SRC_ALPHA = 0x01;
            public const int ULW_ALPHA = 0x00000002;
            public const int GWL_EXSTYLE = -20;
            public const long WS_EX_TRANSPARENT = 0x20;

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
            public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
            public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

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
