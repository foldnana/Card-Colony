using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum EncounterType
    {
        SpecificDay,    // Happens exactly on Day X
        Recurring,      // Happens every X days (e.g. every 7 days)
        Range,          // Happens between Day X and Y
        MinimumDay      // Happens any day after Day X
    }

    [CreateAssetMenu(fileName = "Encounter_", menuName = "StackCraft/Encounter")]
    public class EncounterDefinition : ScriptableObject
    {
        // Identity
        [SerializeField, Tooltip("A unique identifier for this encounter. Automatically generated and used for tracking one-time events in save files.")]
        private string id;

        [SerializeField, TextArea, Tooltip("The message displayed to the player when this encounter triggers. Leave empty for a silent spawn.")]
        private string notificationMessage;

        // Spawn Settings
        [SerializeField, Tooltip("The CardDefinition asset that will be instantiated on the board during this encounter.")]
        private CardDefinition cardToSpawn;

        [SerializeField, Tooltip("How many instances of this card should be created. Multiple cards will spawn at different random positions.")]
        private int count = 1;

        [SerializeField, Tooltip("If true, this encounter happens only once per save file.")]
        private bool oneTimeOnly = false;

        // Conditions
        [SerializeField, Tooltip("The rule for when this encounter can be triggered based on the current in-game day.")]
        private EncounterType type;

        [SerializeField, Tooltip("The primary day value used for comparison. Its meaning depends on the selected Encounter Type.")]
        private int dayValue;

        [SerializeField, Tooltip("Only used when the Encounter Type is 'Range'. This is the last day (inclusive) on which the encounter can occur.")]
        private int maxDayValue = 999;

        // Priority & Chance
        [SerializeField, Tooltip("Higher numbers are evaluated first.")]
        private int priority = 0;

        [SerializeField, Range(0f, 1f), Tooltip("1.0 = 100%")]
        private float chance = 1.0f;

        // Constraints
        [SerializeField, Tooltip("Don't spawn if we already have this many cards on board.")]
        private int maxCardsOnBoardLimit = 100;

        public string Id => id;
        public string NotificationMessage => ChineseLocalization.EncounterMessage(notificationMessage);

        public CardDefinition CardToSpawn => cardToSpawn;
        public int Count => count;
        public bool OneTimeOnly => oneTimeOnly;

        public EncounterType Type => type;

        public int Priority => priority;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = System.Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Evaluates if this encounter is valid for the specific day.
        /// </summary>
        public bool IsValidForDay(int currentDay, HashSet<string> completedEncounters, int totalCardsOnBoard, bool isFriendlyMode)
        {
            // 1. Check Global Limits
            if (totalCardsOnBoard >= maxCardsOnBoardLimit) return false;
            if (oneTimeOnly && completedEncounters.Contains(id)) return false;
            if (cardToSpawn.IsAggressive && isFriendlyMode) return false;

            // 2. Check Day Logic
            bool dayConditionMet = false;
            switch (type)
            {
                case EncounterType.SpecificDay:
                    dayConditionMet = (currentDay == dayValue);
                    break;
                case EncounterType.Recurring:
                    dayConditionMet = (currentDay > 0 && currentDay % dayValue == 0);
                    break;
                case EncounterType.MinimumDay:
                    dayConditionMet = (currentDay >= dayValue);
                    break;
                case EncounterType.Range:
                    dayConditionMet = (currentDay >= dayValue && currentDay <= maxDayValue);
                    break;
            }

            if (!dayConditionMet) return false;

            // 3. Check RNG
            return Random.value <= chance;
        }
    }
}
