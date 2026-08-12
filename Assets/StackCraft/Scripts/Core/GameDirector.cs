using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CryingSnow.StackCraft
{
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        public event System.Action<SceneData, bool> OnSceneDataReady;
        public event System.Action<GameData> OnBeforeSave;
        public event System.Action<GameData> OnAfterSave;
        public event System.Action<CharacterProgressionNotification>
            OnProtagonistProgressed;

        [SerializeField, Tooltip("The name of the scene that serves as the game's main menu or entry point.")]
        private string titleScene = "Title";

        [SerializeField, Tooltip("The name of the default gameplay scene to load when starting a new game.")]
        private string defaultScene = "Main";

        [SerializeField, Tooltip("The reusable scene used for local location boards.")]
        private string locationScene = "Location";

        private const string DefaultProtagonistResourcePath =
            "Cards/Characters/Card_Villager";

        public Dictionary<string, GameData> SavedGames { get; private set; }
        public GameData GameData { get; private set; }

        [System.NonSerialized]
        private List<CardData> incomingTravelers = new List<CardData>();

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SavedGames = SaveSystem.LoadAllValidData<GameData>();
            WorldQuestRuntime.Ensure(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnApplicationQuit()
        {
            if (SceneManager.GetActiveScene().name != titleScene)
            {
                if (DayCycleManager.Instance.IsEndingCycle) return;

                SaveGameAtStableCombatBoundary();
            }
        }
        #endregion

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (incomingTravelers.Count > 0)
            {
                SpawnTravelers();
            }

            if (scene.name != titleScene)
            {
                GameData.CurrentScene = scene.name;
                bool wasLoaded = GameData.TryGetScene(out SceneData sceneData);
                OnSceneDataReady?.Invoke(sceneData, wasLoaded);
                WorldQuestRuntime.Instance?.Initialize(GameData);
                if (!string.IsNullOrWhiteSpace(GameData.ActiveLocationId))
                {
                    WorldQuestRuntime.Instance?.ReportLocationEntered(
                        GameData.ActiveLocationId);
                }
                WorldQuestRuntime.Instance?.HandleSceneLoaded();
            }
        }

        #region Core Game Flow
        /// <summary>
        /// Initializes a new game session. It automatically finds the next available save slot
        /// number, creates a new GameData object with the provided preferences, and starts 
        /// the travel sequence to load the default game scene.
        /// </summary>
        /// <param name="prefs">The gameplay settings for the new session.</param>
        public void NewGame(GameplayPrefs prefs)
        {
            HashSet<int> takenSlots = new HashSet<int>();

            foreach (var data in SavedGames.Values)
            {
                takenSlots.Add(data.SlotNumber);
            }

            int candidateSlot = 1;

            while (takenSlots.Contains(candidateSlot))
            {
                candidateSlot++;
            }

            GameData = new GameData(candidateSlot, prefs);
            GameData.EnsureEconomyState();
            EnsureProtagonistState(GameData);
            WorldQuestRuntime.Instance?.Initialize(GameData);
            StartCoroutine(TravelSequence(defaultScene, null));
        }

        /// <summary>
        /// Saves the current state of the active game session to the disk.
        /// </summary>
        /// <remarks>
        /// This method first invokes the OnBeforeSave event, allowing other systems (managers)
        /// to populate the GameData.
        /// </remarks>
        public void SaveGame()
        {
            if (GameData == null) return;
            if (CombatManager.Instance?.DeferSaveIfResolving() == true)
                return;
            CaptureActiveLocationParty();
            OnBeforeSave?.Invoke(GameData);
            GameData.LastSaved = System.DateTime.Now;
            string fileName = $"SaveSlot{GameData.SlotNumber:D3}";
            SaveSystem.SaveData<GameData>(GameData, fileName);
            SavedGames.TryAdd(fileName, GameData);
            OnAfterSave?.Invoke(GameData);
        }

        private void SaveGameAtStableCombatBoundary()
        {
            CombatManager.Instance?.FlushResolvingActionsForSave();
            SaveGame();
        }

        /// <summary>
        /// Loads a previously saved game session.
        /// </summary>
        /// <param name="gameData">The GameData object loaded from a save file.</param>
        public void LoadGame(GameData gameData)
        {
            if (gameData == null)
                return;

            gameData.EnsureBackpack();
            gameData.EnsureEconomyState();
            EnsureProtagonistState(gameData);
            this.GameData = gameData;
            WorldQuestRuntime.Instance?.Initialize(gameData);
            StartCoroutine(TravelSequence(gameData.CurrentScene, null));
        }

        public bool EnterLocation(string locationId, IEnumerable<CardData> partyMembers)
        {
            if (GameData == null || string.IsNullOrWhiteSpace(locationId))
                return false;

            bool enteringFromLocation = SceneManager.GetActiveScene().name == locationScene &&
                !string.IsNullOrWhiteSpace(GameData.ActiveLocationId);
            SaveGameAtStableCombatBoundary();
            if (enteringFromLocation)
                GameData.PushLocation(GameData.ActiveLocationId);
            else
                GameData.LocationHistory?.Clear();

            GameData.ActiveLocationId = locationId;
            if (!enteringFromLocation)
            {
                // 世界地图上的队伍始终作为一个整体移动。进入新的聚落时，
                // 任何旧的建筑驻留记录都必须失效，不能把成员留在别处。
                GameData.ClearAllBuildingPersonSlots();
                GameData.ActiveBuildingEntry = null;
                GameData.UpdatePartyMembers(partyMembers);
            }
            GameData.MarkLocationTransitionPending(
                enteringFromLocation
                    ? LocationTransitionReason.ChildLocationEntry
                    : LocationTransitionReason.WorldMapEntry);
            StartCoroutine(TravelSequence(locationScene, null));
            return true;
        }

        public bool EnterBuilding(BuildingEntryContext context)
        {
            if (GameData == null || context == null ||
                string.IsNullOrWhiteSpace(context.InteriorLocationId))
            {
                return false;
            }

            GameData.ActiveBuildingEntry = context;
            if (EnterLocation(context.InteriorLocationId, null))
                return true;

            GameData.ActiveBuildingEntry = null;
            return false;
        }

        public bool ReturnFromLocation(IEnumerable<CardData> partyMembers)
        {
            if (GameData == null)
                return false;

            if (partyMembers != null)
                GameData.MergePartyMemberStates(partyMembers);

            SaveGameAtStableCombatBoundary();
            string departingLocationId = GameData.ActiveLocationId;
            if (GameData.TryPopLocation(out string parentLocationId))
            {
                GameData.ActiveLocationId = parentLocationId;
                GameData.ActiveBuildingEntry = null;
                GameData.MarkLocationTransitionPending(
                    LocationTransitionReason.ReturnToParent);
                StartCoroutine(TravelSequence(locationScene, null));
                return true;
            }

            GameData.ClearBuildingSlotsForSettlement(departingLocationId);
            GameData.ActiveLocationId = null;
            GameData.ActiveBuildingEntry = null;
            StartCoroutine(TravelSequence(defaultScene, null));
            return true;
        }

        public bool ReturnToWorldMap(IEnumerable<CardData> partyMembers)
        {
            if (GameData == null)
                return false;

            if (partyMembers != null)
                GameData.MergePartyMemberStates(partyMembers);

            SaveGameAtStableCombatBoundary();
            string settlementId = GameData.ActiveLocationId;
            while (GameData.TryPopLocation(out string parentLocationId))
                settlementId = parentLocationId;
            GameData.ClearBuildingSlotsForSettlement(settlementId);
            GameData.ActiveBuildingEntry = null;
            GameData.LocationHistory?.Clear();
            GameData.ActiveLocationId = null;
            StartCoroutine(TravelSequence(defaultScene, null));
            return true;
        }

        /// <summary>
        /// Deletes a specified saved game session from both the in-memory list of saved games 
        /// and the physical save file on disk.
        /// </summary>
        /// <param name="gameData">The GameData object corresponding to the save file to be deleted.</param>
        public void DeleteGame(GameData gameData)
        {
            string fileName = $"SaveSlot{gameData.SlotNumber:D3}";
            SavedGames.Remove(fileName);
            SaveSystem.DeleteSave(fileName);
        }

        /// <summary>
        /// Saves the current game state and initiates the process of returning to the title scene.
        /// </summary>
        public void BackToTitle()
        {
            SaveGameAtStableCombatBoundary();
            StartCoroutine(TravelSequence(titleScene, null));
        }

        /// <summary>
        /// Handles the final 'game over' state.
        /// </summary>
        /// <remarks>
        /// This method deletes the current game save file and immediately transitions the player 
        /// back to the title scene.
        /// </remarks>
        public void GameOver()
        {
            DeleteGame(this.GameData);
            StartCoroutine(TravelSequence(titleScene, null));
        }

        public bool IsProtagonist(CardInstance card)
        {
            return ProtagonistRules.IsProtagonist(GameData, card);
        }

        public bool IsProtagonist(CardData cardData)
        {
            return ProtagonistRules.IsProtagonist(GameData, cardData);
        }

        public CardData GetProtagonistData()
        {
            return GameData?.GetProtagonistData();
        }

        public CardInstance FindActiveProtagonistCard()
        {
            return CardManager.Instance?.AllCards.FirstOrDefault(
                card => IsProtagonist(card));
        }

        public CharacterProgressionResult GrantProtagonistExperience(int amount)
        {
            CardData protagonistData = GetProtagonistData();
            if (protagonistData == null || amount <= 0)
                return new CharacterProgressionResult(0);

            int previousLevel = protagonistData.Level;
            CardInstance activeCard = FindActiveProtagonistCard();
            CharacterProgressionResult result;
            if (activeCard == null ||
                SceneManager.GetActiveScene().name != locationScene)
            {
                result =
                    CharacterProgressionService.GrantExperience(
                        protagonistData,
                        amount);
                activeCard?.RestoreSavedStats(protagonistData);
            }
            else
            {
                result = activeCard.GainExperience(amount);
                protagonistData.Level = activeCard.Level;
                protagonistData.Experience = activeCard.Experience;
                protagonistData.CurrentHealth = activeCard.CurrentHealth;
                protagonistData.MaximumHealth =
                    activeCard.Stats?.MaxHealth.Value ??
                    activeCard.CurrentHealth;
                protagonistData.CurrentEnergy = activeCard.CurrentEnergy;
                protagonistData.MaxEnergy = activeCard.MaxEnergy;
                protagonistData.IsDowned = activeCard.IsDowned;
            }

            string displayName =
                activeCard?.Definition?.DisplayName ?? "旅行者";
            string message = protagonistData.Level > previousLevel
                ? CharacterProgressionFeedback.BuildLevelUpMessage(
                    displayName,
                    previousLevel,
                    protagonistData.Level)
                : $"{displayName}获得了战斗经验。";
            OnProtagonistProgressed?.Invoke(
                new CharacterProgressionNotification(
                    amount,
                    previousLevel,
                    protagonistData.Level,
                    protagonistData.Experience,
                    message));
            if (protagonistData.Level != previousLevel)
            {
                WorldQuestRuntime.Instance?.ReportProtagonistLevelChanged(
                    protagonistData.Level);
            }
            return result;
        }

        public bool TryRescueDownedProtagonist()
        {
            bool rescued =
                ProtagonistRecoveryService.TryRescueWithFirstMedicine(
                    GameData);
            if (rescued)
                SaveGame();
            return rescued;
        }

        public bool RetreatDownedProtagonist()
        {
            ProtagonistRetreatResult result =
                ProtagonistRetreatService.Apply(
                    GameData,
                    ProtagonistRetreatService.DefaultCoinDefinitionId,
                    ProtagonistRetreatService.DefaultHoursLost,
                    ProtagonistRetreatService.DefaultMaximumCoinsLost);
            if (!result.Succeeded)
                return false;

            FindActiveProtagonistCard()?.Revive(
                restoredHealth: 1,
                restoredEnergy: 0);
            BackpackService.NotifyContentsChanged();
            return ReturnToWorldMap(null);
        }

        public bool ConfirmProtagonistDeath(string deathCause)
        {
            CardData protagonist = GetProtagonistData();
            if (protagonist?.IsDowned != true)
                return false;

            ProtagonistRunChronicle chronicle =
                ProtagonistChronicleService.Create(GameData, deathCause);
            string archiveName =
                ProtagonistChronicleService.GetArchiveFileName(chronicle);
            if (chronicle != null &&
                !string.IsNullOrWhiteSpace(archiveName))
            {
                SaveSystem.SaveData(chronicle, archiveName);
            }

            GameOver();
            return true;
        }

        public void SyncProtagonistState(CardInstance activeCard)
        {
            if (!IsProtagonist(activeCard))
                return;

            CardData data = GetProtagonistData();
            if (data == null)
                return;

            data.CurrentHealth = activeCard.CurrentHealth;
            data.MaximumHealth = activeCard.Stats?.MaxHealth.Value ??
                activeCard.CurrentHealth;
            data.Level = activeCard.Level;
            data.Experience = activeCard.Experience;
            data.CurrentEnergy = activeCard.CurrentEnergy;
            data.MaxEnergy = activeCard.MaxEnergy;
            data.IsDowned = activeCard.IsDowned;
        }
        #endregion

        #region Scene & Travel Management
        /// <summary>
        /// Starts a scene transition sequence, handling saving and transporting traveler cards.
        /// </summary>
        /// <remarks>
        /// It determines the next scene by finding the current scene in the targetScenes list and 
        /// moving to the next one cyclically. The transition involves a screen fade and scene load, 
        /// during which card data for all travelers is preserved.
        /// </remarks>
        /// <param name="targetScenes">A list defining the order of scenes in a travel cycle.</param>
        /// <param name="travelers">The list of CardInstances that should be carried over to the new scene.</param>
        public void InitiateTravel(List<string> targetScenes, List<CardInstance> travelers)
        {
            if (targetScenes == null || targetScenes.Count == 0)
            {
                Debug.LogError("Target scene list is empty.");
                return;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            int currentIndex = targetScenes.IndexOf(currentScene);

            if (currentIndex < 0)
            {
                Debug.LogWarning($"Current scene '{currentScene}' not found in travel list.");
                return;
            }

            SaveGameAtStableCombatBoundary();

            string targetScene = targetScenes[(currentIndex + 1) % targetScenes.Count];

            List<CardData> travelersData = new List<CardData>();
            if (travelers != null)
            {
                foreach (var card in travelers)
                {
                    travelersData.Add(new CardData(card));
                }
            }

            StartCoroutine(TravelSequence(targetScene, travelersData));
        }

        private IEnumerator TravelSequence(string sceneName, List<CardData> travelers)
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.SetExternalPause(true);

            yield return ScreenFader.Instance?.Fade(0f, 1f);

            if (travelers != null)
                incomingTravelers = new List<CardData>(travelers);

            yield return SceneManager.LoadSceneAsync(sceneName);

            if (TimeManager.Instance != null)
                TimeManager.Instance.SetExternalPause(false);

            yield return ScreenFader.Instance?.Fade(1f, 0f);
        }

        private void SpawnTravelers()
        {
            foreach (var data in incomingTravelers)
            {
                var randomPos = Random.insideUnitSphere.Flatten();
                CardManager.Instance.RestoreTraveler(data, randomPos);
            }

            incomingTravelers.Clear();
        }

        private void CaptureActiveLocationParty()
        {
            if (GameData == null ||
                SceneManager.GetActiveScene().name != locationScene ||
                CardManager.Instance == null)
            {
                return;
            }

            List<CardData> activeParty = CardManager.Instance.AllCards
                .Where(card =>
                    card != null &&
                    card.Definition != null &&
                    card.Definition.Category == CardCategory.Character &&
                    card.Definition.Faction == CardFaction.Player)
                .Select(card => new CardData(card))
                .ToList();
            if (activeParty.Count > 0)
                GameData.MergePartyMemberStates(activeParty);
        }

        private static CardData EnsureProtagonistState(GameData gameData)
        {
            if (gameData == null)
                return null;

            CardDefinition fallback = Resources.Load<CardDefinition>(
                DefaultProtagonistResourcePath);
            if (fallback == null)
            {
                Debug.LogError(
                    $"Missing protagonist definition at Resources/{DefaultProtagonistResourcePath}.");
                return null;
            }

            return gameData.EnsureProtagonist(
                fallback.Id,
                fallback.CreateCombatStats().MaxHealth.Value,
                fallbackEnergy: 4);
        }
        #endregion
    }
}
