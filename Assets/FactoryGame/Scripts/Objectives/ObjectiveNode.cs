using UnityEngine;
using LastShift.Utilities;

namespace LastShift.Objectives
{
    /// <summary>Base for anything in the room the engineer works with (repair points, exits).</summary>
    public class ObjectiveNode : MonoBehaviour
    {
        public string objectiveName = "Objective";
        public bool Completed { get; protected set; }

        public event System.Action<ObjectiveNode> CompletedEvent;

        public Vector2 Pos => transform.position;

        protected void RaiseCompleted()
        {
            if (Completed) return;
            Completed = true;
            CompletedEvent?.Invoke(this);
        }

        protected SpriteRenderer MakeBody(Color color, Vector2 size, int order = 4) =>
            Viz.Make("Body", transform, PlaceholderShape.Square, color, Vector2.zero, size, order);
    }
}
