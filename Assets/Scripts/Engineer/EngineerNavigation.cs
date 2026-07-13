using System.Collections.Generic;
using UnityEngine;
using LastShift.Utilities;

namespace LastShift.Engineer
{
    /// <summary>
    /// Grid path following for the engineer. Recomputes on demand, validates the
    /// remaining path whenever the grid changes, and raises PathInvalidated when a
    /// door/pallet/vehicle cuts the current route.
    /// </summary>
    public class EngineerNavigation : MonoBehaviour
    {
        public event System.Action PathInvalidated;

        readonly List<Vector2> path = new List<Vector2>(64);
        int waypointIndex;
        Vector2 destination;
        float lastDangerWeight = 3f;
        bool subscribed;

        public bool HasPath { get; private set; }
        public Vector2 Destination => destination;

        public bool Arrived =>
            !HasPath || (waypointIndex >= path.Count &&
            Vector2.Distance(transform.position, destination) < 0.35f);

        void OnEnable() => TrySubscribe();
        void OnDisable()
        {
            if (subscribed && PathGrid.Instance != null)
                PathGrid.Instance.GridChanged -= OnGridChanged;
            subscribed = false;
        }

        void TrySubscribe()
        {
            if (subscribed || PathGrid.Instance == null) return;
            PathGrid.Instance.GridChanged += OnGridChanged;
            subscribed = true;
        }

        public bool SetDestination(Vector2 dest, float dangerWeight)
        {
            TrySubscribe();
            destination = dest;
            lastDangerWeight = dangerWeight;
            var grid = PathGrid.Instance;
            if (grid == null) { HasPath = false; return false; }
            HasPath = grid.TryFindPath(transform.position, dest, dangerWeight, path);
            waypointIndex = 0;
            return HasPath;
        }

        public void ClearPath()
        {
            HasPath = false;
            path.Clear();
            waypointIndex = 0;
        }

        /// <summary>Moves along the current path. Returns true when the destination is reached.</summary>
        public bool Tick(float speed, float dt)
        {
            if (!HasPath) return false;
            if (waypointIndex >= path.Count) return true;

            Vector2 pos = transform.position;
            Vector2 target = path[waypointIndex];
            float step = speed * dt;
            while (step > 0f && waypointIndex < path.Count)
            {
                target = path[waypointIndex];
                float dist = Vector2.Distance(pos, target);
                if (dist <= step)
                {
                    pos = target;
                    step -= dist;
                    waypointIndex++;
                }
                else
                {
                    pos = Vector2.MoveTowards(pos, target, step);
                    step = 0f;
                }
            }
            transform.position = new Vector3(pos.x, pos.y, 0f);
            return waypointIndex >= path.Count;
        }

        void OnGridChanged()
        {
            var grid = PathGrid.Instance;
            if (grid == null) return;

            // If we are standing inside a newly blocked cell, nudge out of it.
            if (!grid.IsFree(transform.position))
                transform.position = grid.NearestFree(transform.position);

            if (!HasPath) return;
            for (int i = waypointIndex; i < path.Count; i++)
            {
                if (!grid.IsFree(path[i]))
                {
                    // Try a silent replan first; only report a blocked route when the
                    // replan fails or the detour is significantly longer.
                    int oldCount = path.Count - waypointIndex;
                    bool found = grid.TryFindPath(transform.position, destination, lastDangerWeight, path);
                    waypointIndex = 0;
                    if (!found)
                    {
                        HasPath = false;
                        PathInvalidated?.Invoke();
                    }
                    else if (path.Count > oldCount + 6)
                    {
                        PathInvalidated?.Invoke();
                    }
                    return;
                }
            }
        }
    }
}
