using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum LocationTransitionReason
    {
        None,
        WorldMapEntry,
        ChildLocationEntry,
        ReturnToParent
    }

    [System.Serializable]
    public class GameData
    {
        public const int CurrentEconomyStateVersion = 1;
        public const int CurrentProtagonistStateVersion = 1;
        public const int CurrentWorldQuestStateVersion = 2;
        public const int MaximumPartySize = 4;

        public int SlotNumber;
        public string CurrentScene;
        public string ActiveLocationId;
        public int WorldDay;
        public int EconomyStateVersion;
        public int EconomySeed;
        public long WorldElapsedHours;
        public int ProtagonistStateVersion;
        public string ProtagonistPersistentId;
        public int WorldQuestStateVersion;
        public List<MarketStateData> Markets = new();
        public List<WorldQuestStateData> WorldQuests = new();
        public List<WorldFactData> WorldFacts = new();
        public NarrativeHistoryData Narrative = new();
        public List<string> ProcessedWorldQuestEventIds = new();
        public string TrackedWorldQuestId;
        public List<string> LocationHistory = new();
        public List<CardData> PartyMembers = new();
        public List<BuildingPersonSlotData> BuildingPersonSlots = new();
        public BuildingEntryContext ActiveBuildingEntry;
        public BackpackData Backpack = new();
        public GameplayPrefs GameplayPrefs;
        public Dictionary<string, SceneData> SavedScenes = new();
        public HashSet<string> DiscoveredCards = new();
        public HashSet<string> DiscoveredRecipes = new();
        public HashSet<string> SeenItems = new();
        public System.DateTime LastSaved;

        [System.NonSerialized]
        private LocationTransitionReason pendingLocationTransition;

        public GameData() { }

        public GameData(int slotNumber, GameplayPrefs prefs)
        {
            SlotNumber = slotNumber;
            GameplayPrefs = prefs;
        }

        public bool TryGetScene(out SceneData sceneData)
        {
            string sceneScope = GetCurrentSceneScope();
            if (string.IsNullOrWhiteSpace(sceneScope))
            {
                sceneData = new SceneData(string.Empty);
                return false;
            }

            if (SavedScenes.TryGetValue(sceneScope, out sceneData))
            {
                return true;
            }

            sceneData = new SceneData(sceneScope);
            SavedScenes.Add(sceneScope, sceneData);
            return false;
        }

        public string GetCurrentSceneScope()
        {
            return CurrentScene == "Location" && !string.IsNullOrWhiteSpace(ActiveLocationId)
                ? $"Location/{ActiveLocationId}"
                : CurrentScene;
        }

        public int GetWorldDay(int fallbackDay = 1)
        {
            if (WorldDay <= 0)
                WorldDay = Mathf.Max(1, fallbackDay);

            return WorldDay;
        }

        public void SetWorldDay(int day)
        {
            WorldDay = Mathf.Max(1, day);
            WorldElapsedHours = System.Math.Max(
                WorldElapsedHours,
                (long)(WorldDay - 1) * 24L);
        }

        public void EnsureEconomyState()
        {
            Markets ??= new List<MarketStateData>();
            if (EconomySeed == 0)
            {
                unchecked
                {
                    uint hash = 2166136261u;
                    hash ^= (uint)SlotNumber;
                    hash *= 16777619u;
                    foreach (char character in
                             CurrentScene ?? string.Empty)
                    {
                        hash ^= character;
                        hash *= 16777619u;
                    }
                    EconomySeed = hash == 0u
                        ? 1
                        : (int)hash;
                }
            }
            if (WorldElapsedHours <= 0 && WorldDay > 1)
                WorldElapsedHours = (long)(WorldDay - 1) * 24L;
        }

        public BuildingPersonSlotData AssignMemberToBuildingSlot(
            string settlementLocationId,
            string entranceInstanceId,
            string interiorLocationId,
            string occupantPersistentId)
        {
            BuildingPersonSlots ??= new List<BuildingPersonSlotData>();
            if (string.IsNullOrWhiteSpace(settlementLocationId) ||
                string.IsNullOrWhiteSpace(entranceInstanceId) ||
                string.IsNullOrWhiteSpace(interiorLocationId) ||
                string.IsNullOrWhiteSpace(occupantPersistentId))
            {
                return null;
            }

            BuildingPersonSlots.RemoveAll(slot =>
                slot == null ||
                slot.OccupantPersistentId == occupantPersistentId ||
                slot.SettlementLocationId == settlementLocationId &&
                slot.EntranceInstanceId == entranceInstanceId);
            var assignment = new BuildingPersonSlotData
            {
                SettlementLocationId = settlementLocationId,
                EntranceInstanceId = entranceInstanceId,
                InteriorLocationId = interiorLocationId,
                OccupantPersistentId = occupantPersistentId
            };
            BuildingPersonSlots.Add(assignment);
            return assignment;
        }

        public BuildingPersonSlotData FindBuildingPersonSlot(
            string settlementLocationId,
            string entranceInstanceId)
        {
            return BuildingPersonSlots?.FirstOrDefault(slot =>
                slot != null &&
                slot.SettlementLocationId == settlementLocationId &&
                slot.EntranceInstanceId == entranceInstanceId);
        }

        public void ClearBuildingPersonSlot(
            string settlementLocationId,
            string entranceInstanceId)
        {
            BuildingPersonSlots?.RemoveAll(slot =>
                slot == null ||
                slot.SettlementLocationId == settlementLocationId &&
                slot.EntranceInstanceId == entranceInstanceId);
        }

        public void ClearBuildingSlotsForSettlement(string settlementLocationId)
        {
            if (string.IsNullOrWhiteSpace(settlementLocationId))
                return;

            BuildingPersonSlots?.RemoveAll(slot =>
                slot == null ||
                slot.SettlementLocationId == settlementLocationId);
        }

        public void ClearAllBuildingPersonSlots()
        {
            BuildingPersonSlots ??= new List<BuildingPersonSlotData>();
            BuildingPersonSlots.Clear();
        }

        public void MergePartyMemberStates(IEnumerable<CardData> changedMembers)
        {
            PartyMembers ??= new List<CardData>();
            if (changedMembers == null)
                return;

            foreach (CardData changed in changedMembers.Where(member =>
                         member != null &&
                         !string.IsNullOrWhiteSpace(member.PersistentId)))
            {
                int index = PartyMembers.FindIndex(member =>
                    member?.PersistentId == changed.PersistentId);
                if (index >= 0)
                    PartyMembers[index] = changed;
            }
        }

        public void PushLocation(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                return;

            LocationHistory ??= new List<string>();
            LocationHistory.Add(locationId);
        }

        public bool TryPopLocation(out string locationId)
        {
            locationId = null;
            if (LocationHistory == null || LocationHistory.Count == 0)
                return false;

            int lastIndex = LocationHistory.Count - 1;
            locationId = LocationHistory[lastIndex];
            LocationHistory.RemoveAt(lastIndex);
            return true;
        }

        public void MarkLocationPartyTransferPending()
        {
            MarkLocationTransitionPending(LocationTransitionReason.WorldMapEntry);
        }

        public bool ConsumeLocationPartyTransferPending()
        {
            return ConsumeLocationTransitionReason() !=
                LocationTransitionReason.None;
        }

        public void MarkLocationTransitionPending(LocationTransitionReason reason)
        {
            pendingLocationTransition = reason;
        }

        public LocationTransitionReason ConsumeLocationTransitionReason()
        {
            LocationTransitionReason reason = pendingLocationTransition;
            pendingLocationTransition = LocationTransitionReason.None;
            return reason;
        }

        public BackpackData EnsureBackpack()
        {
            Backpack ??= new BackpackData();
            Backpack.Normalize();
            return Backpack;
        }

        public CardData GetProtagonistData()
        {
            if (PartyMembers == null ||
                string.IsNullOrWhiteSpace(ProtagonistPersistentId))
            {
                return null;
            }

            return PartyMembers.FirstOrDefault(member =>
                member != null &&
                member.PersistentId == ProtagonistPersistentId);
        }

        public CardData EnsureProtagonist(
            string fallbackDefinitionId,
            int fallbackMaxHealth,
            int fallbackEnergy)
        {
            PartyMembers ??= new List<CardData>();
            CardData protagonist = GetProtagonistData();
            if (protagonist == null)
            {
                protagonist = PartyMembers.FirstOrDefault(member =>
                    member != null &&
                    member.Id == fallbackDefinitionId);
                protagonist ??= PartyMembers.FirstOrDefault(member => member != null);
            }

            if (protagonist == null)
            {
                protagonist = new CardData
                {
                    Id = fallbackDefinitionId,
                    UsesLeft = 1,
                    CurrentHealth = Mathf.Max(1, fallbackMaxHealth),
                    MaximumHealth = Mathf.Max(1, fallbackMaxHealth)
                };
                PartyMembers.Add(protagonist);
            }

            if (string.IsNullOrWhiteSpace(protagonist.PersistentId))
                protagonist.PersistentId = System.Guid.NewGuid().ToString("N");

            ProtagonistPersistentId = protagonist.PersistentId;
            if (ProtagonistStateVersion < CurrentProtagonistStateVersion)
            {
                protagonist.Level = Mathf.Max(1, protagonist.Level);
                protagonist.Experience = Mathf.Max(0, protagonist.Experience);
                protagonist.MaxEnergy = protagonist.MaxEnergy > 0
                    ? protagonist.MaxEnergy
                    : Mathf.Max(1, fallbackEnergy);
                protagonist.CurrentEnergy = protagonist.CurrentEnergy > 0
                    ? Mathf.Min(protagonist.CurrentEnergy, protagonist.MaxEnergy)
                    : protagonist.MaxEnergy;
                protagonist.IsDowned = protagonist.CurrentHealth <= 0;
                if (protagonist.MaximumHealth <= 0)
                {
                    protagonist.MaximumHealth =
                        Mathf.Max(1, fallbackMaxHealth) +
                        CharacterProgressionService.GetMaxHealthBonus(
                            protagonist.Level);
                }
                ProtagonistStateVersion = CurrentProtagonistStateVersion;
            }
            else
            {
                protagonist.NormalizeProgression(fallbackEnergy);
                if (protagonist.MaximumHealth <= 0)
                {
                    protagonist.MaximumHealth =
                        Mathf.Max(1, fallbackMaxHealth) +
                        CharacterProgressionService.GetMaxHealthBonus(
                            protagonist.Level);
                }
            }

            return protagonist;
        }

        public void UpdatePartyMembers(IEnumerable<CardData> partyMembers)
        {
            CardData existingProtagonist = GetProtagonistData();
            List<CardData> incoming = partyMembers?
                .Where(member => member != null)
                .ToList() ?? new List<CardData>();

            CardData protagonist = string.IsNullOrWhiteSpace(
                    ProtagonistPersistentId)
                ? null
                : incoming.FirstOrDefault(member =>
                    member.PersistentId == ProtagonistPersistentId);
            if (protagonist == null && existingProtagonist != null)
            {
                protagonist = existingProtagonist;
                incoming.Insert(0, protagonist);
            }
            PartyMembers = protagonist == null
                ? incoming.Take(MaximumPartySize).ToList()
                : new[] { protagonist }
                    .Concat(incoming.Where(member => member != protagonist))
                    .Take(MaximumPartySize)
                    .ToList();

            if (!string.IsNullOrWhiteSpace(ProtagonistPersistentId))
                return;

            CardData first = PartyMembers.FirstOrDefault();
            if (first == null)
                return;

            if (string.IsNullOrWhiteSpace(first.PersistentId))
                first.PersistentId = System.Guid.NewGuid().ToString("N");
            ProtagonistPersistentId = first.PersistentId;
        }

        public bool IsProtagonist(CardData cardData)
        {
            return ProtagonistRules.IsProtagonist(this, cardData);
        }
    }

    [System.Serializable]
    public sealed class NarrativeHistoryData
    {
        public NarrativeRunStateData ActiveRun;
        public List<string> CommittedResultIds = new();
        public List<string> CompletedOnceNarrativeIds = new();
        public List<string> CompletedSourceRunKeys = new();
        public List<NarrativeRunCounterData> LastRunNumbers = new();
        public List<string> PendingTriggerInstanceIds = new();
    }

    public enum NarrativeResumePolicy
    {
        ResumeFromCheckpoint = 0,
        RestartCheckpointNode = 1,
        AbortIfInteractionMissing = 2
    }

    [System.Serializable]
    public sealed class NarrativeRunStateData
    {
        public string NarrativeId;
        public int NarrativeVersion;
        public string RunId;
        public string CheckpointNodeId;
        public int CheckpointCommandIndex;
        public List<string> SelectedChoiceIds = new();
        public List<string> CommittedResultIds = new();
        public NarrativeWaitingInteractionData WaitingInteraction;
        public NarrativeResumePolicy ResumePolicy =
            NarrativeResumePolicy.ResumeFromCheckpoint;
    }

    [System.Serializable]
    public sealed class NarrativeWaitingInteractionData
    {
        public string OperationId;
        public string ActionId;
        public string ContextId;
        public string RecoveryToken;
        public List<string> InitiatorActorIds = new();
        public List<string> TargetActorIds = new();
        public List<NarrativeInteractionArgumentData> Arguments = new();
        public float TimeoutSeconds;
        public bool CreditedToParty;
        public bool ProtagonistParticipated;
    }

    [System.Serializable]
    public sealed class NarrativeInteractionArgumentData
    {
        public string Key;
        public InteractionValueType ValueType;
        public string StringValue;
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
    }

    [System.Serializable]
    public sealed class NarrativeRunCounterData
    {
        public string NarrativeId;
        public int LastRunNumber;
    }

    [System.Serializable]
    public class SceneData
    {
        public string SceneName;
        public int ContentMigrationVersion;
        public int LastRandomRefreshDay;
        public int RandomSeed;
        public List<StackData> SavedStacks = new();
        public List<CombatData> SavedCombats = new();
        public List<string> CompletedQuests = new();
        public List<QuestData> ActiveQuests = new();
        public List<VendorData> SavedVendors = new();
        public List<MarketStockData> MarketStock = new();
        public List<NpcTradeStateData> NpcTrades = new();
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
                if (stackData.Cards.Count > 0)
                    SavedStacks.Add(stackData);
            }
        }

        public void SaveCombats(List<CombatTask> activeCombats)
        {
            if (activeCombats.Any(task =>
                    task != null && task.Phase == CombatPhase.ResolvingAction))
            {
                return;
            }

            SavedCombats.Clear();

            foreach (var task in activeCombats)
            {
                if (task.IsOngoing && task.Phase == CombatPhase.Running)
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
                if (card == null || card.IsNarrativeTemporary)
                    continue;
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
    public sealed class BuildingPersonSlotData
    {
        public string SettlementLocationId;
        public string EntranceInstanceId;
        public string InteriorLocationId;
        public string OccupantPersistentId;
    }

    [System.Serializable]
    public sealed class BuildingEntryContext
    {
        public string ParentLocationId;
        public string EntranceInstanceId;
        public string InteriorLocationId;
        public List<string> ParticipantPersistentIds = new();

        public bool ObserverOnly =>
            ParticipantPersistentIds == null ||
            ParticipantPersistentIds.Count == 0;
    }

    [System.Serializable]
    public class CardData
    {
        public string Id;
        public string PersistentId;
        public int UsesLeft;
        public int CurrentHealth;
        public int MaximumHealth;
        public int CurrentNutrition;
        public int StoredCoins;
        public bool IsLocationRandomSpawn;
        public int Level = 1;
        public int Experience;
        public int CurrentEnergy = 4;
        public int MaxEnergy = 4;
        public bool IsDowned;

        public string OriginalId; // Stores "Villager" if current Id is "Warrior"
        public List<CardData> EquippedItems = new();

        public CardData() { }

        public CardData(CardInstance card)
        {
            Id = card.Definition.Id;
            PersistentId = card.PersistentId;
            UsesLeft = card.UsesLeft;
            CurrentHealth = card.CurrentHealth;
            MaximumHealth = card.Stats?.MaxHealth.Value ??
                card.CurrentHealth;
            CurrentNutrition = card.CurrentNutrition;
            Level = card.Level;
            Experience = card.Experience;
            CurrentEnergy = card.CurrentEnergy;
            MaxEnergy = card.MaxEnergy;
            IsDowned = card.IsDowned;
            IsLocationRandomSpawn =
                card.GetComponent<LocationRandomSpawnMarker>() != null;

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

        public void NormalizeProgression(int fallbackEnergy = 4)
        {
            Level = Mathf.Max(1, Level);
            Experience = Mathf.Max(0, Experience);
            MaxEnergy = MaxEnergy > 0
                ? MaxEnergy
                : Mathf.Max(1, fallbackEnergy);
            CurrentEnergy = Mathf.Clamp(CurrentEnergy, 0, MaxEnergy);
        }
    }

    [System.Serializable]
    public class CombatData
    {
        public int Version = 2;
        public string SessionId;
        public uint RandomState;
        public long CreatedSequence;
        public List<CardData> Attackers = new();
        public List<CardData> Defenders = new();
        public bool PlayerIsAttacker;
        public float[] RectPosition;
        public List<CombatantRuntimeData> RuntimeStates = new();
        public List<CombatCommand> QueuedCommands = new();
        public List<string> ResolvedDefeatIds = new();

        public CombatData() { }

        public CombatData(CombatTask task)
        {
            Version = 2;
            SessionId = task.SessionId;
            RandomState = task.RandomState;
            CreatedSequence = task.CreatedSequence;
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


            long fallbackSequence = 0;
            foreach (CardInstance card in task.Attackers.Concat(task.Defenders))
            {
                if (card?.Combatant == null)
                    continue;
                RuntimeStates.Add(new CombatantRuntimeData
                {
                    PersistentId = card.PersistentId,
                    ActionProgress = card.Combatant.ActionProgress,
                    JoinSequence = task.GetJoinSequence(card, fallbackSequence++),
                    RetreatProtectionRemaining =
                        card.Combatant.ReaggroProtectionRemaining,
                    SkillCooldowns = card.Combatant.SkillCooldowns
                        .Select(pair => new CombatSkillCooldownData
                        {
                            SkillId = pair.Key,
                            RemainingSeconds = pair.Value
                        })
                        .ToList()
                });
            }
            QueuedCommands.AddRange(task.QueuedCommands);
            ResolvedDefeatIds.AddRange(task.ResolvedDefeatIds);
        }

        public void NormalizeAndMigrate()
        {
            Attackers ??= new List<CardData>();
            Defenders ??= new List<CardData>();
            RuntimeStates ??= new List<CombatantRuntimeData>();
            QueuedCommands ??= new List<CombatCommand>();
            ResolvedDefeatIds ??= new List<string>();

            if (Version >= 2)
                return;

            Version = 2;
            SessionId = string.IsNullOrWhiteSpace(SessionId)
                ? System.Guid.NewGuid().ToString("N")
                : SessionId;
            int index = 0;
            foreach (CardData card in Attackers.Concat(Defenders))
            {
                RuntimeStates.Add(new CombatantRuntimeData
                {
                    PersistentId = card?.PersistentId,
                    ActionProgress = Mathf.Min(90f, index++ * 10f),
                    JoinSequence = index
                });
            }
        }
    }

    [System.Serializable]
    public sealed class CombatantRuntimeData
    {
        public string PersistentId;
        public float ActionProgress;
        public long JoinSequence;
        public float RetreatProtectionRemaining;
        public List<CombatSkillCooldownData> SkillCooldowns = new();
    }

    [System.Serializable]
    public sealed class CombatSkillCooldownData
    {
        public string SkillId;
        public float RemainingSeconds;
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
    public sealed class MarketStockData
    {
        [SerializeField] private string offerId;
        [SerializeField] private int day;
        [SerializeField] private int remaining;

        public string OfferId
        {
            get => offerId;
            set => offerId = value;
        }

        public int Day
        {
            get => day;
            set => day = value;
        }

        public int Remaining
        {
            get => remaining;
            set => remaining = value;
        }

        public MarketStockData() { }

        public MarketStockData(string offerId, int day, int remaining)
        {
            this.offerId = offerId;
            this.day = day;
            this.remaining = remaining;
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
