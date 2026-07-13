using System.Collections.Generic;
using UnityEngine;

namespace LastShift.Utilities
{
    public enum PlaceholderShape { Square, Circle, Ring, Arrow, Diamond }

    /// <summary>
    /// Generates and caches simple placeholder sprites (squares, circles, rings, arrows)
    /// at runtime so the project needs no imported art assets.
    /// </summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<PlaceholderShape, Sprite> cache = new Dictionary<PlaceholderShape, Sprite>();
        static Material unlitMaterial;
        static Material litMaterial;

        /// <summary>Shared unlit sprite material (UI glows, previews, overlays).</summary>
        public static Material UnlitMaterial
        {
            get
            {
                if (unlitMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    unlitMaterial = new Material(shader);
                }
                return unlitMaterial;
            }
        }

        /// <summary>Shared lit sprite material so world objects react to 2D lights. Falls back to unlit.</summary>
        public static Material LitMaterial
        {
            get
            {
                if (litMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                    litMaterial = shader != null ? new Material(shader) : UnlitMaterial;
                }
                return litMaterial;
            }
        }

        public static Sprite Get(PlaceholderShape shape)
        {
            if (cache.TryGetValue(shape, out Sprite s) && s != null) return s;
            s = Build(shape);
            cache[shape] = s;
            return s;
        }

        static Sprite Build(PlaceholderShape shape)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    bool on = false;
                    switch (shape)
                    {
                        case PlaceholderShape.Square:
                            on = true;
                            break;
                        case PlaceholderShape.Circle:
                            on = dx * dx + dy * dy <= (half - 1f) * (half - 1f);
                            break;
                        case PlaceholderShape.Ring:
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            on = d <= half - 1f && d >= half - 7f;
                            break;
                        case PlaceholderShape.Arrow:
                            // Triangle pointing +X.
                            float t = (x + 0.5f) / size;                 // 0..1 across width
                            float spread = (1f - t) * half * 0.9f;       // wide at left, point at right
                            on = Mathf.Abs(dy) <= spread && t > 0.05f;
                            break;
                        case PlaceholderShape.Diamond:
                            on = Mathf.Abs(dx) + Mathf.Abs(dy) <= half - 1f;
                            break;
                    }
                    pixels[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            // pixelsPerUnit == texture size -> sprite is exactly 1x1 world units.
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }

    /// <summary>Small helpers for building placeholder visuals in code.</summary>
    public static class Viz
    {
        public static SpriteRenderer Make(string name, Transform parent, PlaceholderShape shape, Color color,
            Vector2 localPos, Vector2 size, int sortingOrder, bool unlit = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Get(shape);
            sr.sharedMaterial = unlit ? SpriteFactory.UnlitMaterial : SpriteFactory.LitMaterial;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>Tiled sprite renderer (floor/wall/belt textures) covering the given world size.</summary>
        public static SpriteRenderer MakeTiled(string name, Transform parent, Sprite sprite, Color color,
            Vector2 localPos, Vector2 size, int sortingOrder, bool unlit = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sharedMaterial = unlit ? SpriteFactory.UnlitMaterial : SpriteFactory.LitMaterial;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        public static LineRenderer MakeLine(string name, Transform parent, Color color, float width, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = SpriteFactory.UnlitMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.widthMultiplier = width;
            lr.useWorldSpace = true;
            lr.sortingOrder = sortingOrder;
            return lr;
        }

        public static Rect RectAt(Vector2 center, Vector2 size) =>
            new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);

        /// <summary>True when the named sorting layer exists in the project.</summary>
        public static bool HasSortingLayer(string layerName)
        {
            foreach (var l in SortingLayer.layers)
                if (l.name == layerName) return true;
            return false;
        }

        /// <summary>Assign a sorting layer by name; silently keeps Default when missing.</summary>
        public static void SetLayer(Renderer r, string layerName)
        {
            if (r != null && HasSortingLayer(layerName)) r.sortingLayerName = layerName;
        }
    }
}
