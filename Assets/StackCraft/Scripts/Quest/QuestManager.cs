using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [System.Serializable]
    public class QuestGroup
    {
        public string GroupName;
        public List<Quest> Quests = new();
    }

    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        public event System.Action<QuestInstance> OnQuestActivated;
        // public event System.Action<QuestInstance> OnQuestProgress;
        public event System.Action<QuestInstance> OnQuestCompleted;

        [SerializeField, Tooltip("A list of all organized quest groups.")]
        private List<QuestGroup> questGroups;

        public IEnumerable<QuestGroup> QuestGroups => questGroups;
        public IEnumerable<QuestInstance> AllQuests => completedQuests.Concat(activeQuests);
        public int CompletedQuestsCount => completedQuestIDs.Count;

        private readonly List<QuestInstance> activeQuests = new();
        private readonly HashSet<string> completedQuestIDs = new();
        private readonly List<QuestInstance> completedQuests = new();

        private Dictionary<string, Quest> questLookup = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildQuestLookup();

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;
            }
        }

        private void BuildQuestLookup()
        {
            questLookup.Clear();
            foreach (var group in questGroups)
            {
                foreach (var quest in group.Quests)
                {
                    if (!questLookup.ContainsKey(quest.Id))
                    {
                        questLookup.Add(quest.Id, quest);
                    }
                    else
                    {
                        Debug.LogError($"QuestManager: Duplicate Quest ID detected: {quest.Id} on {quest.name}");
                    }
                }
            }
        }

        private void Start()
        {
            // --- SUBSCRIBE TO ALL GAME EVENTS ---
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardCreated += HandleCardCreated;
                CardManager.Instance.OnCardKilled += HandleCardKilled;
                CardManager.Instance.OnCardEquipped += HandleCardEquipped;
                CardManager.Instance.OnStatsChanged += HandleStatsChanged;
            }

            if (CraftingManager.Instance != null)
            {
                CraftingManager.Instance.OnRecipeDiscovered += HandleRecipeDiscovered;
                CraftingManager.Instance.OnCraftingFinished += HandleCraftingFinished;
                CraftingManager.Instance.OnExplorationFinished += HandleExplorationFinished;
            }

            if (TradeManager.Instance != null)
            {
                TradeManager.Instance.OnCardsSold += HandleCardsSold;
                TradeManager.Instance.OnPackPurchased += HandlePackPurchased;
            }

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimePaceChanged += HandleTimePaceChanged;
                TimeManager.Instance.OnDayStarted += HandleDayChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady -= HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;
            }

            // --- UNSUBSCRIBE TO ALL GAME EVENTS ---
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardCreated -= HandleCardCreated;
                CardManager.Instance.OnCardKilled -= HandleCardKilled;
                CardManager.Instance.OnCardEquipped -= HandleCardEquipped;
                CardManager.Instance.OnStatsChanged -= HandleStatsChanged;
            }

            if (CraftingManager.Instance != null)
            {
                CraftingManager.Instance.OnRecipeDiscovered -= HandleRecipeDiscovered;
                CraftingManager.Instance.OnCraftingFinished -= HandleCraftingFinished;
                CraftingManager.Instance.OnExplorationFinished -= HandleExplorationFinished;
            }

            if (TradeManager.Instance != null)
            {
                TradeManager.Instance.OnCardsSold -= HandleCardsSold;
                TradeManager.Instance.OnPackPurchased -= HandlePackPurchased;
            }

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimePaceChanged -= HandleTimePaceChanged;
                TimeManager.Instance.OnDayStarted -= HandleDayChanged;
            }
        }

        private void HandleSceneDataReady(SceneData sceneData, bool wasLoaded)
        {
            if (wasLoaded)
            {
                RestoreQuests(sceneData);
            }
            else
            {
                InitializeDefaultQuests();
            }
        }

        private void HandleBeforeSave(GameData gameData)
        {
            if (gameData.TryGetScene(out var sceneData))
            {
                sceneData.SaveQuests(completedQuestIDs.ToList(), activeQuests);
                sceneData.QuestProgress = Mathf.RoundToInt(100f * completedQuestIDs.Count / questLookup.Count);
            }
        }

        private void InitializeDefaultQuests()
        {
            foreach (var group in questGroups)
            {
                if (group == null || group.Quests == null) continue;

                foreach (var quest in group.Quests)
                {
                    if (quest.PrerequisiteQuests == null || quest.PrerequisiteQuests.Count == 0)
                    {
                        ActivateQuest(quest);
                    }
                }
            }
        }

        private void RestoreQuests(SceneData data)
        {
            // 1. Clear current state
            activeQuests.Clear();
            completedQuestIDs.Clear();
            completedQuests.Clear();

            // 2. Restore completed quests
            if (data.CompletedQuests != null)
            {
                foreach (var id in data.CompletedQuests)
                {
                    completedQuestIDs.Add(id);

                    // Reconstruct the Instance for UI display
                    if (questLookup.TryGetValue(id, out Quest questDef))
                    {
                        QuestInstance completedInstance = new QuestInstance(questDef);
                        completedInstance.Status = QuestStatus.Completed;
                        completedInstance.SetProgress(questDef.TargetAmount);

                        completedQuests.Add(completedInstance);
                    }
                }
            }

            // 3. Restore Active Quests
            if (data.ActiveQuests != null)
            {
                foreach (var saveData in data.ActiveQuests)
                {
                    if (questLookup.TryGetValue(saveData.QuestId, out Quest questDef))
                    {
                        QuestInstance instance = new QuestInstance(questDef);
                        instance.Status = QuestStatus.Active;
                        instance.SetProgress(saveData.CurrentAmount);

                        activeQuests.Add(instance);
                        OnQuestActivated?.Invoke(instance);
                    }
                    else
                    {
                        Debug.LogWarning($"QuestManager: Could not find Quest Definition for ID '{saveData.QuestId}'. Skipping.");
                    }
                }
            }
        }

        private void ActivateQuest(Quest questData)
        {
            if (questData == null || completedQuestIDs.Contains(questData.Id)) return;
            if (activeQuests.Any(q => q.QuestData == questData)) return;

            QuestInstance newQuest = new QuestInstance(questData);
            newQuest.Status = QuestStatus.Active;
            activeQuests.Add(newQuest);

            OnQuestActivated?.Invoke(newQuest);

            // Immediate check for specific quest types upon activation
            if (newQuest.QuestData.Type == QuestType.Have)
            {
                HandleStatsChanged(CardManager.Instance.GetStatsSnapshot());
            }
            else if (newQuest.QuestData.Type == QuestType.Discover)
            {
                foreach (var recipeId in CraftingManager.Instance.DiscoveredRecipes)
                {
                    HandleRecipeDiscovered(recipeId);
                }
            }
        }

        private void CheckForCompletion(QuestInstance quest)
        {
            if (quest.IsComplete())
            {
                quest.Status = QuestStatus.Completed;
                activeQuests.Remove(quest);
                completedQuestIDs.Add(quest.QuestData.Id);
                completedQuests.Add(quest);

                // Unlock next quests
                foreach (var nextQuest in quest.QuestData.QuestsToUnlock)
                {
                    if (CanActivate(nextQuest))
                    {
                        ActivateQuest(nextQuest);
                    }
                }

                OnQuestCompleted?.Invoke(quest);
            }
            else
            {
                // OnQuestProgress?.Invoke(quest);
            }
        }

        private bool CanActivate(Quest questData)
        {
            // Check if all prerequisites are in the completed list
            return questData.PrerequisiteQuests.All(
                prereq => completedQuestIDs.Contains(prereq.Id)
            );
        }

        #region  Event Handlers
        // --- HAVE | FOOD | COINS | CAPACITY ---
        private void HandleStatsChanged(StatsSnapshot stats)
        {
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Have ||
                     q.QuestData.Type == QuestType.Food ||
                     q.QuestData.Type == QuestType.Coins ||
                     q.QuestData.Type == QuestType.Capacity
            ).ToList();

            foreach (var quest in matchingQuests)
            {
                switch (quest.QuestData.Type)
                {
                    case QuestType.Have:
                        if (quest.QuestData.TargetCard.Category == CardCategory.Currency)
                        {
                            quest.SetProgress(stats.Currency);
                        }
                        else
                        {
                            int count = CardManager.Instance.AllCards
                                .Count(c => c.BaseDefinition == quest.QuestData.TargetCard);
                            quest.SetProgress(count);
                        }
                        break;

                    case QuestType.Food:
                        quest.SetProgress(stats.TotalNutrition);
                        break;

                    case QuestType.Coins:
                        quest.SetProgress(stats.Currency);
                        break;

                    case QuestType.Capacity:
                        quest.SetProgress(stats.CardLimit);
                        break;
                }

                CheckForCompletion(quest);
            }
        }

        // --- OBTAIN ---
        private void HandleCardCreated(CardInstance newCard)
        {
            if (newCard == null) return;

            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Obtain &&
                     q.QuestData.TargetCard == newCard.BaseDefinition
            ).ToList(); // .ToList() to avoid modifying collection while iterating

            foreach (var quest in matchingQuests)
            {
                quest.AddProgress(1);
                CheckForCompletion(quest);
            }
        }

        // --- DISCOVER ---
        private void HandleRecipeDiscovered(string recipeId)
        {
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Discover &&
                     q.QuestData.TargetRecipe.Id == recipeId
            ).ToList();

            foreach (var quest in matchingQuests)
            {
                quest.SetProgress(1);
                CheckForCompletion(quest);
            }
        }

        // --- DEFEAT ---
        private void HandleCardKilled(CardInstance killedCard)
        {
            if (killedCard == null) return;

            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Defeat &&
                     q.QuestData.TargetCard == killedCard.Definition
            ).ToList();

            foreach (var quest in matchingQuests)
            {
                quest.AddProgress(1);
                CheckForCompletion(quest);
            }
        }

        // --- CRAFT ---
        private void HandleCraftingFinished(CardDefinition resultCard)
        {
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Craft &&
                     q.QuestData.TargetCard == resultCard
            ).ToList();

            foreach (var quest in matchingQuests)
            {
                quest.AddProgress(1);
                CheckForCompletion(quest);
            }
        }

        // --- SELL ---
        private void HandleCardsSold(CardStack soldStack)
        {
            int totalSoldCount = soldStack.Cards.Count;
            // If no cards were in the stack, exit.
            if (totalSoldCount == 0) return;

            // 1. Count all sold cards, grouped by their base definition.
            var soldGroups = soldStack.Cards
                .GroupBy(card => card.BaseDefinition)
                .ToDictionary(g => g.Key, g => g.Count());

            // 2. Get all active "Sell" quests
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Sell
            ).ToList(); // .ToList() to safely iterate

            // 3. Iterate through quests and apply progress
            foreach (var quest in matchingQuests)
            {
                var targetCard = quest.QuestData.TargetCard;

                if (targetCard != null)
                {
                    // --- Case 1: Quest is for a SPECIFIC card ---
                    // Check if the dictionary has an entry for the card this quest tracks.
                    if (soldGroups.TryGetValue(targetCard, out int countSold))
                    {
                        quest.AddProgress(countSold);
                        CheckForCompletion(quest);
                    }
                }
                else
                {
                    // --- Case 2: Quest is for ANY card (TargetCard is null) ---
                    // Add the total number of cards that were just sold.
                    quest.AddProgress(totalSoldCount);
                    CheckForCompletion(quest);
                }
            }
        }

        // --- BUY ---
        private void HandlePackPurchased(PackDefinition purchasedPack)
        {
            if (purchasedPack == null) return;

            // Get all active "Buy" quests
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Buy
            ).ToList(); // .ToList() to safely iterate

            foreach (var quest in matchingQuests)
            {
                // Since PackDefinition is a CardDefinition, we can use TargetCard.
                var targetPack = quest.QuestData.TargetCard;

                if (targetPack != null)
                {
                    // --- Case 1: Quest is for a SPECIFIC pack ---
                    if (targetPack == purchasedPack)
                    {
                        quest.AddProgress(1);
                        CheckForCompletion(quest);
                    }
                }
                else
                {
                    // --- Case 2: Quest is for ANY pack ---
                    quest.AddProgress(1);
                    CheckForCompletion(quest);
                }
            }
        }

        // --- EQUIP ---
        private void HandleCardEquipped(CardDefinition equipmentCard)
        {
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Equip &&
                     q.QuestData.TargetCard == equipmentCard
            ).ToList();

            foreach (var quest in matchingQuests)
            {
                quest.SetProgress(1);
                CheckForCompletion(quest);
            }
        }

        // --- EXPLORE ---
        private void HandleExplorationFinished(CardDefinition areaCard)
        {
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Explore &&
                     q.QuestData.TargetCard == areaCard
            ).ToList();

            foreach (var quest in matchingQuests)
            {
                quest.SetProgress(1);
                CheckForCompletion(quest);
            }
        }

        // --- TIME ---
        private void HandleTimePaceChanged(TimePace timePace)
        {
            var matchingQuests = activeQuests.Where(
                 q => q.QuestData.Type == QuestType.Time &&
                      q.QuestData.TargetPace == timePace
             ).ToList();

            foreach (var quest in matchingQuests)
            {
                quest.SetProgress(1);
                CheckForCompletion(quest);
            }
        }

        // --- DAY ---
        private void HandleDayChanged(int currentDay)
        {
            var matchingQuests = activeQuests.Where(
                q => q.QuestData.Type == QuestType.Day
            ).ToList();

            foreach (var quest in matchingQuests)
            {
                quest.SetProgress(currentDay);

                CheckForCompletion(quest);
            }
        }
        #endregion
    }
}
