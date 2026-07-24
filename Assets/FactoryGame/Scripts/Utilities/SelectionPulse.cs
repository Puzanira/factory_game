using UnityEngine;

namespace LastShift.Utilities
{
    /// <summary>Gentle scale pulse for selection highlights. Presentation only.</summary>
    public class SelectionPulse : MonoBehaviour
    {
        public Transform target;
        public float amount = 0.05f;
        public float speed = 3.5f;

        void Update()
        {
            if (target == null) return;
            float s = 1f + Mathf.Sin(Time.unscaledTime * speed) * amount;
            target.localScale = new Vector3(s, s, 1f);
        }
    }
}
