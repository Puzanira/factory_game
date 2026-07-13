using System.Collections.Generic;
using UnityEngine;

namespace LastShift.Utilities
{
    /// <summary>
    /// Simple grid-based A* pathfinding for one room. Cells can be blocked by any number
    /// of blockers (walls, closed doors, pallets, parked vehicles) and can carry a
    /// "danger" cost so the engineer prefers safe routes. Chosen over NavMesh because
    /// Unity has no built-in 2D NavMesh and a grid handles dynamic blocking reliably.
    /// </summary>
    public class PathGrid : MonoBehaviour
    {
        public static PathGrid Instance { get; private set; }

        /// <summary>Raised whenever blocking changes (doors, pallets, vehicles...).</summary>
        public event System.Action GridChanged;

        float cellSize = 0.5f;
        int width, height;
        Vector2 origin; // bottom-left corner of the grid in world space

        int[] blocked;   // blocker counts per cell
        float[] danger;  // additive danger cost per cell

        public static PathGrid Create(Vector2 center, Vector2 size, float cell = 0.5f)
        {
            var go = new GameObject("PathGrid");
            var grid = go.AddComponent<PathGrid>();
            grid.Init(center, size, cell);
            return grid;
        }

        public void Init(Vector2 center, Vector2 size, float cell)
        {
            Instance = this;
            cellSize = cell;
            width = Mathf.Max(2, Mathf.RoundToInt(size.x / cell));
            height = Mathf.Max(2, Mathf.RoundToInt(size.y / cell));
            origin = center - new Vector2(width * cell, height * cell) * 0.5f;
            blocked = new int[width * height];
            danger = new float[width * height];
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        int Idx(int x, int y) => y * width + x;
        bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

        public Vector2Int WorldToCell(Vector2 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / cellSize), 0, width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((p.y - origin.y) / cellSize), 0, height - 1);
            return new Vector2Int(x, y);
        }

        public Vector2 CellToWorld(Vector2Int c) =>
            origin + new Vector2((c.x + 0.5f) * cellSize, (c.y + 0.5f) * cellSize);

        // ---------------- blocking / danger ----------------

        public void BlockRect(Rect r, bool add)
        {
            ForEachCellIn(r, (x, y) => blocked[Idx(x, y)] += add ? 1 : -1);
            GridChanged?.Invoke();
        }

        public void AddDanger(Rect r, float amount)
        {
            ForEachCellIn(r, (x, y) => danger[Idx(x, y)] = Mathf.Max(0f, danger[Idx(x, y)] + amount));
        }

        void ForEachCellIn(Rect r, System.Action<int, int> action)
        {
            // Affect cells whose center lies inside the rect (slightly expanded so
            // thin rects still register).
            Rect rr = new Rect(r.x - cellSize * 0.25f, r.y - cellSize * 0.25f,
                               r.width + cellSize * 0.5f, r.height + cellSize * 0.5f);
            Vector2Int min = WorldToCell(new Vector2(rr.xMin, rr.yMin));
            Vector2Int max = WorldToCell(new Vector2(rr.xMax, rr.yMax));
            for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                {
                    Vector2 c = CellToWorld(new Vector2Int(x, y));
                    if (rr.Contains(c)) action(x, y);
                }
        }

        public bool IsBlockedCell(Vector2Int c) => !InBounds(c.x, c.y) || blocked[Idx(c.x, c.y)] > 0;
        public bool IsFree(Vector2 world) => !IsBlockedCell(WorldToCell(world));

        public float DangerAt(Vector2 world)
        {
            Vector2Int c = WorldToCell(world);
            return InBounds(c.x, c.y) ? danger[Idx(c.x, c.y)] : 0f;
        }

        /// <summary>Nearest unblocked cell center (BFS). Falls back to the input point.</summary>
        public Vector2 NearestFree(Vector2 world)
        {
            Vector2Int start = WorldToCell(world);
            if (!IsBlockedCell(start)) return CellToWorld(start);
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            int guard = 0;
            while (queue.Count > 0 && guard++ < 4000)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Vector2Int n in Neighbors4(c))
                {
                    if (visited.Contains(n) || !InBounds(n.x, n.y)) continue;
                    if (!IsBlockedCell(n)) return CellToWorld(n);
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
            return world;
        }

        /// <summary>Nearest unblocked cell with danger below the threshold.</summary>
        public Vector2 NearestSafe(Vector2 world, float maxDanger)
        {
            Vector2Int start = WorldToCell(world);
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            int guard = 0;
            while (queue.Count > 0 && guard++ < 4000)
            {
                Vector2Int c = queue.Dequeue();
                if (!IsBlockedCell(c) && danger[Idx(c.x, c.y)] <= maxDanger && c != start)
                    return CellToWorld(c);
                foreach (Vector2Int n in Neighbors4(c))
                {
                    if (visited.Contains(n) || !InBounds(n.x, n.y)) continue;
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
            return NearestFree(world);
        }

        static IEnumerable<Vector2Int> Neighbors4(Vector2Int c)
        {
            yield return new Vector2Int(c.x + 1, c.y);
            yield return new Vector2Int(c.x - 1, c.y);
            yield return new Vector2Int(c.x, c.y + 1);
            yield return new Vector2Int(c.x, c.y - 1);
        }

        // ---------------- A* ----------------

        static readonly Vector2Int[] Dirs8 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        /// <summary>
        /// A* over the grid (8-directional, no corner cutting). Danger adds to traversal
        /// cost scaled by dangerWeight, so paths avoid hazards without being forbidden.
        /// </summary>
        public bool TryFindPath(Vector2 fromWorld, Vector2 toWorld, float dangerWeight, List<Vector2> result)
        {
            result.Clear();
            Vector2Int start = WorldToCell(NearestFree(fromWorld));
            Vector2Int goal = WorldToCell(toWorld);
            if (IsBlockedCell(goal)) goal = WorldToCell(NearestFree(toWorld));
            if (IsBlockedCell(start) || IsBlockedCell(goal)) return false;
            if (start == goal) { result.Add(CellToWorld(goal)); return true; }

            int n = width * height;
            var gScore = new float[n];
            var cameFrom = new int[n];
            var state = new byte[n]; // 0 untouched, 1 open, 2 closed
            for (int i = 0; i < n; i++) { gScore[i] = float.MaxValue; cameFrom[i] = -1; }

            var open = new List<int>(128);
            int startIdx = Idx(start.x, start.y);
            int goalIdx = Idx(goal.x, goal.y);
            gScore[startIdx] = 0f;
            open.Add(startIdx);
            state[startIdx] = 1;

            float Heuristic(int idx)
            {
                int x = idx % width, y = idx / width;
                float dx = Mathf.Abs(x - goal.x), dy = Mathf.Abs(y - goal.y);
                return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
            }

            int guard = 0;
            while (open.Count > 0 && guard++ < 20000)
            {
                // Linear min-scan is fine for room-sized grids (< ~1000 cells).
                int best = 0;
                float bestF = gScore[open[0]] + Heuristic(open[0]);
                for (int i = 1; i < open.Count; i++)
                {
                    float f = gScore[open[i]] + Heuristic(open[i]);
                    if (f < bestF) { bestF = f; best = i; }
                }
                int current = open[best];
                open.RemoveAt(best);
                state[current] = 2;

                if (current == goalIdx)
                {
                    // Reconstruct.
                    var cells = new List<int>();
                    int c = current;
                    while (c != -1) { cells.Add(c); c = cameFrom[c]; }
                    cells.Reverse();
                    foreach (int idx in cells)
                        result.Add(CellToWorld(new Vector2Int(idx % width, idx / width)));
                    if (result.Count > 0) result[result.Count - 1] = CellToWorld(goal);
                    return true;
                }

                int cx = current % width, cy = current / width;
                foreach (Vector2Int d in Dirs8)
                {
                    int nx = cx + d.x, ny = cy + d.y;
                    if (!InBounds(nx, ny)) continue;
                    int ni = Idx(nx, ny);
                    if (blocked[ni] > 0 || state[ni] == 2) continue;
                    // No cutting corners diagonally past blocked cells.
                    if (d.x != 0 && d.y != 0)
                    {
                        if (blocked[Idx(cx + d.x, cy)] > 0 || blocked[Idx(cx, cy + d.y)] > 0) continue;
                    }
                    float step = (d.x != 0 && d.y != 0) ? 1.4142f : 1f;
                    float tentative = gScore[current] + step + danger[ni] * dangerWeight;
                    if (tentative < gScore[ni])
                    {
                        gScore[ni] = tentative;
                        cameFrom[ni] = current;
                        if (state[ni] != 1) { open.Add(ni); state[ni] = 1; }
                    }
                }
            }
            return false;
        }
    }
}
