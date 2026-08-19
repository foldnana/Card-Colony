using System;
using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public readonly struct NarrativeActorOrigin
    {
        public NarrativeActorOrigin(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    /// <summary>
    /// Owns narrative control of cards without overriding pauses held by combat,
    /// dialogue, or autonomous interaction systems.
    /// </summary>
    public sealed class NarrativeActorControlService : IDisposable
    {
        private sealed class ControlEntry
        {
            public NarrativeActorHandle Actor;
            public NarrativeActorOrigin Origin;
            public IDisposable ActivityLease;
            public int Count;
        }

        private readonly Dictionary<string, ControlEntry> controls =
            new(StringComparer.Ordinal);
        private bool disposed;

        public bool Acquire(NarrativeActorHandle actor)
        {
            if (disposed || actor?.Card == null ||
                string.IsNullOrWhiteSpace(actor.RoleId))
                return false;
            if (controls.TryGetValue(actor.RoleId, out ControlEntry existing))
            {
                existing.Count++;
                return true;
            }

            CardInstance card = actor.Card;
            var entry = new ControlEntry
            {
                Actor = actor,
                Origin = new NarrativeActorOrigin(
                    card.Stack?.TargetPosition ?? card.transform.position,
                    card.transform.rotation),
                Count = 1
            };
            LocationNpcActivity activity =
                card.GetComponent<LocationNpcActivity>();
            if (activity != null)
                entry.ActivityLease = activity.AcquirePause(this);
            controls.Add(actor.RoleId, entry);
            return true;
        }

        public bool Release(string roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId) ||
                !controls.TryGetValue(roleId, out ControlEntry entry))
                return false;
            entry.Count--;
            if (entry.Count > 0)
                return true;
            controls.Remove(roleId);
            entry.ActivityLease?.Dispose();
            return true;
        }

        public bool IsControlled(string roleId) =>
            !string.IsNullOrWhiteSpace(roleId) &&
            controls.ContainsKey(roleId);

        public bool TryGetOrigin(
            string roleId,
            out NarrativeActorOrigin origin)
        {
            if (!string.IsNullOrWhiteSpace(roleId) &&
                controls.TryGetValue(roleId, out ControlEntry entry))
            {
                origin = entry.Origin;
                return true;
            }
            origin = default;
            return false;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (ControlEntry entry in controls.Values)
                entry.ActivityLease?.Dispose();
            controls.Clear();
        }
    }
}
