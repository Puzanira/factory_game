using UnityEngine;

namespace LastShift.Utilities
{
    /// <summary>
    /// Blocks the path-grid cells under a rectangle centred on this transform.
    /// Blocking is explicit (SetBlocked) so instantiation order never blocks
    /// the wrong cells; it always cleans up after itself on destroy/disable.
    /// </summary>
    public class GridBlocker : MonoBehaviour
    {
        [Tooltip("World-space size of the blocked rectangle, centred on this object.")]
        public Vector2 size = Vector2.one;

        bool applied;
        Rect appliedRect;

        public bool IsBlocking => applied;

        Rect CurrentRect => new Rect(
            transform.position.x - size.x * 0.5f,
            transform.position.y - size.y * 0.5f,
            size.x, size.y);

        public void SetBlocked(bool block)
        {
            if (block == applied) return;
            var grid = PathGrid.Instance;
            if (grid == null) return;
            if (block)
            {
                appliedRect = CurrentRect;
                grid.BlockRect(appliedRect, true);
                applied = true;
            }
            else
            {
                grid.BlockRect(appliedRect, false);
                applied = false;
            }
        }

        /// <summary>Re-applies blocking at the current position (call after moving).</summary>
        public void Reapply()
        {
            if (!applied) return;
            SetBlocked(false);
            SetBlocked(true);
        }

        void OnDisable() => SetBlocked(false);
        void OnDestroy() => SetBlocked(false);
    }
}
