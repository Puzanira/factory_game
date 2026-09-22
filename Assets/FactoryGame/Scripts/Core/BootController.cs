using UnityEngine;
using LastShift.UI;

namespace LastShift.Core
{
    /// <summary>
    /// Boot scene: launches the arcade intro flow
    /// (title card «ПОСЛЕДНЯЯ СМЕНА» → one briefing page → «ВВОДНЫЙ УРОК» → Level 1).
    /// The player cannot reach gameplay before the briefing; gameplay input only
    /// exists once Level 1 loads.
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
