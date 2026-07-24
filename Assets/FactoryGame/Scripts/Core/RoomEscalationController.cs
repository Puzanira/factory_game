using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastShift.Data;
using LastShift.Hazards;
using LastShift.Machines;

namespace LastShift.Core
{
    /// <summary>
    /// Room emergency state at 100 Pressure: big Resolve hit, damaged objective,
    /// expanded hazards, and flagged machines firing on their own while the room
    /// stays live. The engineer gets time to react and run — the level does not end
    /// instantly.
    /// </summary>
    public class RoomEscalationController : MonoBehaviour
    {
        public bool Escalated { get; private set; }

        LevelManager lm;
        EscalationData data;
        readonly List<InteractableMachine> autoMachines = new List<InteractableMachine>();
        readonly List<HazardZone> expandHazards = new List<HazardZone>();

        public void Init(LevelManager levelManager, EscalationData escalationData,
            List<InteractableMachine> machines, List<HazardZone> hazards)
        {
            lm = levelManager;
            data = escalationData;
            foreach (var m in machines)
                if (m != null && m.escalationAuto) autoMachines.Add(m);
            foreach (var h in hazards)
                if (h != null && h.escalationExpand) expandHazards.Add(h);
            lm.Pressure.MaxReached += Trigger;
        }

        public void Trigger()
        {
            if (Escalated) return;
            Escalated = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            float penalty = data != null ? data.resolvePenalty : 20f;
            float interval = data != null ? data.autoActivateInterval : 4f;
            float expandScale = data != null ? data.hazardExpandScale : 1.6f;
            float damageFraction = data != null ? data.objectiveDamageFraction : 0.4f;

            var engineer = LastShift.Engineer.EngineerController.Instance;
            if (engineer != null)
            {
                engineer.Stats.LoseResolve(penalty, "ROOM ESCALATION");
                engineer.AddStress(40f);
            }

            var currentObjective = lm.CurrentObjectiveForHud;
            if (currentObjective != null && !currentObjective.Completed)
                currentObjective.Damage(damageFraction, "ROOM ESCALATION");

            foreach (var h in expandHazards)
                if (h != null) h.Expand(expandScale);

            // Rolling auto-activation of flagged machinery until the room ends.
            int next = 0;
            while (!lm.RoomEnded)
            {
                yield return new WaitForSeconds(interval);
                if (lm.RoomEnded || autoMachines.Count == 0) break;
                for (int tries = 0; tries < autoMachines.Count; tries++)
                {
                    var m = autoMachines[next];
                    next = (next + 1) % autoMachines.Count;
                    if (m != null && m.State != MachineState.Active)
                    {
                        m.ForceActivate();
                        break;
                    }
                }
            }
        }
    }
}
