namespace spotify_lyrics_overlay.Pill
{
    //timing, springs, sizes and colors of the song pill, tuned in the pill prototype
    internal static class PillStyle
    {
        // ================= 1. PUSH: black nub struggles out of the top edge =================

        //nub size once it's free (plain black pill, no border, no info)
        public const float NubWidth = 110f;
        public const float NubHeight = 34f;
        //shape while squeezing through the edge: narrower and taller
        public const float SqueezeX = 0.78f;
        public const float StretchY = 1.3f;

        public const int PushMs = 520;
        //push progress keyframes (time 0..1, progress 0..1), a dip back = it slips and tries again
        public static readonly (float Time, float Progress)[] PushKeys =
        {
            (0f, 0f),
            (0.38f, 0.62f),
            (0.55f, 0.48f),
            (1f, 1f),
        };
        //how far the nub top sits above the screen edge while stuck (negative = partly hidden)
        public const float StuckTop = -16f;

        // ================= 2. STRAIN: stuck at the edge, trembling =================

        public const int HoldMs = 150;
        //px of shake while stuck, 0 turns it off
        public const float TrembleAmount = 0.8f;
        public const float TremblePeriodMs = 34f;

        // ================= 3. POP: lets go and drops, jelly wobble =================

        //distance from the top of the screen once settled
        public const float RestTop = 28f;
        //springs are SwiftUI style: response = seconds per oscillation, damping 1 = no bounce
        public static readonly Spring DropSpring = new(0.46f, 0.62f);
        //squeezed shape -> round nub, low damping overshoots into a wide flat blob
        public static readonly Spring JellySpring = new(0.34f, 0.42f);

        // ================= 4. EXPAND: nub forms into the widget =================

        //delay after the pop before growing
        public const int ExpandDelayMs = 230;
        public static readonly Spring ExpandWidthSpring = new(0.52f, 0.72f);
        public static readonly Spring ExpandHeightSpring = new(0.46f, 0.8f);
        //height starts after the width
        public const int HeightLagMs = 60;
        //black nub -> widget background color, shadow fades in alongside
        public const int ExpandFillMs = 300;

        // ================= 5. REVEAL: info fades in one by one =================

        //delay after the expand starts
        public const int ContentDelayMs = 170;
        //gap between thumbnail, title, artist, status
        public const int StaggerMs = 65;
        public const int ContentFadeMs = 240;
        //each piece grows from this scale and rises from this offset
        public const float ContentStartScale = 0.82f;
        public const float ContentStartOffsetY = 6f;

        // ================= 6. DISPLAY =================

        //time on screen counted from the pop
        public const int DisplayMs = 3200;
        //while hovered the pill stays, it carries on this long after the cursor leaves
        public const int HoverGraceMs = 1000;
        //the width glides to a new size when the status changes
        public const float WidthSmoothingSeconds = 0.06f;

        // ================= 7. EXIT: reverse =================

        public const int ContentOutMs = 160;
        //widget shrinks back to the black nub
        public const int CollapseMs = 300;
        public static readonly Spring CollapseSpring = new(0.38f, 0.86f);
        //nub rises back to the edge and gets squeezed
        public const int RiseMs = 240;
        //then gets sucked in
        public const int RetractMs = 170;

        // ---------- widget layout ----------

        public const float Height = 64f;
        public const float MinWidth = 300f;
        public const float MaxWidth = 520f;
        //null = fully round ends (height / 2)
        public static readonly float? CornerRadius = null;
        public const float PaddingLeft = 10f;
        public const float PaddingRight = 16f;

        public const float ThumbSize = 44f;
        public const float ThumbRadius = 22f;
        public const float ThumbGap = 12f;

        public const string FontName = "Segoe UI";
        public const float TitleSize = 11.5f;
        public const float ArtistSize = 9.5f;
        //vertical gap between title and artist
        public const float LineGap = -1f;

        //a title too long for the pill scrolls: waits, scrolls by one title plus the gap, repeats
        public const int MarqueeDelayMs = 900;
        //px per second
        public const float MarqueeSpeed = 70f;
        public const float MarqueeGap = 40f;
        //px of fade at the edges
        public const float MarqueeFade = 18f;
        //the pill stays until the title scrolled through once, but no longer than this past its usual time
        public const int MarqueeMaxHoldMs = 10000;

        //lyrics status chip on the right
        public const float StatusGap = 14f;
        public const float StatusSize = 8.5f;
        public const float StatusDotSize = 7f;
        public const float StatusPaddingX = 9f;
        public const float StatusHeight = 22f;

        // ---------- colors ----------

        public static readonly Color Nub = Color.FromArgb(255, 0, 0, 0);
        public static readonly Color Background = Color.FromArgb(240, 16, 16, 18);
        public static readonly Color Border = Color.FromArgb(38, 255, 255, 255);
        public static readonly Color Title = Color.FromArgb(255, 255, 255, 255);
        public static readonly Color Artist = Color.FromArgb(160, 255, 255, 255);
        public static readonly Color StatusBackground = Color.FromArgb(30, 255, 255, 255);
        //status chip under the cursor when it can be clicked
        public static readonly Color StatusHoverBackground = Color.FromArgb(60, 255, 255, 255);
        //stands in for the album cover while it loads
        public static readonly Color ThumbPlaceholder = Color.FromArgb(40, 255, 255, 255);

        //fake soft shadow under the widget (GDI+ has no blur), 0 turns it off
        public const int ShadowLayers = 8;
        public const float ShadowOffsetY = 4f;
        public const int ShadowAlpha = 10;
    }

    internal record Spring(float Response, float Damping);
}
