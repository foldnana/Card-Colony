using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class CombatantTurnState
    {
        public string CombatantId { get; }
        public float ActionProgress { get; set; }
        public long JoinSequence { get; }
        public bool IsAlive { get; set; } = true;
        public bool IsBeingDragged { get; set; }

        public CombatantTurnState(
            string combatantId,
            float actionProgress,
            long joinSequence)
        {
            CombatantId = combatantId;
            ActionProgress = actionProgress;
            JoinSequence = joinSequence;
        }
    }

    public sealed class CombatActionScheduler
    {
        public const float ActionThreshold = 100f;
        public const float ProgressCap = 150f;

        public float AddProgress(float current, float attackSpeed, float deltaTime)
        {
            return Math.Clamp(
                current + Math.Max(0f, attackSpeed) * Math.Max(0f, deltaTime),
                0f,
                ProgressCap);
        }

        public CombatantTurnState SelectReadyCombatant(
            IEnumerable<CombatantTurnState> combatants)
        {
            return combatants?
                .Where(state => state != null && state.IsAlive &&
                    !state.IsBeingDragged && state.ActionProgress >= ActionThreshold)
                .OrderByDescending(state => state.ActionProgress)
                .ThenBy(state => state.JoinSequence)
                .FirstOrDefault();
        }

        public float ConsumeAction(float progress)
        {
            return Math.Max(0f, progress - ActionThreshold);
        }
    }
}
