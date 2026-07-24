using UnityEngine;
using LastShift.Data;
using LastShift.Hazards;

namespace LastShift.Audio
{
    /// <summary>
    /// Sound layer for one hazard zone: steam hiss / cold fan loops while the zone is
    /// active (per-zone loop ids, so they can never stack) and short one-shots for
    /// water spray / spark bursts. Attached automatically by HazardZone.Setup.
    /// </summary>
    public class HazardAudio : MonoBehaviour
    {
        HazardZone zone;
        string loopId;

        public void Bind(HazardZone hazardZone)
        {
            zone = hazardZone;
            loopId = "hazard_" + GetEntityId();
            zone.ActiveChanged += OnActiveChanged;
        }

        void OnActiveChanged(bool active)
        {
            if (zone == null) return;
            Vector2 pos = zone.Area.center;
            switch (zone.kind)
            {
                case HazardKind.Steam:
                    if (active)
                    {
                        AudioManager.PlayAt("valve_click", pos, SfxBus.Hazards, 0.6f);
                        AudioManager.LoopOn(loopId, "steam_loop", SfxBus.Hazards, 0.5f, 0.5f);
                    }
                    else AudioManager.LoopOff(loopId, 0.9f);
                    break;

                case HazardKind.Cold:
                    if (active)
                    {
                        AudioManager.PlayAt("steam_burst", pos, SfxBus.Hazards, 0.35f, 0.6f); // low air blast
                        AudioManager.LoopOn(loopId, "cold_fan_loop", SfxBus.Hazards, 0.55f, 0.8f);
                    }
                    else AudioManager.LoopOff(loopId, 1.1f);
                    break;

                case HazardKind.Slippery:
                    if (active) AudioManager.PlayAt("spray_splash", pos, SfxBus.Hazards, 0.6f);
                    break;

                default: // Danger and any future kinds: short electric crackle, no loop
                    if (active)
                        AudioManager.PlayLimited("hazard_spark_" + loopId, 1.5f,
                            "spark_crackle", SfxBus.Hazards, 0.5f);
                    break;
            }
        }

        void OnDisable() => AudioManager.LoopOff(loopId, 0.2f);
        void OnDestroy() => AudioManager.LoopOff(loopId, 0.2f);
    }
}
