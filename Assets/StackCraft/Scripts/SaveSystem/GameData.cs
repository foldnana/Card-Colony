using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [System.Serializable]
    public class GameData
    {
        public int SlotNumber;
        public string CurrentScene;
        public GameplayPrefs GameplayPrefs;
        public Dictionary<string, SceneData> SavedScenes = new();
        public HashSet<string> DiscoveredCards = new();
        public HashSet<string> DiscoveredRecipes = new();
        public HashSet<string> SeenItems = new();
        public System.DateTime LastSaved;

        public GameData() { }

        public GameData(int slotNumber, GameplayPrefs prefs)
        {
            SlotNumber = slotNumber;
            GameplayPrefs = prefs;
        }

        public bool TryGetScene(out SceneData sceneData)
        {
            if (SavedScenes.TryGetValue(CurrentScene, out sceneData))
            {
                return true;
            }

            sceneData = new SceneData(CurrentScene);
            SavedScenes.Add(CurrentScene, sceneData);
            return false;
        }
    }

    [System.Serializable]
    public class SceneData
    {
        public string SceneName;
        public List<StackData> SavedStacks = new();
        public List<CombatData> SavedCombats = new();
        public List<string> CompletedQuests = new();
        public List<QuestData> ActiveQuests = new();
        public List<VendorData> SavedVendors = new();
        public HashSet<string> CompletedEncounters = new();
        public TimeData SavedTime;
        public int QuestProgress;

        public SceneData() { }

        public SceneData(string sceneName)
        {
            SceneName = sceneName;
            SavedTime = new TimeData();
        }

        public void SaveStacks(List<CardStack> stacks)
        {
            SavedStacks.Clear();

            foreach (var stack in stacks)
            {
                var stackData = new StackData(stack);
                SavedStacks.Add(stackData);
            }
        }

        public void SaveCombats(List<CombatTask> activeCombats)
        {
            SavedCombats.Clear();

            foreach (var task in activeCombats)
            {
                if (task.IsOngoing)
                {
                    SavedCombats.Add(new CombatData(task));
                }
            }
        }

        public void SaveQuests(List<string> completed, List<QuestInstance> active)
        {
            CompletedQuests = new List<string>(completed);

            ActiveQuests.Clear();
            foreach (var quest in active)
            {
                ActiveQuests.Add(new QuestData(quest));
            }
        }

        public void SaveVendors(List<PackVendor> vendors)
        {
            SavedVendors.Clear();

            foreach (var vendor in vendors)
            {
                SavedVendors.Add(new VendorData(vendor.PackId, vendor.PaidAmount));
            }
        }
    }

    [System.Serializable]
    public class StackData
    {
        public float[] Position;
        public List<CardData> Cards = new();
        public CraftingData ActiveCraft;

        public StackData() { }

        public StackData(CardStack stack)
        {
            Vector3 stackPos = stack.TargetPosition;
            Position = new float[] { stackPos.x, stackPos.y, stackPos.z };

            foreach (var card in stack.Cards)
            {
                var cardData = new CardData(card);
                Cards.Add(cardData);
            }

            if (stack.IsCrafting)
            {
                var task = CraftingManager.Instance.GetCraftingTask(stack);
                if (task != null)
                {
                    ActiveCraft = new CraftingData(task.Recipe.Id, task.Progress);
                }
            }
        }

        public Vector3 GetPosition()
        {
            return new Vector3(
                Position[0],    // X
                Position[1],    // Y
                Position[2]     // Z
            );
        }
    }

    [System.Serializable]
    public class CardData
    {
        public string Id;
        public int UsesLeft;
        public int CurrentHealth;
        public int CurrentNutrition;
        public int StoredCoins;

        public string OriginalId; // Stores "Villager" if current Id is "Warrior"
        public List<CardData> EquippedItems = new();

        public CardData() { }

        public CardData(CardInstance card)
        {
            Id = card.Definition.Id;
            UsesLeft = card.UsesLeft;
            CurrentHealth = card.CurrentHealth;
            CurrentNutrition = card.CurrentNutrition;

            if (card.TryGetComponent<ChestLogic>(out var chest))
            {
                StoredCoins = chest.StoredCoins;
            }

            if (card.EquipperComponent != null && card.Definition.Category == CardCategory.Character)
            {
                // 1. Save Original Definition ID if we have undergone a class change
                if (card.EquipperComponent.OriginalDefinition != null)
                {
                    OriginalId = card.EquipperComponent.OriginalDefinition.Id;
                }

                // 2. Recursively save all equipped items
                foreach (var item in card.EquipperComponent.EquippedCards)
                {
                    EquippedItems.Add(new CardData(item));
                }
            }
        }
    }

    [System.Serializable]
    public class CombatData
    {
        public List<CardData> Attackers = new();
        public List<CardData> Defenders = new();
        public bool PlayerIsAttacker;
        public float[] RectPosition;

        public CombatData() { }

        public CombatData(CombatTask task)
        {
            PlayerIsAttacker = task.PlayerIsAttacker;

            foreach (var card in task.Attackers)
            {
                Attackers.Add(new CardData(card));
            }

            foreach (var card in task.Defenders)
            {
                Defenders.Add(new CardData(card));
            }

            if (task.Rect != null)
            {
                Vector3 pos = task.Rect.transform.position;
                RectPosition = new float[] { pos.x, pos.y, pos.z };
            }
        }
    }

    [System.Serializable]
    public class CraftingData
    {
        public string RecipeId;
        public float Progress;

        public CraftingData() { }

        public CraftingData(string recipeId, float progress)
        {
            RecipeId = recipeId;
            Progress = progress;
        }
    }

    [System.Serializable]
    public class QuestData
    {
        public string QuestId;
        public int CurrentAmount;

        public QuestData() { }

        public QuestData(QuestInstance questInstance)
        {
            QuestId = questInstance.QuestData.Id;
            CurrentAmount = questInstance.CurrentAmount;
        }
    }

    [System.Serializable]
    public class VendorData
    {
        public string PackId;
        public int PaidAmount;

        public VendorData() { }

        public VendorData(string packId, int paidAmount)
        {
            PackId = packId;
            PaidAmount = paidAmount;
        }
    }

    [System.Serializable]
    public class TimeData
    {
        public float CurrentTime;
        public int CurrentDay;

        public TimeData() { }

        public TimeData(float currentTime, int currentDay)
        {
            CurrentTime = currentTime;
            CurrentDay = currentDay;
        }
    }

    [System.Serializable]
    public class GameplayPrefs
    {
        public int DayDuration;
        public bool IsFriendlyMode;

        public GameplayPrefs() { }

        public GameplayPrefs(int dayDuration, bool isFriendlyMode)
        {
            DayDuration = dayDuration;
            IsFriendlyMode = isFriendlyMode;
        }
    }
}
