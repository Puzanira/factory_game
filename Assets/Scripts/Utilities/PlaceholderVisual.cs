using UnityEngine;

namespace LastShift.Utilities
{
    /// <summary>
    /// Declarative placeholder visual: stores shape/color/size and builds the actual
    /// SpriteRenderer at runtime (so prefabs never reference non-asset sprites).
    /// Intended for dedicated visual child objects.
    /// </summary>
    public class PlaceholderVisual : MonoBehaviour
    {
        [Tooltip("Which generated placeholder shape to display.")]
        public PlaceholderShape shape = PlaceholderShape.Square;
        public Color color = Color.white;
        public Vector2 size = Vector2.one;
        public int sortingOrder = 0;

        SpriteRenderer sr;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Apply();
        }

        public void Apply()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Get(shape);
            sr.sharedMaterial = SpriteFactory.UnlitMaterial;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            transform.localScale = new Vector3(size.x, size.y, 1f);
        }
    }
}
