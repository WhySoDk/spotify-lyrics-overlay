using System.Diagnostics;

namespace spotify_lyrics_overlay
{
    //picks a lyrics color from the album cover, ported from Shelltify's colorPicker.py
    //(https://github.com/WhySoDk/Shelltify): the most saturated color of the cover palette
    //that is neither too dark nor too white
    internal static class AlbumColor
    {
        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        // cover url -> picked color as hex, "" when the cover has no usable color.
        // kept apart from the lyrics so it doesn't wait for them, songs of one album share it
        private static readonly string CacheFilePath = Path.Combine("lyrics_cache", "album_colors.json");
        private static readonly object sync = new();
        private static Dictionary<string, string>? cache;

        //pick the color of the cover again on the next lookup
        public static void Forget(string url)
        {
            lock (sync)
            {
                if (LoadCache().Remove(url)) SaveCache();
            }
        }

        //null when the cover can't be loaded or has no usable color
        public static async Task<Color?> FromImageUrlAsync(string url)
        {
            lock (sync)
            {
                if (LoadCache().TryGetValue(url, out var hex))
                    return ColorHelper.IsValidHex(hex) ? ColorHelper.FromHex(hex, Color.White) : null;
            }

            try
            {
                var bytes = await httpClient.GetByteArrayAsync(url);
                // decoding and the palette run on a worker thread so the overlay keeps animating
                var color = await Task.Run(() =>
                {
                    using var stream = new MemoryStream(bytes);
                    using var image = new Bitmap(stream);
                    return Pick(ColorThief.GetPalette(image, colorCount: 5, quality: 10));
                });

                lock (sync)
                {
                    LoadCache()[url] = color is Color c ? ColorHelper.ToHex(c) : "";
                    SaveCache();
                }
                return color;
            }
            catch (Exception ex)
            {
                // not cached, tried again next time
                Debug.WriteLine($"Error loading album color: {ex.Message}");
                return null;
            }
        }

        private static Dictionary<string, string> LoadCache()
        {
            if (cache != null) return cache;
            try
            {
                cache = File.Exists(CacheFilePath)
                    ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CacheFilePath))
                    : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading album color cache: {ex.Message}");
            }
            return cache ??= new Dictionary<string, string>();
        }

        private static void SaveCache()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
                File.WriteAllText(CacheFilePath, System.Text.Json.JsonSerializer.Serialize(cache));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing album color cache: {ex.Message}");
            }
        }

        public static Color? Pick(List<Color> palette)
        {
            Color? picked = null;
            float best = 0f;
            foreach (var color in palette)
            {
                if (isWhite(color) || !isBright(color)) continue;

                float score = saturationTimesBrightness(color);
                if (picked == null || score > best)
                {
                    picked = color;
                    best = score;
                }
            }
            return picked;
        }

        //not too dark
        private static bool isBright(Color color, double threshold = 90)
        {
            return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B > threshold;
        }

        //too white
        private static bool isWhite(Color color, double threshold = 220)
        {
            return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B > threshold;
        }

        //brightness weights are biased toward blue on purpose
        private static float saturationTimesBrightness(Color color)
        {
            float saturation = ColorHelper.ToHsv(color).saturation;
            return saturation * (0.200f * color.R + 0.400f * color.G + 0.229f * color.B);
        }
    }

    //port of color-thief's modified median cut quantization (MMCQ),
    //https://github.com/fengsp/color-thief-py
    internal static class ColorThief
    {
        private const int SigBits = 5;
        private const int RShift = 8 - SigBits;
        private const int MaxIteration = 1000;
        private const double FractByPopulations = 0.75;

        public static List<Color> GetPalette(Bitmap image, int colorCount = 10, int quality = 10)
        {
            // every quality-th pixel, skipping transparent and almost white ones
            var pixels = new List<(int r, int g, int b)>();
            int pixelCount = image.Width * image.Height;
            for (int i = 0; i < pixelCount; i += quality)
            {
                var c = image.GetPixel(i % image.Width, i / image.Width);
                if (c.A >= 125 && !(c.R > 250 && c.G > 250 && c.B > 250))
                {
                    pixels.Add((c.R, c.G, c.B));
                }
            }
            if (pixels.Count == 0) return new List<Color>();

            return quantize(pixels, colorCount);
        }

        private static int colorIndex(int r, int g, int b) => (r << (2 * SigBits)) + (g << SigBits) + b;

        private class VBox
        {
            public int R1, R2, G1, G2, B1, B2;
            private readonly int[] histo;
            private int? count;

            public VBox(int r1, int r2, int g1, int g2, int b1, int b2, int[] histo)
            {
                R1 = r1; R2 = r2; G1 = g1; G2 = g2; B1 = b1; B2 = b2;
                this.histo = histo;
            }

            public VBox Copy() => new(R1, R2, G1, G2, B1, B2, histo);

            public int Volume => (R2 - R1 + 1) * (G2 - G1 + 1) * (B2 - B1 + 1);

            public int Count
            {
                get
                {
                    if (count == null)
                    {
                        int n = 0;
                        for (int i = R1; i <= R2; i++)
                            for (int j = G1; j <= G2; j++)
                                for (int k = B1; k <= B2; k++)
                                    n += histo[colorIndex(i, j, k)];
                        count = n;
                    }
                    return count.Value;
                }
            }

            //box bounds changed, count has to be recomputed
            public void Reset() => count = null;

            public Color Average
            {
                get
                {
                    int mult = 1 << (8 - SigBits);
                    double total = 0, rSum = 0, gSum = 0, bSum = 0;
                    for (int i = R1; i <= R2; i++)
                        for (int j = G1; j <= G2; j++)
                            for (int k = B1; k <= B2; k++)
                            {
                                int h = histo[colorIndex(i, j, k)];
                                total += h;
                                rSum += h * (i + 0.5) * mult;
                                gSum += h * (j + 0.5) * mult;
                                bSum += h * (k + 0.5) * mult;
                            }

                    if (total > 0)
                    {
                        return Color.FromArgb((int)(rSum / total), (int)(gSum / total), (int)(bSum / total));
                    }
                    return Color.FromArgb(
                        Math.Min(255, mult * (R1 + R2 + 1) / 2),
                        Math.Min(255, mult * (G1 + G2 + 1) / 2),
                        Math.Min(255, mult * (B1 + B2 + 1) / 2));
                }
            }
        }

        //list that pops the item with the largest key, like color-thief's PQueue
        private class PQueue
        {
            private readonly List<VBox> items = new();
            private readonly Func<VBox, double> key;
            private bool sorted;

            public PQueue(Func<VBox, double> key) => this.key = key;

            public int Size => items.Count;

            public void Push(VBox box)
            {
                items.Add(box);
                sorted = false;
            }

            public VBox Pop()
            {
                if (!sorted)
                {
                    // stable sort, same as python's list.sort
                    var ordered = items.OrderBy(key).ToList();
                    items.Clear();
                    items.AddRange(ordered);
                    sorted = true;
                }
                var last = items[^1];
                items.RemoveAt(items.Count - 1);
                return last;
            }
        }

        private static List<Color> quantize(List<(int r, int g, int b)> pixels, int maxColors)
        {
            var histo = new int[1 << (3 * SigBits)];
            int rMin = int.MaxValue, rMax = 0, gMin = int.MaxValue, gMax = 0, bMin = int.MaxValue, bMax = 0;
            foreach (var (r, g, b) in pixels)
            {
                int rv = r >> RShift, gv = g >> RShift, bv = b >> RShift;
                histo[colorIndex(rv, gv, bv)]++;
                rMin = Math.Min(rMin, rv); rMax = Math.Max(rMax, rv);
                gMin = Math.Min(gMin, gv); gMax = Math.Max(gMax, gv);
                bMin = Math.Min(bMin, bv); bMax = Math.Max(bMax, bv);
            }

            var queue = new PQueue(box => box.Count);
            queue.Push(new VBox(rMin, rMax, gMin, gMax, bMin, bMax, histo));

            // first split by population, then by population times volume
            iterate(queue, histo, FractByPopulations * maxColors);

            var queue2 = new PQueue(box => (double)box.Count * box.Volume);
            while (queue.Size > 0) queue2.Push(queue.Pop());
            iterate(queue2, histo, maxColors - queue2.Size);

            var palette = new List<Color>();
            while (queue2.Size > 0) palette.Add(queue2.Pop().Average);
            return palette;
        }

        private static void iterate(PQueue queue, int[] histo, double target)
        {
            int colors = 1;
            int iterations = 0;
            while (iterations < MaxIteration)
            {
                var box = queue.Pop();
                if (box.Count == 0)
                {
                    queue.Push(box);
                    iterations++;
                    continue;
                }

                var (box1, box2) = medianCut(histo, box);
                if (box1 == null) return;

                queue.Push(box1);
                if (box2 != null)
                {
                    queue.Push(box2);
                    colors++;
                }
                if (colors >= target) return;
                iterations++;
            }
        }

        private static (VBox?, VBox?) medianCut(int[] histo, VBox box)
        {
            if (box.Count == 0) return (null, null);
            if (box.Count == 1) return (box.Copy(), null);

            int rw = box.R2 - box.R1 + 1, gw = box.G2 - box.G1 + 1, bw = box.B2 - box.B1 + 1;
            int maxw = Math.Max(rw, Math.Max(gw, bw));

            // cut along the longest side, 0 = red, 1 = green, 2 = blue
            int axis = maxw == rw ? 0 : maxw == gw ? 1 : 2;
            int lo = axis == 0 ? box.R1 : axis == 1 ? box.G1 : box.B1;
            int hi = axis == 0 ? box.R2 : axis == 1 ? box.G2 : box.B2;

            var partialSum = new Dictionary<int, int>();
            int total = 0;
            for (int i = lo; i <= hi; i++)
            {
                int sum = 0;
                for (int j = axis == 0 ? box.G1 : box.R1; j <= (axis == 0 ? box.G2 : box.R2); j++)
                    for (int k = axis == 2 ? box.G1 : box.B1; k <= (axis == 2 ? box.G2 : box.B2); k++)
                    {
                        int index = axis switch
                        {
                            0 => colorIndex(i, j, k),
                            1 => colorIndex(j, i, k),
                            _ => colorIndex(j, k, i),
                        };
                        sum += histo[index];
                    }
                total += sum;
                partialSum[i] = total;
            }
            var lookaheadSum = partialSum.ToDictionary(pair => pair.Key, pair => total - pair.Value);

            for (int i = lo; i <= hi; i++)
            {
                if (partialSum[i] <= total / 2.0) continue;

                int left = i - lo;
                int right = hi - i;
                int d2 = left <= right
                    ? Math.Min(hi - 1, (int)(i + right / 2.0))
                    : Math.Max(lo, (int)(i - 1 - left / 2.0));

                // avoid 0-count boxes
                while (partialSum.GetValueOrDefault(d2) == 0) d2++;
                int count2 = lookaheadSum.GetValueOrDefault(d2);
                while (count2 == 0 && partialSum.GetValueOrDefault(d2 - 1) != 0)
                {
                    d2--;
                    count2 = lookaheadSum.GetValueOrDefault(d2);
                }

                var box1 = box.Copy();
                var box2 = box.Copy();
                setRange(box1, axis, null, d2);
                setRange(box2, axis, d2 + 1, null);
                return (box1, box2);
            }
            return (null, null);
        }

        private static void setRange(VBox box, int axis, int? from, int? to)
        {
            switch (axis)
            {
                case 0: box.R1 = from ?? box.R1; box.R2 = to ?? box.R2; break;
                case 1: box.G1 = from ?? box.G1; box.G2 = to ?? box.G2; break;
                default: box.B1 = from ?? box.B1; box.B2 = to ?? box.B2; break;
            }
            box.Reset();
        }
    }
}
