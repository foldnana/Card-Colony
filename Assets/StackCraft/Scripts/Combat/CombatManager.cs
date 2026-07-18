using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        #region Serialized Fields
        [Header("RPS Settings")]
        [SerializeField, Tooltip("Damage multiplier for an advantageous type (e.g., 1.5 for 50% bonus).")]
        private float advantageMultiplier = 1.5f;

        [SerializeField, Tooltip("Damage multiplier for a disadvantageous type (e.g., 0.75 for 25% penalty).")]
        private float disadvantageMultiplier = 0.75f;

        [Header("UI References")]
        [SerializeField, Tooltip("The prefab used to create the visual area for active combat (a rectangle that holds units).")]
        private CombatRect combatRectPrefab;

        [SerializeField, Tooltip("The prefab used to instantiate floating UI elements to show damage numbers and hit types.")]
        private HitUI hitUIPrefab;

        [SerializeField, Tooltip("The projectile prefab used for attacks of CombatType.Ranged (e.g., Arrows).")]
        private CombatProjectile arrowProjectile;

        [SerializeField, Tooltip("The projectile prefab used for attacks of CombatType.Magic (e.g., Spells).")]
        private CombatProjectile magicProjectile;
        #endregion

        #region Public Properties
        public float AdvantageMultiplier => advantageMultiplier;
        public float DisadvantageMultiplier => disadvantageMultiplier;

        public IEnumerable<CombatTask> ActiveCombats => _activeCombats;
        public IEnumerable<CombatRect> ActiveCombatRects => _activeCombats
            .Where(task => task.Rect != null)
            .Select(task => task.Rect);
        #endregion

        private readonly List<CombatTask> _activeCombats = new();

        #region Unity Lifecycle & Event Handlers
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

        private void Update()
        {
            float delta = Time.deltaTime;
            for (int i = _activeCombats.Count - 1; i >= 0; i--)
            {
                _activeCombats[i].Update(delta);
                if (!_activeCombats[i].IsOngoing)
                    _activeCombats.RemoveAt(i);
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
        #endregion

        #region Save & Load
        private void HandleSceneDataReady(SceneData sceneData, bool wasLoaded)
        {
            if (wasLoaded)
            {
                RestoreCombats(sceneData);
            }
        }

        private void HandleBeforeSave(GameData gameData)
        {
            if (gameData.TryGetScene(out var sceneData))
            {
                sceneData.SaveCombats(_activeCombats);
            }
        }

        private void RestoreCombats(SceneData sceneData)
        {
            foreach (var task in _activeCombats.ToList())
            {
                task.CleanUpForMerge();
            }
            _activeCombats.Clear();

            if (sceneData.SavedCombats == null) return;

            foreach (var combatData in sceneData.SavedCombats)
            {
                List<CardInstance> attackers = RestoreCardList(combatData.Attackers);
                List<CardInstance> defenders = RestoreCardList(combatData.Defenders);

                if (attackers.Count > 0 && defenders.Count > 0)
                {
                    var task = CreateCombatTaskInternal(attackers, defenders, combatData.PlayerIsAttacker);

                    if (combatData.RectPosition != null && combatData.RectPosition.Length == 3 && task.Rect != null)
                    {
                        task.Rect.transform.position = new Vector3(
                            combatData.RectPosition[0],
                            combatData.RectPosition[1],
                            combatData.RectPosition[2]
                        );

                        task.Rect.UpdateLayout();

                        CardManager.Instance.ResolveOverlaps(task.Rect);
                    }
                }
            }
        }

        private List<CardInstance> RestoreCardList(List<CardData> dataList)
        {
            var list = new List<CardInstance>();
            if (dataList == null) return list;

            foreach (var data in dataList)
            {
                CardInstance card = CardManager.Instance.RestoreCardFromData(data, Vector3.zero);

                if (card != null)
                {
                    list.Add(card);
                }
            }
            return list;
        }
        #endregion

        #region Combat Initiation & Merging
        /// <summary>
        /// The main entry point for initiating a new combat encounter between two groups of units.
        /// </summary>
        /// <remarks>
        /// This method first checks for any active combats that physically overlap with the new combatants. 
        /// If overlaps are found, it triggers a merge operation; otherwise, it creates an isolated, standard combat task.
        /// </remarks>
        /// <param name="attackers">The list of CardInstances initiating the attack.</param>
        /// <param name="defenders">The list of CardInstances being attacked.</param>
        /// <param name="playerIsAttacker">True if the Player is the primary attacker; otherwise, false.</param>
        /// <returns>The newly created or merged <see cref="CombatTask"/>, or null if creation failed.</returns>
        public CombatTask StartCombat(List<CardInstance> attackers, List<CardInstance> defenders, bool playerIsAttacker)
        {
            // 1. Find any existing combat tasks that overlap with the new one's initiation area.
            var tasksToMerge = FindOverlappingTasks(attackers, defenders);

            // 2. If overlaps are found, initiate a merge.
            if (tasksToMerge.Any())
            {
                return MergeCombats(attackers, defenders, tasksToMerge);
            }
            // 3. Otherwise, create a standard, isolated combat.
            else
            {
                return CreateCombatTaskInternal(attackers, defenders, playerIsAttacker);
            }
        }

        /// <summary>
        /// Checks if the provided source combat task physically overlaps with any other active combat tasks.
        /// If an overlap is detected, it triggers a merge operation, consolidating all involved units 
        /// into a single, larger combat zone.
        /// </summary>
        /// <remarks>
        /// This ensures that combat areas that touch or cross boundaries are unified, preventing multiple 
        /// small, isolated fights when a single large battle should be occurring.
        /// </remarks>
        /// <param name="sourceTask">
        /// The <see cref="CombatTask"/> that was recently moved or created,
        /// serving as the basis for the overlap check.
        /// </param>
        public void CheckAndMergeCombats(CombatTask sourceTask)
        {
            if (sourceTask?.Rect == null) return;

            var tasksToMergeWith = new List<CombatTask>();
            Bounds sourceBounds = GetWorldBounds(sourceTask.Rect);

            // Find other active combats that now overlap with the source combat
            foreach (var otherTask in _activeCombats)
            {
                if (otherTask == sourceTask || otherTask.Rect == null) continue;

                Bounds otherBounds = GetWorldBounds(otherTask.Rect);
                if (sourceBounds.Intersects(otherBounds))
                {
                    tasksToMergeWith.Add(otherTask);
                }
            }

            if (tasksToMergeWith.Any())
            {
                // We must include the sourceTask itself in the list of tasks to be cleaned up.
                var allTasksToCleanUp = new List<CombatTask>(tasksToMergeWith);
                allTasksToCleanUp.Add(sourceTask);

                // The cards from our sourceTask act as the "initial" combatants.
                MergeCombats(sourceTask.Attackers, sourceTask.Defenders, allTasksToCleanUp);
            }
        }

        private CombatTask CreateCombatTaskInternal(List<CardInstance> attackers, List<CardInstance> defenders, bool playerIsAttacker)
        {
            // Do not create combat if one side is empty.
            if (attackers.Count == 0 || defenders.Count == 0) return null;

            var rect = Instantiate(combatRectPrefab, WorldCanvas.Instance?.transform);
            rect.Initialize(attackers, defenders);
            var task = new CombatTask(attackers, defenders, playerIsAttacker, rect);

            // Make every card aware that it is now in this specific combat.
            attackers.ForEach(a => a.Combatant.EnterCombat(task));
            defenders.ForEach(d => d.Combatant.EnterCombat(task));

            _activeCombats.Add(task);

            // Ensure no world stacks are overlapping the new combat rect.
            CardManager.Instance.ResolveOverlaps(rect);
            return task;
        }

        private CombatTask MergeCombats(List<CardInstance> initialAttackers, List<CardInstance> initialDefenders, List<CombatTask> tasksToMerge)
        {
            var allPlayerUnits = new List<CardInstance>();
            var allMobUnits = new List<CardInstance>();

            var allInitialCombatants = initialAttackers.Concat(initialDefenders);
            var allExistingCombatants = tasksToMerge.SelectMany(t => t.Attackers.Concat(t.Defenders));

            // Consolidate all units from all involved combats into two faction-based lists.
            foreach (var card in allInitialCombatants.Concat(allExistingCombatants))
            {
                // Avoid adding duplicates if a card somehow existed in multiple lists.
                if (allPlayerUnits.Contains(card) || allMobUnits.Contains(card)) continue;

                if (card.Definition.Faction == CardFaction.Player)
                {
                    allPlayerUnits.Add(card);
                }
                else if (card.Definition.Faction == CardFaction.Mob)
                {
                    allMobUnits.Add(card);
                }
            }

            // Clean up the old combats that are being merged.
            foreach (var task in tasksToMerge)
            {
                task.CleanUpForMerge();
                _activeCombats.Remove(task);
            }

            // In merged combats, Player faction is always considered the attacker for simplicity.
            return CreateCombatTaskInternal(allPlayerUnits, allMobUnits, true);
        }

        /// <summary>
        /// Finds all active CombatTasks whose rectangular bounds physically intersect with the
        /// calculated potential bounds of a combat that is about to be started.
        /// </summary>
        /// <remarks>
        /// This method is crucial for determining if a new combat should be started, or if it should
        /// be merged into one or more existing combat zones. It uses precise AABB (Axis-Aligned Bounding Box) 
        /// intersection checks in world space.
        /// </remarks>
        /// <param name="newAttackers">The cards that would form the attacking side of the potential new combat.</param>
        /// <param name="newDefenders">The cards that would form the defending side of the potential new combat.</param>
        /// <returns>A List of existing CombatTasks whose Rects overlap with the new combat's potential area.</returns>
        private List<CombatTask> FindOverlappingTasks(List<CardInstance> newAttackers, List<CardInstance> newDefenders)
        {
            var overlappingTasks = new HashSet<CombatTask>();
            var allNewCombatants = newAttackers.Concat(newDefenders).ToList();

            if (!allNewCombatants.Any())
            {
                return new List<CombatTask>();
            }

            // 1. Calculate the bounds of the combat that is ABOUT to be created.
            Bounds potentialBounds = CalculatePotentialBounds(allNewCombatants);

            // 2. Iterate through existing combats and check for intersection.
            foreach (var existingTask in _activeCombats)
            {
                Bounds existingBounds = GetWorldBounds(existingTask.Rect);

                // 3. Use Bounds.Intersects() for a precise AABB check.
                if (potentialBounds.Intersects(existingBounds))
                {
                    overlappingTasks.Add(existingTask);
                }
            }

            return overlappingTasks.ToList();
        }
        #endregion

        #region Utility & External Queries
        /// <summary>
        /// Finds the active <see cref="CombatTask"/> at a given world position by checking its <see cref="CombatRect"/>.
        /// </summary>
        /// <param name="worldPosition">The position to check.</param>
        /// <returns>The <see cref="CombatTask"/> if found, otherwise null.</returns>
        public CombatTask GetCombatTaskAtPosition(Vector3 worldPosition)
        {
            foreach (var task in _activeCombats)
            {
                if (task.Rect.IsPositionInside(worldPosition))
                {
                    return task;
                }
            }
            return null;
        }

        /// <summary>
        /// Instantiates a floating UI element at a specific world position to display the outcome of a combat hit.
        /// </summary>
        /// <remarks>
        /// This creates the visual indicator for damage, miss, critical hit, and type advantage/disadvantage, 
        /// and initializes it with the provided <see cref="HitResult"/> data.
        /// </remarks>
        /// <param name="position">The world position (typically where the target card is located) to spawn the UI.</param>
        /// <param name="hitResult">The result data (damage, hit type, advantage) used to configure the UI.</param>
        public void SpawnHitUI(Vector3 position, HitResult hitResult)
        {
            var hitUI = Instantiate(hitUIPrefab, position, Quaternion.Euler(90, 0, 0), WorldCanvas.Instance?.transform);
            hitUI.Initialize(hitResult);
        }

        /// <summary>
        /// Spawns and fires a visual projectile from a starting point to an end point based on the combat type.
        /// </summary>
        /// <remarks>
        /// Currently handles Ranged and Magic projectile types. The projectile is
        /// instantiated, set to fly towards the target, and automatically destroyed upon impact.
        /// </remarks>
        /// <param name="type">The <see cref="CombatType"/> (Ranged or Magic) which determines the projectile sprite/model.</param>
        /// <param name="start">The world position where the projectile begins its flight.</param>
        /// <param name="end">The world position where the projectile targets its destination.</param>
        /// <returns>The DOTween Tween object controlling the projectile's movement animation.</returns>
        public Tween SpawnProjectile(CombatType type, Vector3 start, Vector3 end)
        {
            CombatProjectile projectilePrefab = null;

            switch (type)
            {
                case CombatType.Ranged:
                    projectilePrefab = arrowProjectile;
                    break;

                case CombatType.Magic:
                    projectilePrefab = magicProjectile;
                    break;
            }

            var projectileObj = Instantiate(
                projectilePrefab,
                start,
                Quaternion.identity,
                WorldCanvas.Instance?.transform
            );

            return projectileObj.Fire(start, end);
        }
        #endregion

        #region Physics Helpers
        /// <summary>
        /// Calculates the world-space Axis-Aligned Bounding Box (AABB) for an existing <see cref="CombatRect"/> GameObject.
        /// </summary>
        /// <remarks>
        /// It uses the CombatRect's position as the center and its scaled size as the dimensions,
        /// primarily focusing on the horizontal (X/Z) plane for overlap checks.
        /// </remarks>
        /// <param name="combatRect">The active CombatRect instance whose bounds are needed.</param>
        /// <returns>A Bounds struct representing the world-space boundaries of the combat area.</returns>
        private Bounds GetWorldBounds(CombatRect combatRect)
        {
            var rectTransform = combatRect.Rect;
            Vector3 worldCenter = rectTransform.position;

            // Use sizeDelta as it's what we set in code, scaled by the canvas.
            Vector3 worldSize = Vector3.Scale(rectTransform.sizeDelta, rectTransform.lossyScale);

            // We only care about X and Z axes for overlap.
            var boundsSize = new Vector3(worldSize.x, 2f, worldSize.y);

            return new Bounds(worldCenter, boundsSize);
        }

        /// <summary>
        /// Calculates the world-space Axis-Aligned Bounding Box (AABB) that a new combat area
        /// would occupy if it were created with the given list of combatants.
        /// </summary>
        /// <remarks>
        /// This calculation is performed without instantiating the actual <see cref="CombatRect"/> GameObject, 
        /// allowing for pre-checking for overlaps before combat creation or merging.
        /// </remarks>
        /// <param name="combatants">The combined list of attacker and defender CardInstances.</param>
        /// <returns>A Bounds struct representing the calculated potential world-space boundaries.</returns>
        private Bounds CalculatePotentialBounds(List<CardInstance> combatants)
        {
            Vector3 center = combatants
                .Select(c => c.transform.position)
                .Aggregate(Vector3.zero, (acc, v) => acc + v) / combatants.Count();

            int attackerCount = combatants.Count(c => c.Definition.Faction == CardFaction.Player);
            int defenderCount = combatants.Count - attackerCount;
            var firstCard = combatants[0];

            Vector2 rectSize = CombatRect.CalculateRequiredSize(
                attackerCount, defenderCount, firstCard.Size, CombatRect.Margin
            );

            var size = new Vector3(rectSize.x, 1f, rectSize.y);
            return new Bounds(center, size);
        }
        #endregion
    }
}
