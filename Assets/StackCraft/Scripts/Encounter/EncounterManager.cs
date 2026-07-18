using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class EncounterManager : MonoBehaviour
    {
        public static EncounterManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField, Tooltip("All possible EncounterDefinitions that this manager should evaluate for spawning events.")]
        private List<EncounterDefinition> allEncounters;

        [SerializeField, Tooltip("The minimum distance (in world units) from the edge of the board that a new card can spawn.")]
        private float spawnEdgePadding = 2f;

        private HashSet<string> completedEncounters = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;
            }
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady -= HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;
            }
        }

        private void HandleSceneDataReady(SceneData sceneData, bool wasLoaded)
        {
            if (wasLoaded)
            {
                this.completedEncounters = sceneData.CompletedEncounters;
            }
        }

        private void HandleBeforeSave(GameData gameData)
        {
            if (gameData.TryGetScene(out var sceneData))
            {
                sceneData.CompletedEncounters = this.completedEncounters;
            }
        }

        /// <summary>
        /// Evaluates all available encounter definitions to determine which event should trigger for the given day.
        /// </summary>
        /// <param name="day">The current in-game day used to filter time-specific candidates.</param>
        /// <returns>
        /// The highest priority <see cref="EncounterDefinition"/> that meets all requirements(day, card count, friendly mode, and completion status); 
        /// returns null if no valid candidates are found.
        /// </returns>
        /// <remarks>
        /// Selection is determined by a hierarchical sorting system:
        /// <list type="number">
        /// <item><description>User-defined <see cref="EncounterDefinition.Priority"/> (Descending).</description></item>
        /// <item><description>Intrinsic type priority (Specific Day > Recurring > Range > Minimum Day).</description></item>
        /// </list>
        /// </remarks>
        public EncounterDefinition GetBestEncounter(int day)
        {
            int cardCount = CardManager.Instance.AllCards.Count();
            bool isFriendlyMode = GameDirector.Instance.GameData.GameplayPrefs.IsFriendlyMode;

            // 1. Find all valid candidates
            var candidates = allEncounters
                .Where(e => e.IsValidForDay(day, completedEncounters, cardCount, isFriendlyMode))
                .ToList();

            if (candidates.Count == 0) return null;

            // 2. Sort by Priority (Descending) -> Then specific rules
            // Priority Rule: User defined Priority > Specific Day > Cycle > Others
            var sortedCandidates = candidates
                .OrderByDescending(e => e.Priority) // 1. User Priority
                .ThenBy(e => GetTypePriority(e.Type)) // 2. Intrinsic Logic Priority
                .ToList();

            // Return the Winner
            return sortedCandidates.First();
        }

        /// <summary>
        /// A coroutine that handles the visual and functional sequence of an encounter event.
        /// </summary>
        /// <param name="encounter">The <see cref="EncounterDefinition"/> to process.</param>
        /// <returns>An IEnumerator for sequential execution.</returns>
        public IEnumerator ExecuteEncounter(EncounterDefinition encounter)
        {
            if (encounter == null) yield break;

            if (encounter.OneTimeOnly)
            {
                completedEncounters.Add(encounter.Id);
            }

            // 1. Show Notification
            if (!string.IsNullOrEmpty(encounter.NotificationMessage))
            {
                InfoPanel.Instance?.RequestInfoDisplay(
                    this,
                    InfoPriority.Modal,
                    ("事件", encounter.NotificationMessage)
                );
                yield return new WaitForSecondsRealtime(2f);
            }

            // 2. Spawn Cards
            for (int i = 0; i < encounter.Count; i++)
            {
                Vector3 spawnPos = GetRandomBoardPosition();

                // Spawn Logic
                var card = CardManager.Instance.CreateCardInstance(
                    encounter.CardToSpawn,
                    spawnPos,
                    CardStack.RefuseAll
                );

                // Focus Camera
                if (Camera.main.transform.parent.TryGetComponent<CameraController>(out var cam))
                {
                    yield return cam.MoveTo(spawnPos);
                }

                card.PlayPuffParticle();
                yield return new WaitForSecondsRealtime(0.5f);
            }

            InfoPanel.Instance?.ClearInfoRequest(this);
        }

        private Vector3 GetRandomBoardPosition()
        {
            if (Board.Instance == null) return Vector3.zero;

            Bounds b = Board.Instance.WorldBounds;

            // Random position within bounds minus padding
            float x = Random.Range(b.min.x + spawnEdgePadding, b.max.x - spawnEdgePadding);
            float z = Random.Range(b.min.z + spawnEdgePadding, b.max.z - spawnEdgePadding);

            // Ensure we aren't in the restricted top margin
            float restrictedZ = b.max.z - Board.Instance.TopMargin;
            z = Mathf.Min(z, restrictedZ - 1f);

            return new Vector3(x, 0, z);
        }

        private int GetTypePriority(EncounterType type)
        {
            switch (type)
            {
                case EncounterType.SpecificDay: return 0;
                case EncounterType.Recurring: return 1;
                case EncounterType.Range: return 2;
                case EncounterType.MinimumDay: return 3;
                default: return 4;
            }
        }
    }
}
