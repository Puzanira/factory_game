using UnityEngine;
using LastShift.UI;

namespace LastShift.Core
{
    /// <summary>
    /// Boot scene: launches the keyboard-only intro flow
    /// (title card «ПОСЛЕДНЯЯ СМЕНА» → four-page briefing → Level 1).
    /// The player cannot reach gameplay before finishing or explicitly skipping
    /// the briefing; gameplay input only exists once Level 1 loads.
    /// </summary>
    public class BootController : MonoBehaviour
    {
        void Start()
        {
            GameManager.Ensure();
            if (GetComponent<IntroFlowUI>() == null)
                gameObject.AddComponent<IntroFlowUI>();
        }
    }
}
