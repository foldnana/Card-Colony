using System;
using System.Linq;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public static class CombatFocusService
    {
        public static CombatTask FocusedCombat { get; private set; }
        public static CardInstance SelectedActor { get; private set; }
        public static CardInstance SelectedTarget { get; private set; }
        public static CardInstance LastSelectedCard { get; private set; }

        public static event Action FocusChanged;

        public static void Select(CardInstance card)
        {
            CombatTask task = card?.Combatant?.CurrentCombatTask;
            if (task == null || !task.IsOngoing)
                return;

            FocusedCombat = task;
            LastSelectedCard = card;
            if (card.Definition?.Faction == CardFaction.Player)
                SelectedActor = card;
            else
                SelectedTarget = card;
            EnsureValidSelection();
            FocusChanged?.Invoke();
        }

        public static void Focus(CombatTask task)
        {
            FocusedCombat = task?.IsOngoing == true ? task : null;
            LastSelectedCard = null;
            EnsureValidSelection();
            FocusChanged?.Invoke();
        }

        public static void ClearIfFinished(CombatTask task)
        {
            if (FocusedCombat != task)
                return;
            Clear();
        }

        public static void Clear()
        {
            FocusedCombat = null;
            SelectedActor = null;
            SelectedTarget = null;
            LastSelectedCard = null;
            FocusChanged?.Invoke();
        }

        public static CombatTask ChooseDefault(IEnumerable<CombatTask> tasks)
        {
            List<CombatTask> candidates = tasks?
                .Where(task => task?.IsOngoing == true &&
                    task.PlayerCombatants.Any())
                .ToList() ?? new List<CombatTask>();
            return candidates.FirstOrDefault(task =>
                       task.PlayerCombatants.Any(ProtagonistRules.IsProtagonist)) ??
                   candidates.FirstOrDefault(task =>
                       SelectedActor != null &&
                       task.ContainsCombatant(SelectedActor.PersistentId)) ??
                   candidates.OrderByDescending(task => task.CreatedSequence)
                       .FirstOrDefault();
        }

        private static void EnsureValidSelection()
        {
            if (FocusedCombat == null)
            {
                SelectedActor = null;
                SelectedTarget = null;
                return;
            }

            if (SelectedActor?.Combatant?.CurrentCombatTask != FocusedCombat ||
                SelectedActor.CurrentHealth <= 0)
            {
                SelectedActor = FocusedCombat.PlayerCombatants.FirstOrDefault();
            }
            if (SelectedTarget?.Combatant?.CurrentCombatTask != FocusedCombat ||
                SelectedTarget.CurrentHealth <= 0)
            {
                SelectedTarget = FocusedCombat.LivingEnemiesOf(SelectedActor)
                    .FirstOrDefault();
            }
        }
    }
}
