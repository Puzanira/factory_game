using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastShift.Audio
{
    /// <summary>
    /// Procedural DSP synthesis for every sound in the game: cold industrial hums,
    /// relay clicks, servo motors, steam hisses, CRT terminal beeps. Single source of
    /// truth for audio content — the editor AudioProjectBuilder exports these to WAV
    /// files under Assets/Audio/Generated/, and the runtime AudioManager falls back to
    /// synthesizing the same data in memory if a generated clip is missing.
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 44100;

        enum Wave { Sine, Square, Saw }

        // ================= public API =================

        public static IEnumerable<string> Names => Generators.Keys;

        public static bool Has(string name) => Generators.ContainsKey(name);

        /// <summary>Renders mono samples for a known sound; null when unknown or on error.</summary>
        public static float[] Render(string name)
        {
            if (!Generators.TryGetValue(name, out var gen)) return null;
            try { return gen(); }
            catch (Exception e)
            {
                Debug.LogWarning("[LastShift.Audio] SfxSynth failed for '" + name + "': " + e.Message);
                return null;
            }
        }

        /// <summary>Creates a playable AudioClip in memory (runtime fallback path).</summary>
        public static AudioClip CreateClip(string name)
        {
            float[] data = Render(name);
            if (data == null || data.Length == 0) return null;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ================= DSP helpers =================

        static float[] New(float seconds) => new float[Mathf.Max(8, (int)(seconds * Rate))];

        static Func<float, float> E(float k) => t => Mathf.Exp(-t * k);

        /// <summary>Additive tone segment; env receives normalized 0..1 segment time.</summary>
        static void Tone(float[] b, float start, float dur, float f0, float f1, float amp,
            Func<float, float> env = null, Wave wave = Wave.Sine, float vibHz = 0f, float vibDepth = 0f)
        {
            int i0 = (int)(start * Rate);
            int n = (int)(dur * Rate);
            double ph = 0.0;
            for (int i = 0; i < n && i0 + i < b.Length; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                float f = Mathf.Lerp(f0, f1, t);
                if (vibHz > 0f) f += Mathf.Sin(2f * Mathf.PI * vibHz * i / Rate) * vibDepth;
                ph += 2.0 * Math.PI * f / Rate;
                float s;
                switch (wave)
                {
                    case Wave.Square:
                        float sq = (float)Math.Sin(ph);
                        s = Mathf.Sign(sq) * 0.7f + sq * 0.3f;
                        break;
                    case Wave.Saw:
                        s = 2f * (float)(ph / (2.0 * Math.PI) - Math.Floor(ph / (2.0 * Math.PI))) - 1f;
                        break;
                    default:
                        s = (float)Math.Sin(ph);
                        break;
                }
                float g = env != null ? env(t) : 1f;
                b[i0 + i] += s * amp * g;
            }
        }

        /// <summary>Filtered noise segment (one-pole LP then HP); env normalized 0..1.</summary>
        static void Noise(float[] b, float start, float dur, float amp, float lpHz, float hpHz,
            Func<float, float> env = null, int seed = 12345)
        {
            int i0 = (int)(start * Rate);
            int n = (int)(dur * Rate);
            var rnd = new System.Random(seed);
            float lpA = Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(lpHz, 10f, 20000f) / Rate);
            float hpA = Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(hpHz, 1f, 20000f) / Rate);
            float lp = 0f, hpIn = 0f, hpOut = 0f;
            for (int i = 0; i < n && i0 + i < b.Length; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                float x = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (x - lp) * (1f - lpA);
                hpOut = hpA * (hpOut + lp - hpIn);
                hpIn = lp;
                float g = env != null ? env(t) : 1f;
                b[i0 + i] += hpOut * amp * g * 3.5f; // filter loss compensation
            }
        }

        /// <summary>Sparse random micro-impulses (crackle, sparks, ice, ratchet ticks).</summary>
        static void Clicks(float[] b, float start, float dur, int count, float amp,
            float lpHz, float hpHz, int seed)
        {
            var rnd = new System.Random(seed);
            for (int k = 0; k < count; k++)
            {
                float at = start + (float)rnd.NextDouble() * dur;
                float d = 0.004f + (float)rnd.NextDouble() * 0.014f;
                float a = amp * (0.4f + 0.6f * (float)rnd.NextDouble());
                Noise(b, at, d, a, lpHz, hpHz, E(22f), seed + 17 * k + 1);
            }
        }

        /// <summary>Normalizes to target peak with a hard safety clamp.</summary>
        static float[] Peak(float[] b, float target)
        {
            float m = 1e-5f;
            for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
            float s = target / m;
            for (int i = 0; i < b.Length; i++) b[i] = Mathf.Clamp(b[i] * s, -1f, 1f);
            return b;
        }

        /// <summary>
        /// Seamless-loop fold: the source is rendered fadeSec longer than the loop;
        /// the extra tail is crossfaded into the head so end→start is continuous.
        /// </summary>
        static float[] Fold(float[] b, float fadeSec)
        {
            int n = (int)(fadeSec * Rate);
            int len = b.Length - n;
            if (len <= 0) return b;
            var outp = new float[len];
            Array.Copy(b, outp, len);
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                outp[i] = b[i] * k + b[len + i] * (1f - k);
            }
            return outp;
        }

        // ================= generators =================

        static readonly Dictionary<string, Func<float[]>> Generators = new Dictionary<string, Func<float[]>>
        {
            // --- UI / terminal ---
            { "terminal_move", TerminalMove },
            { "terminal_confirm", TerminalConfirm },
            { "terminal_denied", TerminalDenied },
            { "terminal_cooldown", TerminalCooldown },
            { "terminal_ready", TerminalReady },
            { "ui_pause_move", UiPauseMove },
            { "ui_pause_confirm", UiPauseConfirm },
            { "intro_page", IntroPage },

            // --- shared mechanical ---
            { "relay_click", RelayClick },
            { "valve_click", ValveClick },

            // --- machines ---
            { "door_open", DoorOpen },
            { "door_close", DoorClose },
            { "door_warn", DoorWarn },
            { "conveyor_loop", ConveyorLoop },
            { "conveyor_reverse", ConveyorReverse },
            { "arm_windup", ArmWindup },
            { "arm_strike", ArmStrike },
            { "press_slam", PressSlam },
            { "arm_hit", ArmHit },
            { "servo_soft", ServoSoft },
            { "forklift_start", ForkliftStart },
            { "forklift_loop", ForkliftLoop },
            { "forklift_beep", ForkliftBeep },
            { "forklift_bump", ForkliftBump },
            { "forklift_brake", ForkliftBrake },
            { "drone_loop", DroneLoop },
            { "drone_scan", DroneScan },
            { "drone_alert", DroneAlert },
            { "alarm_loop", AlarmLoop },
            { "scanner_loop", ScannerLoop },

            // --- escalation / states ---
            { "escalation_start", EscalationStart },
            { "escalation_loop", EscalationLoop },
            { "tension_loop", TensionLoop },

            // --- hazards ---
            { "steam_loop", SteamLoop },
            { "steam_burst", SteamBurst },
            { "cold_fan_loop", ColdFanLoop },
            { "spray_splash", SpraySplash },
            { "slip_wet", SlipWet },
            { "ice_crackle", IceCrackle },
            { "spark_crackle", SparkCrackle },

            // --- engineer ---
            { "footstep", Footstep },
            { "engineer_repair", EngineerRepair },
            { "engineer_panic", EngineerPanic },
            { "stun_zap", StunZap },
            { "repair_done", RepairDone },
            { "blocked_thud", BlockedThud },

            // --- ambience / music ---
            { "ambient_l1_loop", AmbientL1 },
            { "ambient_l2_loop", AmbientL2 },
            { "ambient_l3_loop", AmbientL3 },
            { "music_drone_loop", MusicDrone },
            { "intro_drone_loop", IntroDrone },

            // --- endings ---
            { "factory_victory", FactoryVictory },
            { "room_won", RoomWon },
            { "factory_defeat", FactoryDefeat },
        };

        // ---------- UI ----------

        static float[] TerminalMove()
        {
            var b = New(0.05f);
            Tone(b, 0f, 0.05f, 1550f, 1400f, 0.5f, E(8f));
            Noise(b, 0f, 0.02f, 0.15f, 6000f, 1200f, E(20f), 11);
            return Peak(b, 0.4f);
        }

        static float[] TerminalConfirm()
        {
            var b = New(0.16f);
            Noise(b, 0f, 0.012f, 0.5f, 8000f, 900f, E(30f), 21);
            Tone(b, 0.01f, 0.06f, 880f, 880f, 0.4f, E(5f));
            Tone(b, 0.07f, 0.08f, 1174f, 1174f, 0.35f, E(5f));
            return Peak(b, 0.5f);
        }

        static float[] TerminalDenied()
        {
            var b = New(0.16f);
            Tone(b, 0f, 0.16f, 115f, 95f, 0.5f, E(2.5f), Wave.Square);
            return Peak(b, 0.28f);
        }

        static float[] TerminalCooldown()
        {
            var b = New(0.06f);
            Tone(b, 0f, 0.06f, 620f, 300f, 0.4f, E(7f));
            return Peak(b, 0.3f);
        }

        static float[] TerminalReady()
        {
            var b = New(0.15f);
            Tone(b, 0f, 0.06f, 660f, 660f, 0.3f, E(4f));
            Tone(b, 0.07f, 0.07f, 990f, 990f, 0.3f, E(4f));
            return Peak(b, 0.35f);
        }

        static float[] UiPauseMove()
        {
            var b = New(0.045f);
            Tone(b, 0f, 0.045f, 1150f, 1080f, 0.5f, E(9f));
            return Peak(b, 0.3f);
        }

        static float[] UiPauseConfirm()
        {
            var b = New(0.1f);
            Noise(b, 0f, 0.01f, 0.4f, 7000f, 800f, E(28f), 31);
            Tone(b, 0.01f, 0.08f, 760f, 760f, 0.4f, E(6f));
            return Peak(b, 0.4f);
        }

        static float[] IntroPage()
        {
            var b = New(0.11f);
            Noise(b, 0f, 0.008f, 0.25f, 6000f, 1000f, E(30f), 41);
            Tone(b, 0.005f, 0.1f, 520f, 660f, 0.35f, E(4f));
            return Peak(b, 0.35f);
        }

        // ---------- shared mechanical ----------

        static float[] RelayClick()
        {
            var b = New(0.08f);
            Noise(b, 0f, 0.01f, 0.8f, 9000f, 800f, E(28f), 51);
            Noise(b, 0.03f, 0.015f, 0.4f, 9000f, 800f, E(30f), 52);
            Tone(b, 0f, 0.03f, 210f, 140f, 0.25f, E(10f));
            return Peak(b, 0.5f);
        }

        static float[] ValveClick()
        {
            var b = New(0.12f);
            Noise(b, 0f, 0.015f, 0.6f, 6000f, 2500f, E(25f), 61);
            Tone(b, 0f, 0.05f, 2900f, 2900f, 0.2f, E(18f));
            Tone(b, 0f, 0.06f, 120f, 80f, 0.3f, E(10f));
            return Peak(b, 0.45f);
        }

        // ---------- machines ----------

        static float[] DoorOpen()
        {
            var b = New(0.65f);
            Tone(b, 0f, 0.6f, 170f, 95f, 0.35f, E(1.2f), Wave.Sine, 22f, 6f);
            Noise(b, 0f, 0.6f, 0.3f, 1200f, 200f, t => Mathf.Min(t * 8f, 1f) * Mathf.Exp(-t * 2.2f), 71);
            Noise(b, 0.56f, 0.05f, 0.3f, 5000f, 500f, E(20f), 72);
            return Peak(b, 0.5f);
        }

        static float[] DoorClose()
        {
            var b = New(0.8f);
            Tone(b, 0f, 0.55f, 95f, 160f, 0.35f, null, Wave.Sine, 22f, 6f);
            Noise(b, 0f, 0.55f, 0.28f, 1100f, 200f, t => Mathf.Min(t * 8f, 1f), 81);
            Tone(b, 0.55f, 0.2f, 55f, 38f, 0.9f, E(6f));
            Noise(b, 0.55f, 0.06f, 0.5f, 3000f, 100f, E(15f), 82);
            Tone(b, 0.55f, 0.15f, 320f, 315f, 0.15f, E(8f));
            return Peak(b, 0.72f);
        }

        static float[] DoorWarn()
        {
            var b = New(0.3f);
            Tone(b, 0f, 0.09f, 950f, 950f, 0.4f, E(3f));
            Tone(b, 0.15f, 0.09f, 950f, 950f, 0.4f, E(3f));
            return Peak(b, 0.4f);
        }

        static float[] ConveyorLoop()
        {
            const float len = 2.5f, fade = 0.2f;
            var b = New(len + fade);
            Func<float, float> flutter = t => 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 8f * t * (len + fade));
            Tone(b, 0f, len + fade, 84f, 84f, 0.3f, flutter);
            Tone(b, 0f, len + fade, 168f, 168f, 0.18f, flutter);
            Tone(b, 0f, len + fade, 252f, 252f, 0.07f);
            Noise(b, 0f, len + fade, 0.22f, 900f, 60f, flutter, 91);
            return Peak(Fold(b, fade), 0.5f);
        }

        static float[] ConveyorReverse()
        {
            var b = New(0.55f);
            Noise(b, 0f, 0.012f, 0.6f, 8000f, 800f, E(28f), 101);
            Tone(b, 0.05f, 0.45f, 60f, 115f, 0.4f, t => Mathf.Min(t * 6f, 1f) * Mathf.Exp(-t * 1.5f));
            Noise(b, 0.05f, 0.45f, 0.25f, 800f, 100f, t => Mathf.Min(t * 5f, 1f) * Mathf.Exp(-t * 2f), 102);
            return Peak(b, 0.5f);
        }

        static float[] ArmWindup()
        {
            var b = New(0.95f);
            Tone(b, 0f, 0.95f, 130f, 430f, 0.28f, null, Wave.Saw);
            Tone(b, 0f, 0.95f, 130f, 430f, 0.22f);
            Noise(b, 0f, 0.95f, 0.3f, 2500f, 300f, t => t * 0.9f, 111);
            return Peak(b, 0.45f);
        }

        static float[] ArmStrike()
        {
            var b = New(0.3f);
            Noise(b, 0f, 0.05f, 0.8f, 7000f, 500f, E(18f), 121);
            Tone(b, 0f, 0.25f, 620f, 615f, 0.5f, E(9f));
            Tone(b, 0f, 0.25f, 932f, 925f, 0.35f, E(11f));
            Tone(b, 0f, 0.22f, 1377f, 1370f, 0.25f, E(14f));
            Tone(b, 0f, 0.12f, 90f, 55f, 0.5f, E(8f));
            return Peak(b, 0.7f);
        }

        static float[] PressSlam()
        {
            var b = New(0.5f);
            Tone(b, 0f, 0.25f, 52f, 30f, 1f, E(5f));
            Noise(b, 0f, 0.08f, 0.7f, 4000f, 80f, E(12f), 131);
            Tone(b, 0f, 0.4f, 240f, 238f, 0.3f, E(6f));
            Tone(b, 0f, 0.4f, 377f, 374f, 0.25f, E(7f));
            Noise(b, 0.35f, 0.08f, 0.3f, 6000f, 900f, E(20f), 132);
            return Peak(b, 0.82f);
        }

        static float[] ArmHit()
        {
            var b = New(0.35f);
            Noise(b, 0f, 0.04f, 0.7f, 7000f, 500f, E(18f), 141);
            Tone(b, 0f, 0.2f, 620f, 610f, 0.45f, E(9f));
            Tone(b, 0f, 0.2f, 70f, 45f, 0.3f, E(6f), Wave.Square);
            Clicks(b, 0.02f, 0.25f, 12, 0.4f, 9000f, 1500f, 142);
            return Peak(b, 0.65f);
        }

        static float[] ServoSoft()
        {
            var b = New(0.12f);
            Tone(b, 0f, 0.12f, 150f, 190f, 0.25f, E(2f), Wave.Sine, 30f, 8f);
            Noise(b, 0f, 0.12f, 0.2f, 900f, 150f, E(3f), 151);
            return Peak(b, 0.25f);
        }

        static float[] ForkliftStart()
        {
            var b = New(0.5f);
            Tone(b, 0f, 0.5f, 65f, 135f, 0.4f, t => Mathf.Min(t * 6f, 1f));
            Noise(b, 0f, 0.5f, 0.3f, 700f, 80f, t => Mathf.Min(t * 4f, 1f), 161);
            return Peak(b, 0.5f);
        }

        static float[] ForkliftLoop()
        {
            const float len = 2f, fade = 0.2f;
            var b = New(len + fade);
            Func<float, float> am = t => 0.88f + 0.12f * Mathf.Sin(2f * Mathf.PI * 6f * t * (len + fade));
            Tone(b, 0f, len + fade, 92f, 92f, 0.3f, am);
            Tone(b, 0f, len + fade, 184f, 184f, 0.15f, am);
            Tone(b, 0f, len + fade, 368f, 368f, 0.06f);
            Tone(b, 0f, len + fade, 740f, 740f, 0.03f);
            Noise(b, 0f, len + fade, 0.18f, 1000f, 90f, am, 171);
            return Peak(Fold(b, fade), 0.45f);
        }

        static float[] ForkliftBeep()
        {
            var b = New(0.22f);
            Tone(b, 0f, 0.18f, 1000f, 1000f, 0.5f,
                t => Mathf.Min(t * 15f, 1f) * Mathf.Min((1f - t) * 8f, 1f));
            return Peak(b, 0.5f);
        }

        static float[] ForkliftBump()
        {
            var b = New(0.25f);
            Tone(b, 0f, 0.2f, 70f, 40f, 0.8f, E(7f));
            Noise(b, 0f, 0.12f, 0.4f, 500f, 60f, E(10f), 181);
            return Peak(b, 0.6f);
        }

        static float[] ForkliftBrake()
        {
            var b = New(0.4f);
            Noise(b, 0f, 0.35f, 0.5f, 5000f, 900f, E(4f), 191);
            Noise(b, 0.02f, 0.012f, 0.4f, 8000f, 800f, E(28f), 192);
            return Peak(b, 0.48f);
        }

        static float[] DroneLoop()
        {
            const float len = 1.8f, fade = 0.15f;
            var b = New(len + fade);
            Tone(b, 0f, len + fade, 220f, 220f, 0.22f);
            Tone(b, 0f, len + fade, 227f, 227f, 0.18f);
            Tone(b, 0f, len + fade, 440f, 440f, 0.08f);
            Tone(b, 0f, len + fade, 660f, 660f, 0.04f);
            Noise(b, 0f, len + fade, 0.06f, 3000f, 800f, null, 201);
            return Peak(Fold(b, fade), 0.4f);
        }

        static float[] DroneScan()
        {
            var b = New(0.35f);
            Tone(b, 0f, 0.07f, 1250f, 1250f, 0.4f, E(6f));
            Tone(b, 0.15f, 0.07f, 1250f, 1250f, 0.25f, E(6f));
            return Peak(b, 0.4f);
        }

        static float[] DroneAlert()
        {
            var b = New(0.25f);
            Tone(b, 0f, 0.1f, 1318f, 1318f, 0.4f, E(4f));
            Tone(b, 0.12f, 0.12f, 988f, 988f, 0.4f, E(4f));
            return Peak(b, 0.45f);
        }

        static float[] AlarmLoop()
        {
            const float len = 2f, fade = 0.1f;
            var b = New(len + fade);
            Func<float, float> swell = t => Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.6f);
            Tone(b, 0f, 1f, 640f, 640f, 0.4f, swell);
            Tone(b, 1f, 1f + fade, 810f, 810f, 0.4f, swell);
            Tone(b, 0f, len + fade, 110f, 110f, 0.1f);
            Noise(b, 0f, len + fade, 0.05f, 2000f, 300f, null, 211);
            return Peak(Fold(b, fade), 0.5f);
        }

        static float[] ScannerLoop()
        {
            const float len = 1.6f, fade = 0.1f;
            var b = New(len + fade);
            for (int k = 0; k < 4; k++)
                Tone(b, k * 0.4f, 0.06f, 1060f, 1060f, 0.35f, E(5f));
            Tone(b, 0f, len + fade, 220f, 220f, 0.12f);
            return Peak(Fold(b, fade), 0.4f);
        }

        // ---------- escalation / states ----------

        static float[] EscalationStart()
        {
            var b = New(1.3f);
            Tone(b, 0f, 0.9f, 220f, 70f, 0.45f, E(1.2f), Wave.Saw);
            Tone(b, 0f, 0.3f, 60f, 35f, 0.8f, E(5f));
            Tone(b, 0.6f, 0.15f, 880f, 880f, 0.3f, E(3f));
            Noise(b, 0f, 0.5f, 0.25f, 2000f, 200f, E(3f), 221);
            return Peak(b, 0.75f);
        }

        static float[] EscalationLoop()
        {
            const float len = 3f, fade = 0.2f;
            var b = New(len + fade);
            Func<float, float> throb = t => 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 1.6f * t * (len + fade));
            Tone(b, 0f, len + fade, 58f, 58f, 0.4f, throb);
            Tone(b, 0f, len + fade, 660f, 660f, 0.06f);
            Tone(b, 0f, len + fade, 663f, 663f, 0.06f);
            for (int k = 0; k < 4; k++)
                Tone(b, k * 0.75f, 0.04f, 1245f, 1245f, 0.08f, E(8f));
            Noise(b, 0f, len + fade, 0.15f, 600f, 60f, throb, 231);
            return Peak(Fold(b, fade), 0.5f);
        }

        static float[] TensionLoop()
        {
            const float len = 6f, fade = 0.4f;
            var b = New(len + fade);
            Tone(b, 0f, len + fade, 660f, 660f, 0.06f);
            Tone(b, 0f, len + fade, 664f, 664f, 0.06f);
            Tone(b, 0f, len + fade, 1320f, 1320f, 0.02f);
            Func<float, float> pulse = t => 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 1.2f * t * (len + fade));
            Tone(b, 0f, len + fade, 55f, 55f, 0.15f, pulse);
            return Peak(Fold(b, fade), 0.3f);
        }

        // ---------- hazards ----------

        static float[] SteamLoop()
        {
            const float len = 3f, fade = 0.3f;
            var b = New(len + fade);
            Func<float, float> wobble = t => 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t * (len + fade));
            Noise(b, 0f, len + fade, 0.5f, 4500f, 700f, wobble, 241);
            Noise(b, 0f, len + fade, 0.2f, 1500f, 200f, null, 242);
            return Peak(Fold(b, fade), 0.4f);
        }

        static float[] SteamBurst()
        {
            var b = New(0.45f);
            Noise(b, 0f, 0.45f, 0.6f, 5000f, 600f, t => Mathf.Min(t * 12f, 1f) * Mathf.Exp(-t * 4f), 251);
            return Peak(b, 0.5f);
        }

        static float[] ColdFanLoop()
        {
            const float len = 4f, fade = 0.35f;
            var b = New(len + fade);
            Func<float, float> am = t => 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t * (len + fade));
            Tone(b, 0f, len + fade, 46f, 46f, 0.3f, am);
            Tone(b, 0f, len + fade, 92f, 92f, 0.2f, am);
            Noise(b, 0f, len + fade, 0.28f, 500f, 40f, am, 261);
            Tone(b, 0f, len + fade, 1380f, 1380f, 0.02f);
            return Peak(Fold(b, fade), 0.45f);
        }

        static float[] SpraySplash()
        {
            var b = New(0.35f);
            Noise(b, 0f, 0.35f, 0.6f, 3500f, 300f, E(4f), 271);
            Clicks(b, 0.02f, 0.28f, 8, 0.2f, 2000f, 600f, 272);
            return Peak(b, 0.5f);
        }

        static float[] SlipWet()
        {
            var b = New(0.3f);
            Noise(b, 0f, 0.3f, 0.4f, 2000f, 400f, E(5f), 281);
            Tone(b, 0f, 0.2f, 300f, 140f, 0.2f, E(6f));
            return Peak(b, 0.4f);
        }

        static float[] IceCrackle()
        {
            var b = New(0.5f);
            Clicks(b, 0f, 0.45f, 10, 0.5f, 12000f, 2000f, 291);
            Noise(b, 0f, 0.5f, 0.08f, 8000f, 3000f, E(3f), 292);
            return Peak(b, 0.45f);
        }

        static float[] SparkCrackle()
        {
            var b = New(0.35f);
            Clicks(b, 0f, 0.3f, 14, 0.6f, 12000f, 1200f, 301);
            Tone(b, 0f, 0.2f, 100f, 100f, 0.15f, E(4f), Wave.Square);
            return Peak(b, 0.5f);
        }

        // ---------- engineer ----------

        static float[] Footstep()
        {
            var b = New(0.12f);
            Noise(b, 0f, 0.08f, 0.6f, 350f, 40f, E(10f), 311);
            Tone(b, 0f, 0.07f, 62f, 44f, 0.4f, E(9f));
            return Peak(b, 0.4f);
        }

        static float[] EngineerRepair()
        {
            const float len = 1.4f, fade = 0.05f;
            var b = New(len + fade);
            float[] ticksAt = { 0.0f, 0.18f, 0.5f, 0.62f, 0.95f, 1.1f };
            for (int k = 0; k < ticksAt.Length; k++)
            {
                Noise(b, ticksAt[k], 0.012f, 0.4f, 9000f, 1500f, E(22f), 321 + k);
                Tone(b, ticksAt[k], 0.04f, 2400f, 2380f, 0.1f, E(20f));
            }
            Tone(b, 0f, len + fade, 140f, 140f, 0.05f);
            return Peak(Fold(b, fade), 0.4f);
        }

        static float[] EngineerPanic()
        {
            const float len = 2.2f, fade = 0.2f;
            var b = New(len + fade);
            Func<float, float> breaths = t =>
                Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t * 3f)), 1.5f) * 0.5f;
            Noise(b, 0f, len + fade, 0.4f, 900f, 250f, breaths, 331);
            return Peak(Fold(b, fade), 0.3f);
        }

        static float[] StunZap()
        {
            var b = New(0.3f);
            Tone(b, 0f, 0.25f, 55f, 38f, 0.35f, E(4f), Wave.Square);
            Clicks(b, 0f, 0.22f, 10, 0.5f, 12000f, 1800f, 341);
            Noise(b, 0f, 0.15f, 0.2f, 9000f, 2500f, E(8f), 342);
            return Peak(b, 0.55f);
        }

        static float[] RepairDone()
        {
            var b = New(0.5f);
            Tone(b, 0f, 0.18f, 523f, 523f, 0.35f, E(3f));
            Tone(b, 0.2f, 0.25f, 392f, 392f, 0.35f, E(3f));
            return Peak(b, 0.45f);
        }

        static float[] BlockedThud()
        {
            var b = New(0.2f);
            Tone(b, 0f, 0.15f, 90f, 55f, 0.5f, E(8f));
            Noise(b, 0f, 0.08f, 0.2f, 800f, 80f, E(12f), 351);
            return Peak(b, 0.4f);
        }

        // ---------- ambience / music ----------

        static float[] AmbientL1()
        {
            const float len = 9f, fade = 0.5f;
            var b = New(len + fade);
            Func<float, float> pump = t => 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.65f * t * (len + fade));
            Tone(b, 0f, len + fade, 55f, 55f, 0.28f, pump);
            Tone(b, 0f, len + fade, 110f, 110f, 0.16f);
            Tone(b, 0f, len + fade, 100f, 100f, 0.05f);
            Tone(b, 0f, len + fade, 200f, 200f, 0.03f);
            Noise(b, 0f, len + fade, 0.22f, 350f, 40f, pump, 401);
            Noise(b, 0f, len + fade, 0.06f, 2000f, 400f, null, 402);
            return Peak(Fold(b, fade), 0.5f);
        }

        static float[] AmbientL2()
        {
            const float len = 8f, fade = 0.5f;
            var b = New(len + fade);
            Func<float, float> pump = t => 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.9f * t * (len + fade));
            Tone(b, 0f, len + fade, 65f, 65f, 0.26f, pump);
            Tone(b, 0f, len + fade, 130f, 130f, 0.15f);
            Tone(b, 0f, len + fade, 165f, 165f, 0.04f);
            Noise(b, 0f, len + fade, 0.2f, 500f, 50f, pump, 411);
            Noise(b, 0f, len + fade, 0.1f, 2600f, 500f, null, 412);
            // Rhythmic packaging-roller knocks.
            for (float at = 0.1f; at < len; at += 0.42f)
                Tone(b, at, 0.03f, 190f, 185f, 0.12f, E(10f));
            return Peak(Fold(b, fade), 0.5f);
        }

        static float[] AmbientL3()
        {
            const float len = 10f, fade = 0.6f;
            var b = New(len + fade);
            Func<float, float> am = t => 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.4f * t * (len + fade));
            Tone(b, 0f, len + fade, 40f, 40f, 0.32f, am);
            Tone(b, 0f, len + fade, 80f, 80f, 0.18f, am);
            Noise(b, 0f, len + fade, 0.2f, 300f, 30f, am, 421);
            Noise(b, 0f, len + fade, 0.05f, 6000f, 1500f, null, 422);
            return Peak(Fold(b, fade), 0.5f);
        }

        static float[] MusicDrone()
        {
            const float len = 12f, fade = 0.8f;
            var b = New(len + fade);
            Func<float, float> slow = t => 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t);
            Tone(b, 0f, len + fade, 55f, 55f, 0.2f, slow);
            Tone(b, 0f, len + fade, 82.4f, 82.4f, 0.12f, slow);
            Tone(b, 0f, len + fade, 110f, 110f, 0.08f);
            Tone(b, 0f, len + fade, 164.8f, 164.8f, 0.05f);
            return Peak(Fold(b, fade), 0.35f);
        }

        static float[] IntroDrone()
        {
            const float len = 10f, fade = 0.6f;
            var b = New(len + fade);
            Func<float, float> slow = t => 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.7f * t);
            Tone(b, 0f, len + fade, 60f, 60f, 0.25f, slow);
            Tone(b, 0f, len + fade, 120f, 120f, 0.12f);
            Tone(b, 0f, len + fade, 240f, 240f, 0.05f);
            Tone(b, 0f, len + fade, 3520f, 3520f, 0.012f); // faint CRT whine
            Noise(b, 0f, len + fade, 0.15f, 250f, 30f, slow, 431);
            return Peak(Fold(b, fade), 0.4f);
        }

        // ---------- endings ----------

        static float[] FactoryVictory()
        {
            var b = New(4f);
            Tone(b, 0f, 4f, 55f, 55f, 0.2f, t => Mathf.Min(t * 5f, 1f) * Mathf.Min((1f - t) * 3f, 1f));
            Tone(b, 0.2f, 0.6f, 880f, 880f, 0.3f, E(2.5f));
            Tone(b, 1.1f, 0.6f, 659.3f, 659.3f, 0.3f, E(2.5f));
            Tone(b, 2.0f, 0.9f, 440f, 440f, 0.3f, E(2f));
            Tone(b, 2.0f, 0.9f, 587.3f, 587.3f, 0.12f, E(2f)); // hollow fourth, not a happy triad
            Noise(b, 3.1f, 0.012f, 0.4f, 8000f, 900f, E(28f), 441);
            return Peak(b, 0.55f);
        }

        static float[] RoomWon()
        {
            var b = New(1.8f);
            Tone(b, 0f, 0.45f, 659.3f, 659.3f, 0.3f, E(3f));
            Tone(b, 0.5f, 0.6f, 440f, 440f, 0.3f, E(2.5f));
            Tone(b, 0f, 1.8f, 55f, 55f, 0.15f, t => Mathf.Min(t * 6f, 1f) * (1f - t));
            Noise(b, 1.2f, 0.012f, 0.35f, 8000f, 900f, E(28f), 451);
            return Peak(b, 0.5f);
        }

        static float[] FactoryDefeat()
        {
            var b = New(2.8f);
            Tone(b, 0f, 1.8f, 196f, 49f, 0.35f, t => Mathf.Min(t * 8f, 1f));
            Tone(b, 0f, 2.5f, 55f, 42f, 0.25f, t => 1f - t);
            Noise(b, 0f, 2f, 0.15f, 400f, 40f, E(2f), 461);
            Tone(b, 2.0f, 0.5f, 45f, 30f, 0.5f, E(4f));
            return Peak(b, 0.55f);
        }
    }
}
