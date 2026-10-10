namespace spotify_lyrics_overlay.Pill
{
    //pill state for one frame, the pill is centered horizontally
    internal struct PillPose
    {
        public float Top;
        public float Width;
        public float Height;
        //0 = black nub color, 1 = widget background
        public float Fill;
        public float Shadow;
        public float Border;
        //reveal progress of each piece of info, 0 hidden, 1 shown
        public float Thumb, Title, Artist, Status;
    }

    //the whole animation as a function of time, no state
    internal static class PillAnimation
    {
        static float popAt => PillStyle.PushMs + PillStyle.HoldMs;
        static float expandAt => popAt + PillStyle.ExpandDelayMs;
        static float contentAt => expandAt + PillStyle.ContentDelayMs;

        //everything is in place, hovering can hold the pill from here
        public static float SettledAt => contentAt + 3 * PillStyle.StaggerMs + PillStyle.ContentFadeMs + 100;
        //the info starts fading out
        public static float ExitAt => popAt + PillStyle.DisplayMs;
        //the widget starts shrinking back to the nub, too late to hold it
        public static float CollapseAt => ExitAt + PillStyle.ContentOutMs;
        static float riseAt => CollapseAt + PillStyle.CollapseMs;
        static float retractAt => riseAt + PillStyle.RiseMs;

        public static float TotalMs => retractAt + PillStyle.RetractMs;

        static float squeezedWidth => PillStyle.NubWidth * PillStyle.SqueezeX;
        static float squeezedHeight => PillStyle.NubHeight * PillStyle.StretchY;
        //fully tucked behind the top of the screen
        static float hiddenTop => -squeezedHeight - 2f;

        public static PillPose At(float ms, float fullWidth)
        {
            if (ms < PillStyle.PushMs)
            {
                //squeezes out of the edge in fits and starts
                float p = keyframes(PillStyle.PushKeys, ms / PillStyle.PushMs);
                return nub(lerp(hiddenTop, PillStyle.StuckTop, p));
            }

            if (ms < popAt)
            {
                //stuck, trembling
                float shake = MathF.Sin((ms - PillStyle.PushMs) * 2 * MathF.PI / PillStyle.TremblePeriodMs) * PillStyle.TrembleAmount;
                return nub(PillStyle.StuckTop + shake);
            }

            if (ms < ExitAt)
            {
                return shown(ms, fullWidth);
            }

            return exit(ms, fullWidth);
        }

        static PillPose nub(float top) => new()
        {
            Top = top,
            Width = squeezedWidth,
            Height = squeezedHeight,
        };

        //popped free, then expanding and revealing the info
        static PillPose shown(float ms, float fullWidth)
        {
            float drop = spring(ms - popAt, PillStyle.DropSpring);
            //overshoot of this spring makes the nub briefly wider and flatter than round
            float jelly = spring(ms - popAt, PillStyle.JellySpring);
            float growW = spring(ms - expandAt, PillStyle.ExpandWidthSpring);
            float growH = spring(ms - expandAt - PillStyle.HeightLagMs, PillStyle.ExpandHeightSpring);
            float fill = clamp01((ms - expandAt) / PillStyle.ExpandFillMs);

            float thumb = reveal(ms, 0), title = reveal(ms, 1), artist = reveal(ms, 2), status = reveal(ms, 3);

            return new PillPose
            {
                Top = lerp(PillStyle.StuckTop, PillStyle.RestTop, drop),
                Width = lerp(squeezedWidth, PillStyle.NubWidth, jelly) + (fullWidth - PillStyle.NubWidth) * growW,
                Height = lerp(squeezedHeight, PillStyle.NubHeight, jelly) + (PillStyle.Height - PillStyle.NubHeight) * growH,
                Fill = fill,
                Shadow = fill,
                Border = status,
                Thumb = thumb,
                Title = title,
                Artist = artist,
                Status = status,
            };
        }

        static float reveal(float ms, int index) =>
            easeOutCubic(clamp01((ms - contentAt - index * PillStyle.StaggerMs) / PillStyle.ContentFadeMs));

        //info fades, widget shrinks to the nub, nub rises into the edge and gets sucked in
        static PillPose exit(float ms, float fullWidth)
        {
            var from = shown(ExitAt, fullWidth);
            var pose = from;

            //info out, last piece first
            float fade = clamp01((ms - ExitAt) / PillStyle.ContentOutMs);
            pose.Status = from.Status * (1 - easeInCubic(clamp01(fade * 1.6f)));
            pose.Artist = from.Artist * (1 - easeInCubic(clamp01(fade * 1.6f - 0.2f)));
            pose.Title = from.Title * (1 - easeInCubic(clamp01(fade * 1.6f - 0.4f)));
            pose.Thumb = from.Thumb * (1 - easeInCubic(clamp01(fade * 1.6f - 0.6f)));
            pose.Border = pose.Status;
            if (ms < CollapseAt) return pose;

            //back to the black nub
            float collapse = spring(ms - CollapseAt, PillStyle.CollapseSpring);
            pose.Width = lerp(from.Width, PillStyle.NubWidth, collapse);
            pose.Height = lerp(from.Height, PillStyle.NubHeight, collapse);
            pose.Fill = pose.Shadow = 1 - clamp01((ms - CollapseAt) / PillStyle.CollapseMs);
            if (ms < riseAt) return pose;

            //rises to the edge, squeezing as it goes
            float rise = easeInOutCubic(clamp01((ms - riseAt) / PillStyle.RiseMs));
            pose.Top = lerp(from.Top, PillStyle.StuckTop, rise);
            pose.Width *= lerp(1, PillStyle.SqueezeX, rise);
            pose.Height *= lerp(1, PillStyle.StretchY, rise);
            if (ms < retractAt) return pose;

            //sucked in
            float retract = easeInCubic(clamp01((ms - retractAt) / PillStyle.RetractMs));
            pose.Top = lerp(PillStyle.StuckTop, hiddenTop, retract);
            return pose;
        }

        //damped spring from 0 to 1 over t ms, overshoots when damping < 1
        static float spring(float ms, Spring s)
        {
            if (ms <= 0) return 0;
            double t = ms / 1000.0;
            double omega = 2 * Math.PI / s.Response;
            double zeta = s.Damping;
            if (zeta >= 1)
            {
                return (float)(1 - (1 + omega * t) * Math.Exp(-omega * t));
            }
            double omegaD = omega * Math.Sqrt(1 - zeta * zeta);
            double decay = Math.Exp(-zeta * omega * t);
            return (float)(1 - decay * (Math.Cos(omegaD * t) + zeta * omega / omegaD * Math.Sin(omegaD * t)));
        }

        //eased in and out between keyframes
        static float keyframes((float Time, float Progress)[] keys, float t)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (t <= keys[i].Time)
                {
                    var a = keys[i - 1];
                    var b = keys[i];
                    float p = b.Time > a.Time ? (t - a.Time) / (b.Time - a.Time) : 1;
                    return lerp(a.Progress, b.Progress, easeInOutCubic(p));
                }
            }
            return keys[^1].Progress;
        }

        public static float lerp(float a, float b, float p) => a + (b - a) * p;
        static float clamp01(float p) => Math.Clamp(p, 0f, 1f);
        static float easeOutCubic(float p) => 1 - MathF.Pow(1 - p, 3);
        static float easeInCubic(float p) => p * p * p;
        static float easeInOutCubic(float p) => p < 0.5f ? 4 * p * p * p : 1 - MathF.Pow(-2 * p + 2, 3) / 2;
    }
}
