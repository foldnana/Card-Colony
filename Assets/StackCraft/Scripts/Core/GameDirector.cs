using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CryingSnow.StackCraft
{
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        public event System.Action<SceneData, bool> OnSceneDataReady;
        public event System.Action<GameData> OnBeforeSave;

        [SerializeField, Tooltip("The name of the scene that serves as the game's main menu or entry point.")]
        private string titleScene = "Title";

        [SerializeField, Tooltip("The name of the default gameplay scene to load when starting a new game.")]
        private string defaultScene = "Main";

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

                SaveGame();
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
            OnBeforeSave?.Invoke(GameData);
            GameData.LastSaved = System.DateTime.Now;
            string fileName = $"SaveSlot{GameData.SlotNumber:D3}";
            SaveSystem.SaveData<GameData>(GameData, fileName);
            SavedGames.TryAdd(fileName, GameData);
        }

        /// <summary>
        /// Loads a previously saved game session.
        /// </summary>
        /// <param name="gameData">The GameData object loaded from a save file.</param>
        public void LoadGame(GameData gameData)
        {
            this.GameData = gameData;
            StartCoroutine(TravelSequence(gameData.CurrentScene, null));
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
            SaveGame();
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

            SaveGame();

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
        #endregion
    }
}
