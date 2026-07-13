using System.Collections.Generic;
using UnityEngine;
using LastShift.Audio;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// Keyboard-driven command terminal logic: Up/Down select a ready command
    /// (wrapping, skipping cooldowns), Enter activates it. Selection also highlights
    /// the machine in the room and shows its effect preview.
    /// </summary>
    public class FactoryCommandTerminal : MonoBehaviour
    {
        readonly List<FactoryCommandItem> items = new List<FactoryCommandItem>();

        public IReadOnlyList<FactoryCommandItem> Items => items;
        public int SelectedIndex { get; private set; } = -1;

        public InteractableMachine Selected =>
            SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex].machine : null;

        public bool AnyReady
        {
            get
            {
                for (int i = 0; i < items.Count; i++)
                    if (items[i].IsReady) return true;
                return false;
            }
        }

        public void Init(List<InteractableMachine> machines)
        {
            items.Clear();
            foreach (var m in machines)
                if (m != null) items.Add(new FactoryCommandItem(m));
            SelectFirstReady();
        }

        public int ReadyCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < items.Count; i++)
                    if (items[i].IsReady) n++;
                return n;
            }
        }

        void Update()
        {
            if (items.Count == 0) return;

            // Cooldown recovery: the player should normally have at least two ready
            // commands. Speed up all cooldowns while below that (fully stalled = fastest).
            int ready = ReadyCount;
            InteractableMachine.GlobalCooldownScale = ready == 0 ? 2.4f : ready < 2 ? 1.6f : 1f;

            var lm = Core.LevelManager.Instance;
            if (lm != null && lm.InputLocked) return;

            if (GameInput.UpPressed) { int prev = SelectedIndex; Step(-1); if (SelectedIndex != prev) UiSfx.TerminalMove(); }
            if (GameInput.DownPressed) { int prev = SelectedIndex; Step(+1); if (SelectedIndex != prev) UiSfx.TerminalMove(); }
            if (GameInput.ConfirmPressed) ActivateSelected();

            // If nothing was ever selectable at init, grab the first ready command
            // as soon as one becomes available.
            if (SelectedIndex < 0 && AnyReady) SelectFirstReady();
        }

        void Step(int delta)
        {
            if (!AnyReady || items.Count == 0) return; // keep current selection visible
            int start = Mathf.Max(0, SelectedIndex);
            int idx = start;
            for (int i = 0; i < items.Count; i++)
            {
                idx = (idx + delta + items.Count) % items.Count;
                if (items[idx].IsReady) { Select(idx); return; }
            }
        }

        void ActivateSelected()
        {
            var machine = Selected;
            if (machine == null || !machine.TryActivate())
            {
                // No system selected, or the command is busy/cooling down: soft error buzz.
                UiSfx.Denied();
                return;
            }
            UiSfx.Confirm();
            // Command entered its active/cooldown phase: move to the next ready one.
            if (!machine.IsReady) SelectNextReady(keepIfNone: true);
        }

        void SelectFirstReady()
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].IsReady) { Select(i); return; }
            if (items.Count > 0) Select(0);
        }

        void SelectNextReady(bool keepIfNone)
        {
            int start = Mathf.Max(0, SelectedIndex);
            for (int i = 1; i <= items.Count; i++)
            {
                int idx = (start + i) % items.Count;
                if (items[idx].IsReady) { Select(idx); return; }
            }
            // No ready command: keep the current one visible ("ALL SYSTEMS COOLDOWN").
        }

        void Select(int index)
        {
            if (SelectedIndex == index) return;
            var prev = Selected;
            if (prev != null) prev.SetSelected(false);
            SelectedIndex = index;
            var next = Selected;
            if (next != null) next.SetSelected(true);
        }
    }
}
