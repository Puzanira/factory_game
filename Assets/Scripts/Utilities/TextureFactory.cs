using System.Collections.Generic;
using UnityEngine;

namespace LastShift.Utilities
{
    /// <summary>
    /// Procedurally generated, cached texture sprites for the industrial art pass:
    /// tiling floor panels, hazard stripes, conveyor belts, scanlines, soft glows,
    /// puddles. All original, project-owned, no external downloads.
    /// </summary>
    public static class TextureFactory
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Cached(string key, System.Func<Sprite> build)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = build();
            cache[key] = s;
            return s;
        }

        static Sprite MakeSprite(Texture2D tex, float ppu)
        {
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        static Texture2D NewTex(int w, int h, bool repeat)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return tex;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        /// <summary>Tiling metal floor panel: seams, corner bolts, subtle grime. 1 tile = 2x2 world units at PPU 64.</summary>
        public static Sprite FloorTile(Color baseColor, int seed = 7)
        {
            return Cached("floor" + baseColor + seed, () =>
            {
                const int S = 128;
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float n = Hash(x / 4, y / 4, seed) * 0.5f + Hash(x / 16, y / 16, seed + 3) * 0.5f;
                        float v = 0.92f + n * 0.14f; // subtle noise
                        Color c = baseColor * v;

                        // Panel seams along tile edges + a middle cross seam.
                        int ex = Mathf.Min(x, S - 1 - x);
                        int ey = Mathf.Min(y, S - 1 - y);
                        int mx = Mathf.Abs(x - S / 2);
                        int my = Mathf.Abs(y - S / 2);
                        if (ex < 2 || ey < 2) c *= 0.62f;
                        else if (mx < 1 || my < 1) c *= 0.78f;

                        // Corner bolts on each quarter panel.
                        foreach (var b in BoltCenters)
                        {
                            float dx = x - b.x, dy = y - b.y;
                            float d = dx * dx + dy * dy;
                            if (d < 6f) c *= 0.55f;
                            else if (d < 11f) c *= 1.18f;
                        }

                        // Sparse stains.
                        if (Hash(x / 22, y / 22, seed + 9) > 0.87f) c *= 0.88f;

                        c.a = 1f;
                        px[y * S + x] = c;
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, 64f);
            });
        }

        static readonly Vector2Int[] BoltCenters =
        {
            new Vector2Int(10, 10), new Vector2Int(118, 10), new Vector2Int(10, 118), new Vector2Int(118, 118),
            new Vector2Int(54, 10), new Vector2Int(74, 118), new Vector2Int(10, 54), new Vector2Int(118, 74),
        };

        /// <summary>Diagonal industrial yellow/black hazard stripes (tiling).</summary>
        public static Sprite HazardStripes()
        {
            return Cached("stripes", () =>
            {
                const int S = 64;
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                var yellow = new Color32(212, 172, 60, 255);
                var black = new Color32(26, 24, 20, 255);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                        px[y * S + x] = (((x + y) / 12) % 2 == 0) ? yellow : black;
                tex.SetPixels32(px);
                return MakeSprite(tex, 64f);
            });
        }

        /// <summary>Tiling conveyor belt: dark rubber with roller slats.</summary>
        public static Sprite BeltTexture()
        {
            return Cached("belt", () =>
            {
                const int S = 64;
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float n = Hash(x / 3, y / 3, 5) * 0.06f;
                        float v = 0.16f + n;
                        // Slats every 16px.
                        int sx = x % 16;
                        if (sx < 2) v *= 0.55f;
                        else if (sx == 2) v *= 1.5f;
                        byte g = (byte)(Mathf.Clamp01(v) * 255);
                        px[y * S + x] = new Color32(g, (byte)(g + 2), (byte)(g + 6), 255);
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, 64f);
            });
        }

        /// <summary>Soft radial gradient circle: glows, shadows, fog particles.</summary>
        public static Sprite SoftCircle()
        {
            return Cached("soft", () =>
            {
                const int S = 64;
                var tex = NewTex(S, S, false);
                var px = new Color32[S * S];
                float half = S * 0.5f;
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a; // softer falloff
                        px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, 64f);
            });
        }

        /// <summary>Irregular puddle blob with soft edges (for wet floors).</summary>
        public static Sprite Puddle(int seed)
        {
            return Cached("puddle" + seed, () =>
            {
                const int S = 64;
                var tex = NewTex(S, S, false);
                var px = new Color32[S * S];
                float half = S * 0.5f;
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float ang = Mathf.Atan2(y - half, x - half);
                        float wobble = 0.78f + 0.22f * Mathf.Sin(ang * 3f + seed) * Mathf.Cos(ang * 2f - seed);
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / (half * wobble);
                        float a = Mathf.Clamp01(1f - d * d);
                        px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(1f, a * 1.6f) * 200));
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, 64f);
            });
        }

        /// <summary>Tiling horizontal CRT scanlines (for the terminal UI).</summary>
        public static Sprite Scanlines()
        {
            return Cached("scan", () =>
            {
                const int S = 8;
                var tex = NewTex(S, S, true);
                tex.filterMode = FilterMode.Point;
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                        px[y * S + x] = (y % 4 == 0)
                            ? new Color32(0, 0, 0, 70)
                            : new Color32(0, 0, 0, 0);
                tex.SetPixels32(px);
                return MakeSprite(tex, 64f);
            });
        }

        /// <summary>Vertical metal wall slab with a top highlight and rivets (tiling horizontally).</summary>
        public static Sprite WallTexture(Color baseColor)
        {
            return Cached("wall" + baseColor, () =>
            {
                const int S = 64;
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float n = Hash(x / 5, y / 5, 21) * 0.10f;
                        float v = 0.9f + n;
                        Color c = baseColor * v;
                        if (y > S - 6) c *= 1.35f;          // top edge highlight (pseudo-depth)
                        else if (y < 5) c *= 0.6f;          // bottom shadow line
                        if (x % 32 < 1) c *= 0.72f;         // vertical panel seams
                        if ((x % 32 == 16) && (y % 24 < 2)) c *= 0.5f; // rivets
                        c.a = 1f;
                        px[y * S + x] = c;
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, 64f);
            });
        }
    }
}
