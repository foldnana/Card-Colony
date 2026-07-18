using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum QuestType
    {
        // Cards
        Have,
        Obtain,
        Discover,
        Defeat,
        Craft,

        // Actions
        Sell,
        Buy,
        Equip,
        Explore,

        // Time
        Time,
        Day,

        // Stats
        Food,
        Coins,
        Capacity
    }

    [CreateAssetMenu(fileName = "New Quest", menuName = "StackCraft/Quest")]
    public class Quest : ScriptableObject
    {
        // Info
        [SerializeField, Tooltip("A unique identifier for this quest. Automatically generated if left empty.")]
        private string id;

        [SerializeField, Tooltip("The short, display name of the quest.")]
        private string title;

        [SerializeField, TextArea, Tooltip("The detailed description or guide for the quest.")]
        private string description;

        // Requirements
        [SerializeField, Tooltip("The type of action or condition required to complete this quest.")]
        private QuestType type;

        [SerializeField, Tooltip("The specific CardDefinition (item, character, enemy, etc.) related to the quest requirement.")]
        private CardDefinition targetCard;

        [SerializeField, Tooltip("The specific RecipeDefinition required for 'Craft' type quests.")]
        private RecipeDefinition targetRecipe;

        [SerializeField, Min(1), Tooltip("The quantity or duration required to complete the quest.")]
        private int targetAmount = 1;

        [SerializeField, Tooltip("The time pace (e.g., Normal, Fast) to monitor for 'Time' quests.")]
        private TimePace targetPace = TimePace.Normal;

        // Flow
        [SerializeField, Tooltip("Quests that must be completed before this quest becomes available.")]
        private List<Quest> prerequisiteQuests;

        [SerializeField, Tooltip("Quests that will be unlocked and activated upon successful completion of this quest.")]
        private List<Quest> questsToUnlock;

        // Info
        public string Id => id;
        public string Title => ChineseLocalization.QuestTitle(title);
        public string Description => ChineseLocalization.QuestDescription(title, description);

        // Requirements
        public QuestType Type => type;
        public CardDefinition TargetCard => targetCard;
        public RecipeDefinition TargetRecipe => targetRecipe;
        public int TargetAmount => targetAmount;
        public TimePace TargetPace => targetPace;

        // Flow
        public List<Quest> PrerequisiteQuests => prerequisiteQuests;
        public List<Quest> QuestsToUnlock => questsToUnlock;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = System.Guid.NewGuid().ToString("N");
        }
    }
}
