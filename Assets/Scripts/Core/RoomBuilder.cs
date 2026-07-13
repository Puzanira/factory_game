using System.Collections.Generic;
using UnityEngine;
using LastShift.Data;
using LastShift.Hazards;
using LastShift.Machines;
using LastShift.Objectives;
using LastShift.Utilities;

namespace LastShift.Core
{
    /// <summary>Everything RoomBuilder produced for one room.</summary>
    public class RoomRefs
    {
        public readonly List<InteractableMachine> machines = new List<InteractableMachine>();
        public readonly List<HazardZone> hazards = new List<HazardZone>();
        public readonly List<RepairObjective> objectives = new List<RepairObjective>();
        public ExitDoor exit;
    }

    /// <summary>
    /// Instantiates a RoomLayout into the scene: floor, walls, decor, hazards,
    /// machines, objectives and the exit door. Prefabs come from the PrefabLibrary;
    /// missing entries fall back to code factories so nothing can be left unassigned.
    /// </summary>
    public static class RoomBuilder
    {
        const float WallThickness = 0.5f;
        static readonly Color WallColor = new Color(0.30f, 0.33f, 0.37f);
        static readonly Color FloorColor = new Color(0.137f, 0.149f, 0.168f);
        static readonly Color InnerFloorColor = new Color(0.18f, 0.196f, 0.219f);

        public static RoomRefs Build(RoomLayout layout, PrefabLibrary lib, Transform parent)
        {
            var refs = new RoomRefs();
            var root = new GameObject("Room_" + layout.roomName.Replace(' ', '_'));
            root.transform.SetParent(parent, false);
            Transform t = root.transform;

            BuildLighting(layout, t);
            BuildFloor(layout, t);
            BuildBorderWalls(layout, t);

            foreach (var o in layout.obstacles) BuildRect(o, t, blocks: true);
            foreach (var d in layout.decor) BuildRect(d, t, blocks: false);

            foreach (var h in layout.hazards)
            {
                HazardZone zone;
                GameObject prefab = lib != null ? lib.HazardPrefab(h.kind) : null;
                if (prefab != null)
                {
                    var go = Object.Instantiate(prefab, t);
                    zone = go.GetComponent<HazardZone>();
                    if (zone == null) zone = go.AddComponent<HazardZone>();
                    zone.Setup(h.kind, h.rect, h.startsActive);
                }
                else
                {
                    zone = HazardFactory.Create(h.kind, h.rect, h.startsActive, t);
                }
                zone.escalationExpand = h.escalationExpand;
                refs.hazards.Add(zone);
            }

            foreach (var spec in layout.machines)
            {
                GameObject prefab = lib != null ? lib.MachinePrefab(spec.kind) : null;
                GameObject go = prefab != null
                    ? Object.Instantiate(prefab, t)
                    : PrefabFactories.CreateMachine(spec.kind);
                if (prefab == null) go.transform.SetParent(t, false);
                go.name = "Machine_" + spec.displayName.Replace(' ', '_');
                var machine = go.GetComponent<InteractableMachine>();
                machine.Configure(spec);
                refs.machines.Add(machine);
            }

            foreach (var spec in layout.objectives)
            {
                GameObject go = lib != null && lib.repairObjective != null
                    ? Object.Instantiate(lib.repairObjective, t)
                    : PrefabFactories.CreateRepairObjective();
                if (go.transform.parent != t) go.transform.SetParent(t, false);
                var objective = go.GetComponent<RepairObjective>();
                objective.Setup(spec);
                refs.objectives.Add(objective);
            }

            {
                GameObject go = lib != null && lib.exitDoor != null
                    ? Object.Instantiate(lib.exitDoor, t)
                    : PrefabFactories.CreateExitDoor();
                if (go.transform.parent != t) go.transform.SetParent(t, false);
                refs.exit = go.GetComponent<ExitDoor>();
                refs.exit.Setup(layout.exitPos, layout.exitSize);
            }

            return refs;
        }

        static void BuildLighting(RoomLayout layout, Transform t)
        {
            // Room mood: warm for production halls, cold for storage (from floorTint).
            Color ambient = Color.white;
            if (layout.floorTint.a > 0.001f)
            {
                Color tint = new Color(layout.floorTint.r, layout.floorTint.g, layout.floorTint.b, 1f);
                ambient = Color.Lerp(Color.white, tint, 0.30f);
            }
            var light = FxFactory.GlobalLight(ambient, 1.0f);
            light.transform.SetParent(t, false);

            // Corner work lamps for gentle pseudo-depth.
            float hx = layout.roomSize.x * 0.5f - 1.2f;
            float hy = layout.roomSize.y * 0.5f - 1.2f;
            Color lampColor = layout.floorTint.a > 0.001f && layout.floorTint.b > layout.floorTint.r
                ? new Color(0.75f, 0.85f, 1f)   // cold hall
                : new Color(1f, 0.92f, 0.75f);  // warm hall
            FxFactory.PointLight(t, new Vector2(-hx, hy), lampColor, 5f, 0.35f);
            FxFactory.PointLight(t, new Vector2(hx, hy), lampColor, 5f, 0.35f);
            FxFactory.PointLight(t, new Vector2(-hx, -hy), lampColor, 5f, 0.3f);
            FxFactory.PointLight(t, new Vector2(hx, -hy), lampColor, 5f, 0.3f);
        }

        static void BuildFloor(RoomLayout layout, Transform t)
        {
            Vector2 full = layout.roomSize + new Vector2(WallThickness * 2f, WallThickness * 2f);

            // Tiling metal panels with seams, bolts and grime.
            Viz.MakeTiled("Floor", t, TextureFactory.FloorTile(new Color(0.42f, 0.46f, 0.52f)),
                new Color(0.62f, 0.65f, 0.7f), Vector2.zero, full, 0);

            // Floor drains along the middle.
            var drainColor = new Color(0.1f, 0.11f, 0.13f, 0.85f);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 p = new Vector2(i * layout.roomSize.x * 0.3f, -layout.roomSize.y * 0.32f);
                Viz.Make("DrainRing", t, PlaceholderShape.Ring, drainColor, p, new Vector2(0.5f, 0.5f), 1);
                Viz.Make("DrainCore", t, PlaceholderShape.Circle, drainColor, p, new Vector2(0.28f, 0.28f), 1);
            }

            // Sparse dark stains for grime (deterministic positions).
            for (int i = 0; i < 6; i++)
            {
                float fx = Mathf.Sin(i * 2.39996f) * layout.roomSize.x * 0.38f;
                float fy = Mathf.Cos(i * 2.39996f) * layout.roomSize.y * 0.36f;
                var stain = new GameObject("Stain" + i);
                stain.transform.SetParent(t, false);
                stain.transform.localPosition = new Vector3(fx, fy, 0f);
                stain.transform.localScale = new Vector3(1.2f + (i % 3) * 0.5f, 0.9f + (i % 2) * 0.4f, 1f);
                var sr = stain.AddComponent<SpriteRenderer>();
                sr.sprite = TextureFactory.Puddle(i * 31 + 7);
                sr.sharedMaterial = SpriteFactory.LitMaterial;
                sr.color = new Color(0.05f, 0.05f, 0.06f, 0.14f);
                sr.sortingOrder = 1;
            }

            if (layout.floorTint.a > 0.001f)
                Viz.Make("FloorTint", t, PlaceholderShape.Square, layout.floorTint, Vector2.zero, full, 1, unlit: true);
        }

        static void BuildBorderWalls(RoomLayout layout, Transform t)
        {
            float hw = layout.roomSize.x / 2f;
            float hh = layout.roomSize.y / 2f;
            float wt = WallThickness;

            // Bottom / left / right run the full edge.
            Wall(t, new Vector2(0f, -hh - wt / 2f), new Vector2(layout.roomSize.x + wt * 2f, wt));
            Wall(t, new Vector2(-hw - wt / 2f, 0f), new Vector2(wt, layout.roomSize.y + wt * 2f));
            Wall(t, new Vector2(hw + wt / 2f, 0f), new Vector2(wt, layout.roomSize.y + wt * 2f));

            // Top wall leaves a gap for the exit (the locked ExitDoor blocks the gap itself).
            float gapMin = layout.exitPos.x - layout.exitSize.x / 2f;
            float gapMax = layout.exitPos.x + layout.exitSize.x / 2f;
            float leftLen = gapMin - (-hw);
            float rightLen = hw - gapMax;
            if (leftLen > 0.05f)
                Wall(t, new Vector2(-hw + leftLen / 2f, hh + wt / 2f), new Vector2(leftLen, wt));
            if (rightLen > 0.05f)
                Wall(t, new Vector2(gapMax + rightLen / 2f, hh + wt / 2f), new Vector2(rightLen, wt));
        }

        static void Wall(Transform t, Vector2 pos, Vector2 size)
        {
            var sr = Viz.MakeTiled("Wall", t, TextureFactory.WallTexture(WallColor), Color.white, pos, size, 5);

            // Structural beams / vents along long walls for silhouette variety.
            bool horizontal = size.x >= size.y;
            float length = horizontal ? size.x : size.y;
            if (length > 4f)
            {
                int marks = Mathf.FloorToInt(length / 4f);
                for (int i = 0; i < marks; i++)
                {
                    float f = (i + 0.5f) / marks - 0.5f;
                    Vector2 p = pos + (horizontal ? new Vector2(f * size.x, 0f) : new Vector2(0f, f * size.y));
                    Viz.Make("WallVent", t, PlaceholderShape.Square, new Color(0.16f, 0.18f, 0.2f),
                        p, horizontal ? new Vector2(0.5f, size.y * 0.5f) : new Vector2(size.x * 0.5f, 0.5f), 6);
                }
            }

            var blocker = sr.gameObject.AddComponent<GridBlocker>();
            blocker.size = size;
            blocker.SetBlocked(true);
        }

        static void BuildRect(RectSpec spec, Transform t, bool blocks)
        {
            if (!blocks)
            {
                Viz.Make("Decor", t, PlaceholderShape.Square, spec.color, spec.pos, spec.size, spec.sortingOrder);
                return;
            }

            // Obstacles get a drop shadow, a top highlight (pseudo-depth) and edging.
            var holder = new GameObject("Obstacle");
            holder.transform.SetParent(t, false);
            holder.transform.localPosition = new Vector3(spec.pos.x, spec.pos.y, 0f);
            FxFactory.Shadow(holder.transform, spec.size * 1.15f, 0.35f, 4);
            Viz.Make("Body", holder.transform, PlaceholderShape.Square, spec.color, Vector2.zero, spec.size, 5);
            Viz.Make("TopHighlight", holder.transform, PlaceholderShape.Square,
                new Color(1f, 1f, 1f, 0.16f), new Vector2(0f, spec.size.y * 0.38f),
                new Vector2(spec.size.x, spec.size.y * 0.22f), 6);
            Viz.Make("BottomShade", holder.transform, PlaceholderShape.Square,
                new Color(0f, 0f, 0f, 0.28f), new Vector2(0f, -spec.size.y * 0.42f),
                new Vector2(spec.size.x, spec.size.y * 0.14f), 6);

            // Wooden pallets get slats; steel tanks get a rounded lid.
            bool wooden = spec.color.r > spec.color.b + 0.15f;
            if (wooden)
            {
                int slats = Mathf.Max(2, Mathf.FloorToInt(spec.size.x / 0.45f));
                for (int i = 0; i < slats; i++)
                {
                    float f = (i + 0.5f) / slats - 0.5f;
                    Viz.Make("Slat", holder.transform, PlaceholderShape.Square,
                        new Color(0f, 0f, 0f, 0.22f), new Vector2(f * spec.size.x, 0f),
                        new Vector2(0.045f, spec.size.y * 0.92f), 6);
                }
            }
            else if (Mathf.Abs(spec.size.x - spec.size.y) < 0.3f)
            {
                Viz.Make("TankLid", holder.transform, PlaceholderShape.Circle,
                    new Color(1f, 1f, 1f, 0.22f), new Vector2(0f, 0.05f),
                    spec.size * 0.6f, 6);
                Viz.Make("TankCap", holder.transform, PlaceholderShape.Circle,
                    new Color(0f, 0f, 0f, 0.3f), new Vector2(0f, 0.05f),
                    spec.size * 0.2f, 7);
            }

            if (spec.blocksPath)
            {
                var blocker = holder.AddComponent<GridBlocker>();
                blocker.size = spec.size;
                blocker.SetBlocked(true);
            }
        }
    }
}
