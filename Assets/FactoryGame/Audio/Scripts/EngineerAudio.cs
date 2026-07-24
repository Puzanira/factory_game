using UnityEngine;
using LastShift.Data;
using LastShift.Engineer;

namespace LastShift.Audio
{
    /// <summary>
    /// Non-verbal engineer sound layer: distance-driven footsteps on metal (never a
    /// timer, so speed changes read naturally), quiet tool ticks while repairing,
    /// stressed breathing during panic, and a short electric reaction on stun.
    /// Attached by EngineerController.Init; everything routes to the Engineer bus.
    /// </summary>
    public class EngineerAudio : MonoBehaviour
    {
        EngineerController engineer;
        Vector3 lastPos;
        float stepAccum;
        string repairLoopId;
        string panicLoopId;

        public void Bind(EngineerController controller)
        {
            engineer = controller;
            lastPos = transform.position;
            repairLoopId = "eng_repair_" + GetEntityId();
            panicLoopId = "eng_panic_" + GetEntityId();
            if (engineer.Fsm != null) engineer.Fsm.StateChanged += OnStateChanged;
        }

        void OnStateChanged(EngineerStateId id, string label)
        {
            if (id == EngineerStateId.Stunned)
                AudioManager.PlayAt("stun_zap", transform.position, SfxBus.Engineer, 0.6f);
        }

        void Update()
        {
            if (engineer == null || engineer.Fsm == null) return;
            var id = engineer.Fsm.CurrentId;

            // Footsteps by traveled distance, slightly faster/louder in panic/retreat.
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            float dist = (pos - lastPos).magnitude;
            lastPos = pos;
            bool hurried = id == EngineerStateId.Panic || id == EngineerStateId.RetreatToExit;
            if (dt > 0f && dist / dt > 0.15f && !engineer.IsEscaped)
            {
                stepAccum += dist;
                float stride = hurried ? 0.5f : 0.64f;
                if (stepAccum >= stride)
                {
                    stepAccum = 0f;
                    AudioManager.PlayAt("footstep", pos, SfxBus.Engineer,
                        hurried ? 0.5f : 0.35f, 0.9f + Random.value * 0.2f);
                }
            }
            else stepAccum = 0f;

            // State loops (LoopOn retargets instead of stacking, so per-frame is safe).
            if (id == EngineerStateId.RepairObjective)
                AudioManager.LoopOn(repairLoopId, "engineer_repair", SfxBus.Engineer, 0.4f, 0.3f);
            else
                AudioManager.LoopOff(repairLoopId, 0.25f);

            if (id == EngineerStateId.Panic)
                AudioManager.LoopOn(panicLoopId, "engineer_panic", SfxBus.Engineer, 0.4f, 0.5f);
            else
                AudioManager.LoopOff(panicLoopId, 0.6f);
        }

        /// <summary>Fresh hazard contact feedback (frost crackle, wet slip, steam sting).</summary>
        public static void PlayHazardContact(HazardKind kind, Vector2 pos)
        {
            switch (kind)
            {
                case HazardKind.Cold:
                    AudioManager.PlayAt("ice_crackle", pos, SfxBus.Engineer, 0.5f); break;
                case HazardKind.Slippery:
                    AudioManager.PlayAt("slip_wet", pos, SfxBus.Engineer, 0.5f); break;
                case HazardKind.Steam:
                    AudioManager.PlayAt("steam_burst", pos, SfxBus.Engineer, 0.45f, 1.15f); break;
                default:
                    AudioManager.PlayAt("spark_crackle", pos, SfxBus.Engineer, 0.4f); break;
            }
        }

        void OnDestroy()
        {
            AudioManager.LoopOff(repairLoopId, 0.2f);
            AudioManager.LoopOff(panicLoopId, 0.2f);
            if (engineer != null && engineer.Fsm != null)
                engineer.Fsm.StateChanged -= OnStateChanged;
        }
    }
}
