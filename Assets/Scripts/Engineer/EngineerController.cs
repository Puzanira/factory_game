using System.Collections.Generic;
using UnityEngine;
using LastShift.Data;
using LastShift.Hazards;
using LastShift.Machines;
using LastShift.Objectives;
using LastShift.Utilities;

namespace LastShift.Engineer
{
    /// <summary>
    /// The stubborn engineer NPC. Owns the FSM, applies hazard/conveyor/machine effects,
    /// and reports meaningful factory interference as Pressure and Resolve loss.
    /// </summary>
    public class EngineerController : MonoBehaviour
    {
        public static EngineerController Instance { get; private set; }

        public EngineerData Data { get; private set; }
        public EngineerStats Stats { get; private set; }
        public EngineerNavigation Nav { get; private set; }
        public EngineerStateMachine Fsm { get; private set; }

        public RepairObjective CurrentObjective { get; private set; }
        public Vector2 ExitPos { get; private set; }
        public float PendingStunDuration { get; private set; }

        public event System.Action Escaped;
        public event System.Action RepairInterrupted;
        /// <summary>A machine stun connected (source = machine display name).</summary>
        public event System.Action<string> StunnedBy;
        /// <summary>The engineer was displaced a meaningful distance by conveyors.</summary>
        public event System.Action ConveyorCarried;
        /// <summary>Fresh contact with an active hazard zone (zone name).</summary>
        public event System.Action<string> HazardContact;

        /// <summary>True while the drone mark is on him (hazards/stuns hit harder).</summary>
        public bool IsMarked { get; private set; }
        /// <summary>Drone mark applied/removed.</summary>
        public event System.Action<bool> MarkedChanged;

        readonly List<SlowEffect> slows = new List<SlowEffect>();
        readonly HashSet<HazardZone> insideZones = new HashSet<HazardZone>();
        readonly List<HazardZone> zoneScratch = new List<HazardZone>();

        float conveyorDisplacement;
        float lastRouteBlockedLossTime = -99f;
        bool initialized;
        SpriteRenderer markRing;

        struct SlowEffect { public float factor; public float until; }

        public Vector2 Pos => transform.position;
        public bool IsEscaped => Fsm != null && Fsm.CurrentId == EngineerStateId.Escape;
        public bool IsRetreating => Fsm != null &&
            (Fsm.CurrentId == EngineerStateId.RetreatToExit || Fsm.CurrentId == EngineerStateId.Escape);

        public float PanicExitThreshold => Data != null ? Data.panicExitThreshold : 30f;
        public float DangerWeight => Data != null ? Data.dangerWeight : 3f;

        public bool WithinRepairRange => CurrentObjective != null &&
            Vector2.Distance(Pos, CurrentObjective.Pos) <= (Data != null ? Data.repairRange : 1.15f);

        public bool LocalDangerHigh
        {
            get
            {
                var grid = PathGrid.Instance;
                float threshold = Data != null ? Data.avoidDangerThreshold : 1.5f;
                return grid != null && grid.DangerAt(Pos) >= threshold;
            }
        }

        public float CurrentSpeed
        {
            get
            {
                float baseSpeed = Data != null ? Data.moveSpeed : 2.6f;
                if (Fsm != null)
                {
                    if (Fsm.CurrentId == EngineerStateId.Panic) baseSpeed = Data != null ? Data.panicMoveSpeed : 3.6f;
                    else if (Fsm.CurrentId == EngineerStateId.RetreatToExit) baseSpeed = Data != null ? Data.retreatMoveSpeed : 3.2f;
                }
                float slow = 1f;
                for (int i = 0; i < slows.Count; i++)
                    if (Time.time < slows[i].until) slow = Mathf.Min(slow, slows[i].factor);
                foreach (var zone in insideZones)
                    if (zone != null && zone.Active) slow = Mathf.Min(slow, zone.SlowFactor);
                return baseSpeed * slow;
            }
        }

        void Awake()
        {
            Instance = this;
            Stats = GetComponent<EngineerStats>();
            if (Stats == null) Stats = gameObject.AddComponent<EngineerStats>();
            Nav = GetComponent<EngineerNavigation>();
            if (Nav == null) Nav = gameObject.AddComponent<EngineerNavigation>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Init(EngineerData data, Vector2 exitPos)
        {
            Data = data;
            ExitPos = exitPos;
            Stats.Init(data);
            Fsm = new EngineerStateMachine(this);
            Nav.PathInvalidated += OnPathInvalidated;
            BuildVisuals();
            var ui = GetComponent<EngineerStateUI>();
            if (ui == null) ui = gameObject.AddComponent<EngineerStateUI>();
            ui.Bind(this);
            var audio = GetComponent<LastShift.Audio.EngineerAudio>();
            if (audio == null) audio = gameObject.AddComponent<LastShift.Audio.EngineerAudio>();
            audio.Bind(this);
            Fsm.Start(EngineerStateId.EnterRoom);
            initialized = true;
        }

        Transform bodyRoot;
        Transform dirIndicator;
        Transform legL, legR;
        Transform torsoT, helmetT;
        SpriteRenderer rimGlow;
        SpriteRenderer repairFlash;
        SpriteRenderer retreatArrow;
        Vector3 lastPos;
        float bobPhase;
        float stateChangedAt;

        const string CharactersLayer = "Characters";
        const string EffectsLayer = "Effects";

        static readonly Color RimDefault = new Color(1f, 0.9f, 0.7f, 0.20f);
        static readonly Color RimPanic = new Color(1f, 0.25f, 0.15f, 0.45f);
        static readonly Color RimStun = new Color(1f, 0.9f, 0.2f, 0.55f);
        static readonly Color RimCold = new Color(0.65f, 0.85f, 1f, 0.45f);
        static readonly Color RimSteam = new Color(1f, 0.95f, 0.85f, 0.4f);

        /// <summary>
        /// Composed top-down human worker: shadow, trousers with alternating legs and
        /// boots, orange work jacket with hi-vis stripe and shoulders, head partially
        /// covered by a pale helmet with a lamp, warm readability rim, direction lamp
        /// cone. Renders on the Characters sorting layer so floor decals, hazard
        /// overlays and fog can never hide him. Presentation only — no gameplay change.
        /// </summary>
        void BuildVisuals()
        {
            Fsm.StateChanged += (_, __) => stateChangedAt = Time.time;

            var shadow = FxFactory.Shadow(transform, new Vector2(0.85f, 0.55f), 0.45f, 0);
            Viz.SetLayer(shadow, CharactersLayer);

            var root = new GameObject("BodyRoot");
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one * 1.12f; // slightly larger, still path-readable
            bodyRoot = root.transform;

            // Warm readability rim (soft, not neon) — keeps him visible in fog/dark.
            rimGlow = FxFactory.Glow(bodyRoot, RimDefault, new Vector2(1.15f, 1.3f), 1);
            Viz.SetLayer(rimGlow, CharactersLayer);

            // Lower body: dark work trousers (two legs) + boots.
            var lL = Viz.Make("LegL", bodyRoot, PlaceholderShape.Square, new Color(0.20f, 0.22f, 0.27f), new Vector2(-0.11f, -0.30f), new Vector2(0.17f, 0.3f), 2);
            var lR = Viz.Make("LegR", bodyRoot, PlaceholderShape.Square, new Color(0.20f, 0.22f, 0.27f), new Vector2(0.11f, -0.30f), new Vector2(0.17f, 0.3f), 2);
            legL = lL.transform;
            legR = lR.transform;
            Viz.Make("BootL", legL, PlaceholderShape.Square, new Color(0.10f, 0.10f, 0.11f), new Vector2(0f, -0.55f), new Vector2(1.05f, 0.32f), 3);
            Viz.Make("BootR", legR, PlaceholderShape.Square, new Color(0.10f, 0.10f, 0.11f), new Vector2(0f, -0.55f), new Vector2(1.05f, 0.32f), 3);
            Viz.Make("Belt", bodyRoot, PlaceholderShape.Square, new Color(0.13f, 0.14f, 0.17f), new Vector2(0f, -0.18f), new Vector2(0.44f, 0.12f), 3);

            // Torso: orange work jacket, clearly larger than the head.
            var torso = Viz.Make("Torso", bodyRoot, PlaceholderShape.Circle, new Color(0.95f, 0.55f, 0.14f), new Vector2(0f, 0f), new Vector2(0.56f, 0.5f), 4);
            torsoT = torso.transform;
            Viz.Make("ShoulderL", bodyRoot, PlaceholderShape.Circle, new Color(0.82f, 0.45f, 0.1f), new Vector2(-0.26f, 0.05f), new Vector2(0.2f, 0.2f), 4);
            Viz.Make("ShoulderR", bodyRoot, PlaceholderShape.Circle, new Color(0.82f, 0.45f, 0.1f), new Vector2(0.26f, 0.05f), new Vector2(0.2f, 0.2f), 4);
            Viz.Make("HiVisA", bodyRoot, PlaceholderShape.Square, new Color(0.97f, 0.92f, 0.55f), new Vector2(0f, -0.05f), new Vector2(0.5f, 0.055f), 5);
            Viz.Make("HiVisB", bodyRoot, PlaceholderShape.Square, new Color(0.97f, 0.92f, 0.55f), new Vector2(0f, 0.07f), new Vector2(0.46f, 0.045f), 5);

            // Head (skin tone) partially covered by a pale industrial helmet with a brim.
            Viz.Make("Head", bodyRoot, PlaceholderShape.Circle, new Color(0.92f, 0.75f, 0.58f), new Vector2(0f, 0.24f), new Vector2(0.24f, 0.24f), 6);
            var helmet = Viz.Make("Helmet", bodyRoot, PlaceholderShape.Circle, new Color(0.96f, 0.92f, 0.78f), new Vector2(0f, 0.28f), new Vector2(0.3f, 0.27f), 7);
            helmetT = helmet.transform;
            Viz.Make("HelmetBrim", helmetT, PlaceholderShape.Square, new Color(0.85f, 0.8f, 0.64f), new Vector2(0f, -0.42f), new Vector2(1.15f, 0.2f), 7);
            Viz.Make("HelmetLamp", helmetT, PlaceholderShape.Circle, new Color(1f, 0.95f, 0.6f), new Vector2(0f, -0.15f), new Vector2(0.3f, 0.3f), 8);

            // Direction: small warm lamp cone that turns toward movement.
            var dir = Viz.Make("DirCone", bodyRoot, PlaceholderShape.Arrow,
                new Color(1f, 0.9f, 0.55f, 0.5f), new Vector2(0.44f, 0f), new Vector2(0.34f, 0.24f), 5);
            dirIndicator = dir.transform;

            // Brief evacuation pointer shown when retreat starts.
            retreatArrow = Viz.Make("RetreatArrow", bodyRoot, PlaceholderShape.Arrow,
                new Color(0.35f, 0.95f, 0.5f, 0.8f), new Vector2(0f, 0.85f), new Vector2(0.4f, 0.28f), 9);
            retreatArrow.gameObject.SetActive(false);

            // Small tool flash used while repairing.
            repairFlash = FxFactory.Glow(bodyRoot, new Color(1f, 0.9f, 0.5f, 0f), new Vector2(0.5f, 0.5f), 9);
            repairFlash.name = "RepairFlash";
            repairFlash.transform.localPosition = new Vector3(0.35f, 0.05f, 0f);

            // Everything above renders on Characters (over floor art, hazards, fog).
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                Viz.SetLayer(sr, CharactersLayer);

            // Helmet work light: keeps him readable in dark rooms and cold fog.
            FxFactory.PointLight(transform, new Vector2(0f, 0.22f), new Color(1f, 0.9f, 0.7f), 1.8f, 0.55f);

            markRing = Viz.Make("MarkRing", transform, PlaceholderShape.Ring, new Color(1f, 0.25f, 0.2f, 0.9f), Vector2.zero, new Vector2(1.1f, 1.1f), 11, unlit: true);
            Viz.SetLayer(markRing, EffectsLayer);
            markRing.gameObject.SetActive(false);
            lastPos = transform.position;
        }

        /// <summary>
        /// Procedural motion + state feedback; pure presentation. Walking alternates
        /// the legs and sways the torso; idle breathes; repairing leans toward the
        /// objective with a tool flash; panic speeds the bob and pulses a red rim;
        /// stun flickers yellow with a shaky posture; cold/steam tint the rim.
        /// </summary>
        void UpdateVisuals(float dt)
        {
            if (bodyRoot == null || dt <= 0f) return;
            Vector3 vel = (transform.position - lastPos) / dt;
            lastPos = transform.position;
            float speed = vel.magnitude;
            var id = Fsm != null ? Fsm.CurrentId : EngineerStateId.EnterRoom;
            bool panicking = id == EngineerStateId.Panic;
            bool stunned = id == EngineerStateId.Stunned;
            bool repairing = id == EngineerStateId.RepairObjective;
            bool retreating = id == EngineerStateId.RetreatToExit || id == EngineerStateId.Escape;

            const float baseScale = 1.12f;
            float lean = 0f;
            Vector3 rootPos = Vector3.zero;

            if (speed > 0.15f)
            {
                // Walking: legs alternate, torso sways, body bobs.
                bobPhase += dt * (6f + speed * 3f) * (panicking ? 1.5f : 1f);
                float swing = Mathf.Sin(bobPhase);
                rootPos.y = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.04f;
                lean = swing * (panicking ? 4f : 2.5f);
                if (legL != null) legL.localPosition = new Vector3(-0.11f, -0.30f + swing * 0.05f, 0f);
                if (legR != null) legR.localPosition = new Vector3(0.11f, -0.30f - swing * 0.05f, 0f);

                if (dirIndicator != null)
                {
                    float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
                    dirIndicator.localRotation = Quaternion.Euler(0f, 0f, ang);
                    dirIndicator.localPosition = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0f) * 0.44f;
                    dirIndicator.gameObject.SetActive(true);
                    var dsr = dirIndicator.GetComponent<SpriteRenderer>();
                    if (dsr != null) dsr.color = new Color(1f, 0.9f, 0.55f, retreating ? 0.85f : 0.5f);
                }
            }
            else
            {
                // Idle: subtle breathing, tiny helmet motion, legs settle.
                bobPhase += dt * 2f;
                float breath = Mathf.Sin(bobPhase) * 0.015f;
                if (torsoT != null) torsoT.localScale = new Vector3(0.56f, 0.5f * (1f + breath), 1f);
                if (helmetT != null) helmetT.localPosition = new Vector3(0f, 0.28f + breath * 0.3f, 0f);
                if (legL != null) legL.localPosition = new Vector3(-0.11f, -0.30f, 0f);
                if (legR != null) legR.localPosition = new Vector3(0.11f, -0.30f, 0f);
                if (dirIndicator != null && (repairing || id == EngineerStateId.AssessSituation))
                    dirIndicator.gameObject.SetActive(false);
            }

            // Repairing: lean toward the objective, tool flash blinks.
            if (repairing && CurrentObjective != null)
            {
                Vector2 to = CurrentObjective.Pos - (Vector2)transform.position;
                lean = to.x >= 0f ? -8f : 8f;
                bobPhase += dt * 10f;
                rootPos.x = Mathf.Sin(bobPhase) * 0.025f;
                if (repairFlash != null)
                {
                    repairFlash.transform.localPosition = new Vector3(Mathf.Sign(to.x) * 0.34f, 0.02f, 0f);
                    float blink = Mathf.PingPong(Time.time * 5f, 1f) > 0.6f ? 0.55f : 0.1f;
                    repairFlash.color = new Color(1f, 0.9f, 0.5f, blink);
                }
            }
            else if (repairFlash != null && repairFlash.color.a > 0.01f)
            {
                repairFlash.color = new Color(1f, 0.9f, 0.5f, 0f);
            }

            // Stunned: shaky posture (electric jitter), handled below with a yellow rim.
            if (stunned)
                rootPos.x += Mathf.Sin(Time.time * 45f) * 0.03f;

            // Slippery footing wobble.
            bool onCold = false, onSteam = false, onSlippery = false;
            foreach (var zone in insideZones)
            {
                if (zone == null || !zone.Active) continue;
                if (zone.kind == HazardKind.Cold) onCold = true;
                else if (zone.kind == HazardKind.Steam) onSteam = true;
                else if (zone.kind == HazardKind.Slippery) onSlippery = true;
            }
            if (onSlippery && speed > 0.15f)
                lean += Mathf.Sin(Time.time * 9f) * 4f;

            bodyRoot.localPosition = rootPos;
            bodyRoot.localRotation = Quaternion.Euler(0f, 0f, lean);
            bodyRoot.localScale = Vector3.one * baseScale;

            // Rim color: readable feedback with strict priority, never a big shape.
            if (rimGlow != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                Color rim = RimDefault;
                if (stunned) rim = Color.Lerp(RimDefault, RimStun, Mathf.PingPong(Time.time * 12f, 1f));
                else if (panicking) rim = Color.Lerp(RimDefault, RimPanic, pulse);
                else if (onCold) rim = RimCold;
                else if (onSteam) rim = RimSteam;
                rimGlow.color = rim;
            }

            // Brief green evacuation pointer when the retreat starts.
            if (retreatArrow != null)
            {
                bool show = retreating && Time.time - stateChangedAt < 3f;
                if (retreatArrow.gameObject.activeSelf != show) retreatArrow.gameObject.SetActive(show);
                if (show)
                {
                    Vector2 to = ExitPos - (Vector2)transform.position;
                    float ang = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                    retreatArrow.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                    retreatArrow.color = new Color(0.35f, 0.95f, 0.5f, 0.4f + 0.4f * Mathf.PingPong(Time.time * 3f, 1f));
                }
            }
        }

        public void SetObjective(RepairObjective objective) => SetObjective(objective, autoReassess: true);

        public void SetObjective(RepairObjective objective, bool autoReassess)
        {
            if (CurrentObjective != null) CurrentObjective.SetIsCurrent(false);
            CurrentObjective = objective;
            if (CurrentObjective != null)
            {
                CurrentObjective.SetIsCurrent(true);
                if (autoReassess && initialized && !IsRetreating &&
                    Fsm.CurrentId != EngineerStateId.Stunned &&
                    Fsm.CurrentId != EngineerStateId.EnterRoom)
                    Fsm.ChangeState(EngineerStateId.AssessSituation);
            }
        }

        /// <summary>
        /// After a stun or on entering panic: if another unfinished repair point
        /// exists, he switches to it (no stubborn walk back into the same trap).
        /// Returns true when the target changed. State transitions stay with the caller.
        /// </summary>
        public bool TrySwitchObjectiveAfterShock()
        {
            if (!initialized || IsRetreating || CurrentObjective == null) return false;
            var lm = Core.LevelManager.Instance;
            var alt = lm != null ? lm.AlternativeObjectiveFor(CurrentObjective) : null;
            if (alt == null) return false;
            SetObjective(alt, autoReassess: false);
            return true;
        }

        public void AbandonObjective()
        {
            if (CurrentObjective != null) CurrentObjective.SetIsCurrent(false);
            CurrentObjective = null;
        }

        void Update()
        {
            if (!initialized || IsEscaped) return;
            float dt = Time.deltaTime;

            ProcessHazards(dt);
            ProcessConveyors(dt);
            GlobalTransitions();
            Fsm.Tick(dt);
            TickStuckWatchdog(dt);
            UpdateVisuals(dt);

            var grid = PathGrid.Instance;
            if (grid != null) Stats.Safety = grid.DangerAt(Pos);
        }

        Vector2 stuckAnchor;
        float stuckTimer;

        /// <summary>
        /// Safety net against pinning (belt vs waypoint, gate housings, future
        /// geometry): if a walking state makes no spatial progress for a while,
        /// re-plan instead of grinding in place forever.
        /// </summary>
        void TickStuckWatchdog(float dt)
        {
            var id = Fsm.CurrentId;
            bool walking = id == EngineerStateId.MoveToObjective || id == EngineerStateId.Panic ||
                           id == EngineerStateId.RetreatToExit;
            if (!walking || !Nav.HasPath)
            {
                stuckTimer = 0f;
                stuckAnchor = Pos;
                return;
            }
            if (Vector2.Distance(Pos, stuckAnchor) > 0.12f)
            {
                stuckAnchor = Pos;
                stuckTimer = 0f;
                return;
            }
            stuckTimer += dt;
            if (stuckTimer < 2.5f) return;
            stuckTimer = 0f;
            if (id == EngineerStateId.RetreatToExit) Nav.SetDestination(ExitPos, 0.5f);
            else Fsm.ChangeState(EngineerStateId.Repath);
        }

        void GlobalTransitions()
        {
            var id = Fsm.CurrentId;
            if (id == EngineerStateId.Escape) return;

            if (Stats.IsResolveEmpty &&
                id != EngineerStateId.Stunned && id != EngineerStateId.RetreatToExit)
            {
                Fsm.ChangeState(EngineerStateId.RetreatToExit);
                return;
            }

            if (!Stats.IsResolveEmpty && Data != null && Stats.Stress >= Data.panicEnterThreshold &&
                (id == EngineerStateId.MoveToObjective || id == EngineerStateId.Repath ||
                 id == EngineerStateId.AvoidHazard || id == EngineerStateId.RepairObjective ||
                 id == EngineerStateId.BackOff))
            {
                // Panic can now break an active repair: sirens/scanners that max his
                // stress really do pull him off the panel.
                if (id == EngineerStateId.RepairObjective) NotifyRepairInterrupted();
                Fsm.ChangeState(EngineerStateId.Panic);
            }
        }

        // ---------------- external effects ----------------

        /// <summary>A machine physically caught the engineer (arm grab, press, vehicle ram).</summary>
        public void StunHit(float stunDuration, string source)
        {
            if (IsEscaped) return;
            var tactics = TacticsData.Get();
            PendingStunDuration = stunDuration > 0f ? stunDuration : (Data != null ? Data.defaultStunDuration : 1.6f);
            float loss = Data != null ? Data.lossArmGrab : 5f;
            if (IsMarked)
            {
                // Drone mark synergy: a marked engineer is easier to catch cleanly.
                loss *= tactics.markedVulnerabilityMultiplier;
                PendingStunDuration += tactics.markedStunBonusSeconds;
            }
            Stats.LoseResolve(loss, source);
            Stats.AddStress(18f);
            AddPressure(8f, source);
            if (Fsm.CurrentId == EngineerStateId.RepairObjective) NotifyRepairInterrupted();
            Fsm.ChangeState(EngineerStateId.Stunned);
            StunnedBy?.Invoke(source);
        }

        public void ApplySlow(float factor, float duration)
        {
            slows.Add(new SlowEffect { factor = factor, until = Time.time + duration });
            if (slows.Count > 16) slows.RemoveAll(s => Time.time >= s.until);
        }

        public void AddStress(float amount) => Stats.AddStress(amount);

        public void DrainResolve(float perSecond, float dt, string source) =>
            Stats.LoseResolve(perSecond * dt, source);

        public void SetMarked(bool marked)
        {
            bool changed = IsMarked != marked;
            IsMarked = marked;
            if (markRing != null) markRing.gameObject.SetActive(marked);
            if (changed) MarkedChanged?.Invoke(marked);
        }

        /// <summary>
        /// Tutorial helper: put the engineer back at a spot and let him re-plan.
        /// Never used outside the tutorial room.
        /// </summary>
        public void TutorialReset(Vector2 pos)
        {
            var grid = PathGrid.Instance;
            if (grid != null) pos = grid.NearestFree(pos);
            transform.position = new Vector3(pos.x, pos.y, 0f);
            Nav.ClearPath();
            Stats.CalmStress(); // no panic in the lesson — he approaches calmly again
            if (Fsm != null && !IsEscaped) Fsm.ChangeState(EngineerStateId.AssessSituation);
        }

        public void NotifyRepairInterrupted()
        {
            AddPressure(8f, "repair interrupted");
            RepairInterrupted?.Invoke();
        }

        public void NotifyNoPath()
        {
            LastShift.Audio.AudioManager.PlayLimited("eng_blocked", 2.5f,
                "blocked_thud", LastShift.Audio.SfxBus.Engineer, 0.5f);
            Stats.LoseResolve(Data != null ? Data.lossNoPath : 6f, "no path");
            Stats.AddStress(10f);
            AddPressure(5f, "no path");
        }

        public void NotifyEscaped() => Escaped?.Invoke();

        // ---------------- per-frame world effects ----------------

        void ProcessHazards(float dt)
        {
            zoneScratch.Clear();
            foreach (var zone in insideZones) zoneScratch.Add(zone);
            foreach (var zone in zoneScratch)
                if (zone == null || !zone.Active || !zone.Contains(Pos)) insideZones.Remove(zone);

            for (int i = 0; i < HazardZone.All.Count; i++)
            {
                var zone = HazardZone.All[i];
                if (zone == null || !zone.Active || !zone.Contains(Pos)) continue;
                if (insideZones.Add(zone))
                {
                    // Fresh contact with a hazard: this is where Resolve is really lost.
                    LastShift.Audio.EngineerAudio.PlayHazardContact(zone.kind, Pos);
                    float loss = zone.ResolveOnEnter;
                    if (IsMarked) loss *= TacticsData.Get().markedVulnerabilityMultiplier;
                    Stats.LoseResolve(loss, zone.name);
                    Stats.AddStress(12f);
                    AddPressure(6f, zone.name);
                    if (Fsm.CurrentId == EngineerStateId.RepairObjective) NotifyRepairInterrupted();
                    HazardContact?.Invoke(zone.name);
                }
                Stats.AddStress(zone.StressPerSec * dt);
            }
        }

        void ProcessConveyors(float dt)
        {
            if (Fsm.CurrentId == EngineerStateId.Stunned) { /* still gets carried */ }
            // The room is already won when he retreats: he simply steps over the
            // belts on his way out — they cannot pin him away from the exit.
            Vector2 push = IsRetreating ? Vector2.zero : ConveyorMachine.TotalPushAt(Pos);
            if (push.sqrMagnitude < 0.001f)
            {
                conveyorDisplacement = Mathf.Max(0f, conveyorDisplacement - dt);
                return;
            }

            Vector2 target = Pos + push * dt;
            var grid = PathGrid.Instance;
            if (grid == null || grid.IsFree(target))
            {
                transform.position = new Vector3(target.x, target.y, 0f);
                // Being actively carried: poor footing — his own walking barely
                // works, so the belt genuinely carries him toward its end.
                ApplySlow(0.4f, 0.15f);
                conveyorDisplacement += push.magnitude * dt;
                if (conveyorDisplacement > 1.2f)
                {
                    conveyorDisplacement = 0f;
                    Stats.AddStress(9f);
                    ConveyorCarried?.Invoke();
                }
            }
            // Pressed against a belt end / gate housing / wall: the push cannot move
            // him and his footing is back — he steps off at full speed, no sticking.
        }

        void OnPathInvalidated()
        {
            var id = Fsm.CurrentId;
            if (id != EngineerStateId.MoveToObjective && id != EngineerStateId.Panic &&
                id != EngineerStateId.RetreatToExit) return;

            if (Time.time - lastRouteBlockedLossTime > 3f)
            {
                lastRouteBlockedLossTime = Time.time;
                Stats.LoseResolve(Data != null ? Data.lossRouteBlocked : 4f, "route blocked");
                Stats.AddStress(8f);
                AddPressure(5f, "route blocked");
            }
            if (id != EngineerStateId.RetreatToExit)
                Fsm.ChangeState(EngineerStateId.Repath);
        }

        static void AddPressure(float amount, string reason)
        {
            var lm = Core.LevelManager.Instance;
            if (lm != null && lm.Pressure != null) lm.Pressure.Add(amount, reason);
        }
    }
}
