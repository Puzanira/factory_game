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

        /// <summary>
        /// Texels per world unit for the tiling world textures. The camera fits a room
        /// into the screen, which puts a world unit at ~77 px on a 1920x1080 monitor
        /// (Level 1: ortho 7.03 -> 1080 / 14.06). At the old 64 these textures were
        /// being magnified ~1.2x — every floor seam, rivet and hazard stripe was
        /// slightly soft. 128 covers 1080p with headroom and still reads at 1440p.
        /// Each texture keeps its world size: the pixel count and the pixelsPerUnit
        /// move together, and pattern periods scale with them.
        /// </summary>
        const int Density = 128;

        static Sprite Cached(string key, System.Func<Sprite> build)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = build();
            cache[key] = s;
            return s;
        }

        static Sprite MakeSprite(Texture2D tex, float ppu)
        {
            // No mipmaps here, and nothing reads the pixels back: free the CPU copy.
            tex.Apply(false, true);
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
                const int S = Density * 2;          // the tile stays 2x2 world units
                const int K = S / 128;              // scale of every pattern period
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float n = Hash(x / (4 * K), y / (4 * K), seed) * 0.5f
                                + Hash(x / (16 * K), y / (16 * K), seed + 3) * 0.5f;
                        float v = 0.92f + n * 0.14f; // subtle noise
                        Color c = baseColor * v;

                        // Panel seams along tile edges + a middle cross seam.
                        int ex = Mathf.Min(x, S - 1 - x);
                        int ey = Mathf.Min(y, S - 1 - y);
                        int mx = Mathf.Abs(x - S / 2);
                        int my = Mathf.Abs(y - S / 2);
                        if (ex < 2 * K || ey < 2 * K) c *= 0.62f;
                        else if (mx < K || my < K) c *= 0.78f;

                        // Corner bolts on each quarter panel (radii are squared distances,
                        // so they scale with K^2).
                        foreach (var b in BoltCenters)
                        {
                            float dx = x - b.x * K, dy = y - b.y * K;
                            float d = dx * dx + dy * dy;
                            if (d < 6f * K * K) c *= 0.55f;
                            else if (d < 11f * K * K) c *= 1.18f;
                        }

                        // Sparse stains.
                        if (Hash(x / (22 * K), y / (22 * K), seed + 9) > 0.87f) c *= 0.88f;

                        c.a = 1f;
                        px[y * S + x] = c;
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, Density);
            });
        }

        /// <summary>Bolt centres in the original 128-px tile space; scaled by K at use.</summary>
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
                const int S = Density;              // 1x1 world unit, as before
                const int K = S / 64;               // stripe period scales with the size
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                var yellow = new Color32(212, 172, 60, 255);
                var black = new Color32(26, 24, 20, 255);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                        px[y * S + x] = (((x + y) / (12 * K)) % 2 == 0) ? yellow : black;
                tex.SetPixels32(px);
                return MakeSprite(tex, Density);
            });
        }

        /// <summary>Tiling conveyor belt: dark rubber with roller slats.</summary>
        public static Sprite BeltTexture()
        {
            return Cached("belt", () =>
            {
                const int S = Density;              // 1x1 world unit, as before
                const int K = S / 64;
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float n = Hash(x / (3 * K), y / (3 * K), 5) * 0.06f;
                        float v = 0.16f + n;
                        // Slats every 16px of the original tile.
                        int sx = x % (16 * K);
                        if (sx < 2 * K) v *= 0.55f;
                        else if (sx < 3 * K) v *= 1.5f;
                        byte g = (byte)(Mathf.Clamp01(v) * 255);
                        px[y * S + x] = new Color32(g, (byte)(g + 2), (byte)(g + 6), 255);
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, Density);
            });
        }

        /// <summary>Soft radial gradient circle: glows, shadows, fog particles.</summary>
        public static Sprite SoftCircle()
        {
            return Cached("soft", () =>
            {
                // Glows are stretched over metres of the room, so the gradient needs
                // enough samples not to band or read as a pixelated blob.
                const int S = 256;
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
                return MakeSprite(tex, S);
            });
        }

        /// <summary>Irregular puddle blob with soft edges (for wet floors).</summary>
        public static Sprite Puddle(int seed)
        {
            return Cached("puddle" + seed, () =>
            {
                const int S = Density;              // 1x1 world unit, as before
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
                return MakeSprite(tex, Density);
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
                const int S = Density;              // 1x1 world unit, as before
                const int K = S / 64;
                var tex = NewTex(S, S, true);
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float n = Hash(x / (5 * K), y / (5 * K), 21) * 0.10f;
                        float v = 0.9f + n;
                        Color c = baseColor * v;
                        int sx = x % (32 * K);
                        if (y > S - 6 * K) c *= 1.35f;      // top edge highlight (pseudo-depth)
                        else if (y < 5 * K) c *= 0.6f;      // bottom shadow line
                        if (sx < K) c *= 0.72f;             // vertical panel seams
                        if (sx >= 16 * K && sx < 17 * K && y % (24 * K) < 2 * K) c *= 0.5f; // rivets
                        c.a = 1f;
                        px[y * S + x] = c;
                    }
                }
                tex.SetPixels32(px);
                return MakeSprite(tex, Density);
            });
        }
    }
}
