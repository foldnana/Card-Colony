using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Stores the party member selected by the player. The persistent id is used
    /// so the selection survives scene changes and card instance reconstruction.
    /// </summary>
    public static class PartySelectionService
    {
        public static event Action<string> SelectionChanged;

        public static string SelectedPersistentId { get; private set; }

        public static void Select(string persistentId)
        {
            string normalized = string.IsNullOrWhiteSpace(persistentId)
                ? null
                : persistentId;
            if (SelectedPersistentId == normalized)
                return;

            SelectedPersistentId = normalized;
            SelectionChanged?.Invoke(SelectedPersistentId);
        }

        public static void EnsureValidSelection(
            IEnumerable<CardData> members,
            string preferredPersistentId = null)
        {
            List<CardData> available = members?
                .Where(member => member != null &&
                    !string.IsNullOrWhiteSpace(member.PersistentId))
                .Take(GameData.MaximumPartySize)
                .ToList() ?? new List<CardData>();

            if (available.Any(member =>
                    member.PersistentId == SelectedPersistentId))
            {
                return;
            }

            CardData preferred = available.FirstOrDefault(member =>
                member.PersistentId == preferredPersistentId);
            Select((preferred ?? available.FirstOrDefault())?.PersistentId);
        }
    }
}
