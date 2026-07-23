using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class CardManager : MonoBehaviour
    {
        #region Singleton & Events
        public static CardManager Instance { get; private set; }

        public event System.Action<CardInstance> OnCardCreated;

        public event System.Action<CardInstance> OnCardKilled;
        public void NotifyCardKilled(CardInstance card) => OnCardKilled?.Invoke(card);

        public event System.Action<CardDefinition> OnCardEquipped;
        public void NotifyCardEquipped(CardDefinition card) => OnCardEquipped?.Invoke(card);

        public event System.Action<StatsSnapshot> OnStatsChanged;
        public void NotifyStatsChanged()
        {
            currentStats = GetStatsSnapshot();
            OnStatsChanged?.Invoke(currentStats);
        }
        #endregion

        #region Serialized Fields
        [Header("Defaults")]
        [SerializeField, Tooltip("The radius within which default cards will spawn randomly around the default spawn position.")]
        private float defaultSpawnRadius = 1f;

        [SerializeField, Tooltip("The center point for where the initial set of cards will be spawned when a new game starts.")]
        private Vector3 defaultSpawnPosition = Vector3.zero;

        [SerializeField, Tooltip("A list of CardDefinitions to be instantiated when a new game session is created.")]
        private List<CardDefinition> defaultSpawnCards;

        [Header("References")]
        [SerializeField, Tooltip("The prefab used for all Card Packs (e.g., Starter Pack, Booster Pack).")]
        private PackInstance packPrefab;

        [SerializeField, Tooltip("A list mapping CardCategory enums to their corresponding CardInstance prefabs for instantiation.")]
        private List<CategoryEntry> cardPrefabs;

        [SerializeField, Tooltip("Prefab used specifically for Mobs marked as Aggressive.")]
        private CardInstance aggressiveMobPrefab;

        [SerializeField, Tooltip("The base ScriptableObject used to dynamically create temporary Recipe Card Definitions at runtime.")]
        private CardDefinition recipeCardTemplate;

        [SerializeField, Tooltip("The ScriptableObject containing all the defined rules for which card categories can stack on top of others.")]
        private StackingRulesMatrix stackingMatrix;

        [SerializeField, Tooltip("Reference to the global CardSettings ScriptableObject for configuration values like radii and limits.")]
        private CardSettings cardSettings;
        #endregion

        private readonly List<CardStack> stacks = new();

        public IEnumerable<CardInstance> AllCards
        {
            get
            {
                IEnumerable<CardInstance> cards = stacks.SelectMany(s => s.Cards);

                if (CombatManager.Instance != null)
                {
                    IEnumerable<CardInstance> combatCards = CombatManager.Instance.ActiveCombats
                        .Where(c => c.IsOngoing)
                        .SelectMany(c => c.Attackers.Concat(c.Defenders));
                    cards = cards.Concat(combatCards);
                }

                if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive)
                    cards = cards.Concat(DialogueManager.Instance.Participants);

                return cards.Where(card => card != null).Distinct();
            }
        }

        private Dictionary<CardCategory, CardInstance> prefabLookup;
        private Dictionary<string, CardDefinition> definitionLookup;

        private List<CardInstance> highlightedCards = new();

        private StatsSnapshot currentStats;

        private readonly HashSet<CardDefinition> discoveredCards = new();

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePrefabLookup();
            BuildDefinitionDatabase();

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;

                RestoreDiscoveredCards();
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(defaultSpawnPosition, defaultSpawnRadius);
        }
#endif
        #endregion

        #region Initialization
        private void InitializePrefabLookup()
        {
            prefabLookup = new Dictionary<CardCategory, CardInstance>();

            foreach (var mapping in cardPrefabs)
            {
                if (mapping.prefab == null)
                {
                    Debug.LogWarning($"CardManager: Prefab for category '{mapping.category}' is missing!");
                    continue;
                }

                if (prefabLookup.ContainsKey(mapping.category))
                {
                    Debug.LogWarning($"CardManager: Duplicate entry for category '{mapping.category}' detected. Ignoring second entry.");
                }
                else
                {
                    prefabLookup.Add(mapping.category, mapping.prefab);
                }
            }
        }

        private void BuildDefinitionDatabase()
        {
            definitionLookup = new Dictionary<string, CardDefinition>();

            foreach (var def in Resources.LoadAll<CardDefinition>("Cards"))
            {
                if (!definitionLookup.ContainsKey(def.Id))
                {
                    definitionLookup.Add(def.Id, def);
                }
            }

            foreach (var packDef in Resources.LoadAll<PackDefinition>("Packs"))
            {
                if (!definitionLookup.ContainsKey(packDef.Id))
                {
                    definitionLookup.Add(packDef.Id, packDef);
                }
            }
        }

        /// <summary>
        /// Retrieves a <see cref="CardDefinition"/> based on its unique ID string.
        /// </summary>
        /// <remarks>
        /// This method handles lookup for both static definitions (loaded at startup) 
        /// and dynamic definitions (e.g., those created at runtime for Recipe Cards).
        /// </remarks>
        /// <param name="id">The unique ID string of the <see cref="CardDefinition"/>.</param>
        /// <returns>The found <see cref="CardDefinition"/>, or null if the ID is not recognized.</returns>
        public CardDefinition GetDefinitionById(string id)
        {
            // 1. Try standard static lookup first
            if (definitionLookup.TryGetValue(id, out var def))
                return def;

            // 2. Handle Dynamic Recipe Cards
            // Check if this ID looks like our formatted Recipe ID
            if (id.StartsWith("Recipe:"))
            {
                // Extract the original Recipe ID (e.g., "Recipe:12345abcde" -> "12345abcde")
                string recipeId = id.Substring("Recipe:".Length);

                // Find the static recipe asset
                var recipe = CraftingManager.Instance?.GetRecipeById(recipeId);

                if (recipe != null)
                {
                    return CreateRecipeCardDefinition(recipe);
                }
            }

            // 3. Fail gracefully
            Debug.LogError($"CardManager: Could not find CardDefinition with ID '{id}'");
            return null;
        }
        #endregion

        #region Save & Load
        private void HandleSceneDataReady(SceneData sceneData, bool wasLoaded)
        {
            if (wasLoaded)
            {
                foreach (var stackData in sceneData.SavedStacks)
                {
                    RestoreStack(stackData);
                }
            }
            else
            {
                WorldMapBootstrap worldMapBootstrap = GetComponent<WorldMapBootstrap>();
                if (worldMapBootstrap != null)
                    worldMapBootstrap.SpawnNewGame(this);
                else
                    SpawnDefaultCards();
            }
        }

        private void RestoreStack(StackData stackData)
        {
            if (stackData.Cards == null || stackData.Cards.Count == 0) return;

            CardStack mainStack = null;
            Vector3 stackPos = stackData.GetPosition();

            for (int i = 0; i < stackData.Cards.Count; i++)
            {
                var cardData = stackData.Cards[i];

                CardDefinition def = GetDefinitionById(cardData.Id);

                if (def == null) continue;

                if (def is PackDefinition packDef)
                {
                    var packInstance = CreatePackInstance(packDef, stackPos);
                    packInstance.RestoreSavedStats(cardData);
                    continue;
                }

                CardInstance newCard = CreateCardInstance(
                    def,
                    stackPos,
                    CardStack.RefuseAll,
                    notifyCreated: false);
                newCard.RestoreSavedStats(cardData);
                RestoreLocationRandomSpawnMarker(newCard, cardData);

                if (newCard.EquipperComponent != null && cardData.EquippedItems != null && cardData.EquippedItems.Count > 0)
                {
                    RestoreEquipmentForCard(newCard, cardData);
                }

                if (i == 0)
                {
                    // The first card becomes the "anchor" of our stack.
                    mainStack = newCard.Stack;

                    // Ensure the base is locked to the saved position immediately.
                    mainStack.SetTargetPosition(stackPos, instant: true);
                }
                else
                {
                    // Subsequent cards created their own temporary stacks on Init.
                    // We must "steal" them and add them to the main stack.
                    CardStack tempStack = newCard.Stack;

                    // 1. Remove from temp stack (this unregisters the temp stack if it becomes empty).
                    tempStack.RemoveCard(newCard);

                    // 2. Add to the main reconstructed stack.
                    mainStack.AddCard(newCard);
                }
            }

            if (mainStack != null)
            {
                // Immediately align all cards in the stack to their correct visual stacking offsets.
                mainStack.SetTargetPosition(stackPos, instant: true);

                // If this stack had an active crafting task when saved, restore and resume it.
                if (stackData.ActiveCraft != null && !string.IsNullOrEmpty(stackData.ActiveCraft.RecipeId))
                {
                    CraftingManager.Instance.RestoreCraftingTask(
                        mainStack,
                        stackData.ActiveCraft.RecipeId,
                        stackData.ActiveCraft.Progress
                    );
                }
            }
        }

        private void RestoreEquipmentForCard(
            CardInstance character,
            CardData data,
            bool notifyStats = true,
            bool registerWithManager = true)
        {
            // 1. If this character changed class (e.g. is now a Warrior), 
            // manually inject the "Villager" base state so the Equipper knows we aren't starting fresh.
            if (!string.IsNullOrEmpty(data.OriginalId))
            {
                var originalDef = GetDefinitionById(data.OriginalId);
                character.EquipperComponent.SetOriginalDefinition(originalDef);
            }

            // 2. Instantiate and Equip items
            foreach (var itemData in data.EquippedItems)
            {
                var itemDef = GetDefinitionById(itemData.Id);
                if (itemDef == null) continue;

                // Create the item "in the void" (Vector3.zero)
                // We temporarily create it on the board, but Equip() will immediately remove it.
                CardInstance itemCard = CreateCardInstance(
                    itemDef,
                    Vector3.zero,
                    CardStack.RefuseAll,
                    notifyCreated: false,
                    notifyStats: notifyStats,
                    registerWithManager: registerWithManager);

                // Restore stats (durability, etc)
                itemCard.RestoreSavedStats(itemData);

                // Perform the Equip
                // Because we set OriginalDefinition above, this won't accidentally double-transform the class.
                character.EquipperComponent.Equip(itemCard);
            }
        }

        /// <summary>
        /// Reconstructs a <see cref="CardInstance"/> from serialized <see cref="CardData"/>.
        /// </summary>
        /// <param name="data">The serialized data containing the card's state, equipment, and original ID.</param>
        /// <param name="position">The world-space position where the card should be instantiated.</param>
        /// <returns>A fully initialized <see cref="CardInstance"/>; returns <c>null</c> if the definition cannot be found.</returns>
        public CardInstance RestoreCardFromData(
            CardData data,
            Vector3 position,
            bool notifyStats = true)
        {
            return RestoreCardFromDataInternal(
                data,
                position,
                notifyStats,
                registerWithManager: true);
        }

        /// <summary>
        /// Reconstructs a card solely for a secondary visual surface such as the backpack.
        /// Its stack is never registered with the world board, so creation cannot affect
        /// world overlap resolution or location save data.
        /// </summary>
        public CardInstance RestoreUnmanagedCardFromData(CardData data, Vector3 position)
        {
            return RestoreCardFromDataInternal(
                data,
                position,
                notifyStats: false,
                registerWithManager: false);
        }

        private CardInstance RestoreCardFromDataInternal(
            CardData data,
            Vector3 position,
            bool notifyStats,
            bool registerWithManager)
        {
            string spawnId = !string.IsNullOrEmpty(data.OriginalId) ? data.OriginalId : data.Id;
            CardDefinition baseDef = GetDefinitionById(spawnId);

            if (baseDef == null) return null;
            CardInstance card = CreateCardInstance(
                baseDef,
                position,
                CardStack.RefuseAll,
                notifyCreated: false,
                notifyStats: notifyStats,
                registerWithManager: registerWithManager);
            card.RestoreSavedStats(data);
            RestoreLocationRandomSpawnMarker(card, data);
            RestoreEquipmentForCard(
                card,
                data,
                notifyStats,
                registerWithManager);

            return card;
        }

        private static void RestoreLocationRandomSpawnMarker(
            CardInstance card,
            CardData data)
        {
            if (card == null || data == null || !data.IsLocationRandomSpawn ||
                card.GetComponent<LocationRandomSpawnMarker>() != null)
            {
                return;
            }

            card.gameObject.AddComponent<LocationRandomSpawnMarker>();
        }

        /// <summary>
        /// Creates and initializes a single card instance using saved <see cref="CardData"/>, 
        /// restoring its basic stats, equipped items, and any class transformations it had.
        /// </summary>
        /// <param name="cardData">The saved data object containing the card's definition ID, stats, and equipment.</param>
        /// <param name="position">The world position at which to spawn the card.</param>
        public void RestoreTraveler(CardData cardData, Vector3 position)
        {
            CardDefinition def = GetDefinitionById(cardData.Id);
            if (def == null) return;

            // 1. Create the base card instance
            CardInstance newCard = CreateCardInstance(
                def,
                position,
                CardStack.RefuseAll,
                notifyCreated: false);
            if (newCard == null) return;

            // 2. Restore basic stats (Health, Uses, Nutrition)
            newCard.RestoreSavedStats(cardData);

            // 3. Restore Equipment & Class Changes
            if (newCard.EquipperComponent != null &&
                cardData.EquippedItems != null &&
                cardData.EquippedItems.Count > 0)
            {
                RestoreEquipmentForCard(newCard, cardData);
            }
        }

        private void RestoreDiscoveredCards()
        {
            if (GameDirector.Instance.GameData != null)
            {
                foreach (string cardId in GameDirector.Instance.GameData.DiscoveredCards)
                {
                    var def = GetDefinitionById(cardId);
                    if (def != null && !discoveredCards.Contains(def))
                    {
                        discoveredCards.Add(def);
                    }
                }
            }
        }

        private void HandleBeforeSave(GameData gameData)
        {
            DialogueManager.Instance?.EndDialogue();

            if (gameData.TryGetScene(out var sceneData))
            {
                sceneData.SaveStacks(stacks);
            }

            foreach (var cardDef in discoveredCards)
            {
                if (cardDef != null)
                {
                    gameData.DiscoveredCards.Add(cardDef.Id);
                }
            }
        }
        #endregion

        #region Card Factory
        private void SpawnDefaultCards()
        {
            foreach (var card in defaultSpawnCards)
            {
                Vector3 randomPos = Random.insideUnitSphere * defaultSpawnRadius;
                Vector3 spawnPos = defaultSpawnPosition + randomPos.Flatten();

                if (card is PackDefinition pack)
                {
                    CreatePackInstance(pack, spawnPos);
                }
                else
                {
                    CreateCardInstance(card, spawnPos, CardStack.RefuseAll);
                }
            }
        }

        /// <summary>
        /// Instantiates a new <see cref="CardInstance"/> GameObject in the world, initializes it with the given definition,
        /// and registers its resulting stack.
        /// </summary>
        /// <remarks>
        /// This factory method handles logic for discovering new cards, selecting the correct
        /// prefab (including overrides for aggressive mobs), and attaching necessary logic
        /// components (like <see cref="EnclosureLogic"/> or <see cref="ChestLogic"/>) based on the card's definition.
        /// If Friendly Mode is enabled, aggressive mobs will not be spawned.
        /// </remarks>
        /// <param name="definition">The data definition for the card to be created.</param>
        /// <param name="position">The initial world position for the card.</param>
        /// <param name="stackToIgnore">A specific stack to ignore when the new card attempts to merge into nearby stacks.</param>
        /// <returns>The newly created <see cref="CardInstance"/> object, or null if spawning failed (e.g., in Friendly Mode).</returns>
        public CardInstance CreateCardInstance(
            CardDefinition definition,
            Vector3 position,
            CardStack stackToIgnore = null,
            bool notifyCreated = true,
            bool notifyStats = true,
            bool registerWithManager = true)
        {
            MarkCardAsDiscovered(definition);

            if (GameDirector.Instance.GameData.GameplayPrefs.IsFriendlyMode && definition.IsAggressive)
            {
                return null;
            }

            CardInstance prefabToSpawn = null;

            // 1. Check for specific override first
            if (definition.Category == CardCategory.Mob && definition.IsAggressive && aggressiveMobPrefab != null)
            {
                prefabToSpawn = aggressiveMobPrefab;
            }
            // 2. Fallback to standard category lookup
            else if (prefabLookup.TryGetValue(definition.Category, out var genericPrefab))
            {
                prefabToSpawn = genericPrefab;
            }

            if (prefabToSpawn == null)
            {
                Debug.LogError($"CardManager: No prefab found for category '{definition.Category}'. Cannot spawn card '{definition.DisplayName}'.");
                return null;
            }

            CardInstance newCard = Instantiate(prefabToSpawn, position, Quaternion.identity);
            newCard.Initialize(
                definition,
                cardSettings,
                stackToIgnore,
                registerWithManager);

            if (definition is EnclosureDefinition enclosureDef)
            {
                var enclosureLogic = newCard.gameObject.AddComponent<EnclosureLogic>();
                enclosureLogic.Initialize(enclosureDef.Capacity);
            }
            else if (definition is ChestDefinition chestDef)
            {
                var chestLogic = newCard.gameObject.AddComponent<ChestLogic>();
                chestLogic.Initialize(newCard);
            }

            if (notifyStats)
                NotifyStatsChanged();
            if (notifyCreated)
                OnCardCreated?.Invoke(newCard);
            return newCard;
        }

        /// <summary>
        /// Instantiates the generic <see cref="PackInstance"/> prefab and initializes it using a specific <see cref="PackDefinition"/>.
        /// </summary>
        /// <param name="definition">The data definition for the card pack to be created.</param>
        /// <param name="position">The initial world position for the pack.</param>
        /// <returns>The newly created <see cref="PackInstance"/> object.</returns>
        public PackInstance CreatePackInstance(PackDefinition definition, Vector3 position)
        {
            PackInstance newPack = Instantiate(packPrefab, position, Quaternion.identity);
            newPack.Initialize(definition, cardSettings);
            return newPack;
        }

        /// <summary>
        /// Creates a new, temporary <see cref="CardDefinition"/> instance for a specific recipe.
        /// </summary>
        /// <remarks>
        /// This dynamic definition uses the assigned recipe card template, sets a unique
        /// ID based on the recipe's ID, and populates the display name and description
        /// with the recipe's result and required ingredients.
        /// </remarks>
        /// <param name="recipe">The <see cref="RecipeDefinition"/> containing the information for the card.</param>
        /// <returns>A dynamically created <see cref="CardDefinition"/> representing the recipe.</returns>
        public CardDefinition CreateRecipeCardDefinition(RecipeDefinition recipe)
        {
            if (recipeCardTemplate == null)
            {
                Debug.LogError("CardManager: 'Recipe Card Template' is not assigned in the Inspector!");
                return null;
            }

            CardDefinition dynamicDef = Instantiate(recipeCardTemplate);

            dynamicDef.SetId($"Recipe:{recipe.Id}");

            if (recipe.ResultingCard != null)
            {
                dynamicDef.SetDisplayName($"配方：{recipe.ResultingCard.DisplayName}");
            }
            else
            {
                dynamicDef.SetDisplayName($"配方：？？？");
            }

            string description = CraftingManager.Instance?.GetFormattedIngredients(recipe);
            dynamicDef.SetDescription(description);
            return dynamicDef;
        }

        /// <summary>
        /// Creates a dynamic <see cref="CardDefinition"/> for the given recipe and spawns a <see cref="CardInstance"/>
        /// representing that recipe onto the board near the crafting stack.
        /// </summary>
        /// <param name="recipe">The <see cref="RecipeDefinition"/> used to generate the card's data.</param>
        /// <param name="craftingStack">The <see cref="CardStack"/> that is currently crafting, used for positioning the new card.</param>
        public void SpawnRecipeCard(RecipeDefinition recipe, CardStack craftingStack)
        {
            var dynamicDef = CreateRecipeCardDefinition(recipe);
            var spawnPos = craftingStack.TargetPosition.Flatten();
            CreateCardInstance(dynamicDef, spawnPos, craftingStack);
        }

        /// <summary>
        /// Re-introduces a previously equipped card back onto the board as a new, standalone stack.
        /// </summary>
        /// <remarks>
        /// The card is placed at its current world position, a new <see cref="CardStack"/> is created and registered
        /// for it, and the CardManager's overlap resolution is triggered to ensure proper placement.
        /// This method is used, for example, when an item is unequipped.
        /// </remarks>
        /// <param name="cardToDrop">The equipment <see cref="CardInstance"/> being returned to the game board.</param>
        public void ReturnCardToBoard(CardInstance cardToDrop)
        {
            var dropPosition = cardToDrop.transform.position;
            var newStack = new CardStack(cardToDrop, dropPosition.Flatten());
            RegisterStack(newStack);
            ResolveOverlaps();
        }
        #endregion

        #region Stacking
        /// <summary>
        /// Adds a new <see cref="CardStack"/> to the manager's internal tracking list, allowing it to participate
        /// in physics resolution and global operations.
        /// </summary>
        /// <param name="stack">The <see cref="CardStack"/> to register.</param>
        public void RegisterStack(CardStack stack)
        {
            if (stack != null && !stacks.Contains(stack))
                stacks.Add(stack);
        }

        /// <summary>
        /// Removes a <see cref="CardStack"/> from the manager's tracking list.
        /// Used when a stack is fully destroyed or absorbed into another stack.
        /// </summary>
        /// <param name="stack">The <see cref="CardStack"/> to unregister.</param>
        public void UnregisterStack(CardStack stack)
        {
            if (stack != null) stacks.Remove(stack);
        }

        /// <summary>
        /// Determines if one card definition (top) can physically stack on top of another (bottom)
        /// based on the global <see cref="StackingRulesMatrix"/> configuration.
        /// </summary>
        /// <param name="bottom">The definition of the card on the bottom of the stack.</param>
        /// <param name="top">The definition of the card to be placed on top.</param>
        /// <returns>True if the stacking rule allows the placement; otherwise, false.</returns>
        public bool CanStack(CardDefinition bottom, CardDefinition top)
        {
            var rule = stackingMatrix.GetRule(bottom.Category, top.Category);

            switch (rule)
            {
                case StackingRule.None:
                    return false;
                case StackingRule.CategoryWide:
                    return true;
                case StackingRule.SameDefinition:
                    return bottom == top;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Visually highlights the bottom card of all eligible CardStacks that the liftedCard
        /// is physically allowed to merge with.
        /// </summary>
        /// <param name="liftedCard">The card currently being dragged or moved.</param>
        public void HighlightStackableStacks(CardInstance liftedCard)
        {
            foreach (var stack in stacks)
            {
                if (stack.BottomCard == null) continue;

                bool canStack = CanStack(liftedCard.Definition, stack.BottomCard.Definition);
                bool sameCard = liftedCard == stack.BottomCard;
                bool sameStack = liftedCard.Stack == stack;

                if (canStack && !sameCard && !sameStack && !stack.IsCrafting)
                {
                    stack.BottomCard.SetHighlighted(true);
                    highlightedCards.Add(stack.BottomCard);
                }
            }
        }

        /// <summary>
        /// Clears the visual highlight state from all cards that were previously marked
        /// as stackable targets.
        /// </summary>
        public void TurnOffHighlightedCards()
        {
            highlightedCards.ForEach(card => card.SetHighlighted(false));
            highlightedCards.Clear();
        }

        /// <summary>
        /// Triggers the <see cref="CardPhysicsSolver"/> to detect and resolve physical overlaps 
        /// between all known card stacks and active combat areas on the board.
        /// </summary>
        public void ResolveOverlaps()
        {
            List<CombatRect> combatRects = CombatManager.Instance != null
                ? CombatManager.Instance.ActiveCombatRects.Where(rect => rect != null).ToList()
                : new List<CombatRect>();
            CombatRect dialogueRect = DialogueManager.Instance?.InteractionRect;
            if (dialogueRect != null && !combatRects.Contains(dialogueRect))
                combatRects.Add(dialogueRect);

            CardPhysicsSolver.ResolveOverlaps(
                stacks,
                combatRects,
                cardSettings.MaxIterations
            );
        }

        /// <summary>
        /// Triggers the <see cref="CardPhysicsSolver"/> to detect and resolve physical overlaps 
        /// between all known card stacks and active combat areas on the board.
        /// </summary>
        /// <param name="combatRect">A reference to an active combat area.</param>
        /// <param name="stackToIgnore">A specific stack to ignore during the overlap resolution process.</param>
        public void ResolveOverlaps(CombatRect combatRect, CardStack stackToIgnore = null)
        {
            List<CombatRect> combatRects = CombatManager.Instance != null
                ? CombatManager.Instance.ActiveCombatRects.Where(rect => rect != null).ToList()
                : new List<CombatRect>();
            CombatRect dialogueRect = DialogueManager.Instance?.InteractionRect;
            if (dialogueRect != null && !combatRects.Contains(dialogueRect))
                combatRects.Add(dialogueRect);
            if (combatRect != null && !combatRects.Contains(combatRect))
                combatRects.Add(combatRect);

            IList<CardStack> stacksToResolve = stackToIgnore == null
                ? stacks
                : stacks.Where(stack => stack != stackToIgnore).ToList();

            CardPhysicsSolver.ResolveOverlaps(
                stacksToResolve,
                combatRects,
                cardSettings.MaxIterations
            );
        }

        /// <summary>
        /// Iterates through all unlocked CardStacks and moves any that are found to be 
        /// outside the valid board boundaries back into the playable area.
        /// </summary>
        /// <remarks>
        /// If any stack is moved, a full overlap resolution is performed afterwards.
        /// </remarks>
        public void EnforceBoardLimits()
        {
            if (Board.Instance == null) return;

            bool anyMoved = false;

            foreach (var stack in stacks)
            {
                if (stack.IsLocked) continue;

                Vector3 currentPos = stack.TargetPosition;
                Vector3 validPos = Board.Instance.EnforcePlacementRules(currentPos, stack);

                if (Vector3.SqrMagnitude(currentPos - validPos) > 0.001f)
                {
                    stack.SetTargetPosition(validPos, instant: false);
                    anyMoved = true;
                }
            }

            if (anyMoved)
            {
                ResolveOverlaps();
            }
        }
        #endregion

        #region Discovery
        /// <summary>
        /// Checks if the provided <see cref="CardDefinition"/> has been officially added to the game's set of discovered cards.
        /// </summary>
        /// <param name="card">The card definition to check.</param>
        /// <returns>True if the card is non-null and is in the discovered set; otherwise, false.</returns>
        public bool IsCardDiscovered(CardDefinition card)
        {
            return card != null && discoveredCards.Contains(card);
        }

        /// <summary>
        /// Adds a card definition to the permanent global set of discovered cards.
        /// </summary>
        /// <remarks>
        /// Recipe cards are explicitly ignored by this discovery process as they are usually temporary.
        /// </remarks>
        /// <param name="card">The definition of the card that was just encountered or created.</param>
        public void MarkCardAsDiscovered(CardDefinition card)
        {
            if (card != null && !discoveredCards.Contains(card))
            {
                if (card.Category == CardCategory.Recipe) return;
                discoveredCards.Add(card);
            }
        }
        #endregion

        #region  Game Cycles
        /// <summary>
        /// Executes the global feeding phase, where all Character cards attempt to consume 
        /// available Consumable cards to satisfy their hunger.
        /// </summary>
        /// <returns>An IEnumerator to run the feeding process as a coroutine, managing animations and delays.</returns>
        public IEnumerator FeedCharacters()
        {
            var allCards = AllCards.ToList();

            var characterCards = GetSurvivalCharacters(allCards).ToList();

            var consumableCards = allCards
                .Where(card => card.Definition.Category == CardCategory.Consumable &&
                       card.CurrentNutrition > 0
                )
                .ToList();

            foreach (var character in characterCards)
            {
                if (character == null) continue;

                if (Camera.main.transform.parent.TryGetComponent<CameraController>(out var cam))
                {
                    yield return cam.MoveTo(character.transform.position);
                }

                int hungerLeft = cardSettings.HungerPerCharacter;

                while (hungerLeft > 0)
                {
                    if (consumableCards.Count == 0)
                    {
                        Vector3 position = character.transform.position;
                        character.Kill();
                        break;
                    }

                    var nearestConsumable = consumableCards
                        .OrderBy(c => Vector3.Distance(character.transform.position, c.transform.position))
                        .First();

                    consumableCards.Remove(nearestConsumable);

                    yield return nearestConsumable.Consume(
                        character,
                        hungerLeft,
                        nutrition =>
                        {
                            hungerLeft -= nutrition;

                            float healFraction = (float)nutrition / cardSettings.HungerPerCharacter;
                            float healPercent = 0.5f * healFraction;
                            int maxHealth = character.Stats.MaxHealth.Value;
                            int healAmount = Mathf.RoundToInt(maxHealth * healPercent);
                            int maxPossibleHeal = maxHealth - character.CurrentHealth;
                            healAmount = Mathf.Min(healAmount, maxPossibleHeal);

                            character.Heal(healAmount);
                        }
                    );

                    if (nearestConsumable != null && nearestConsumable.CurrentNutrition > 0)
                    {
                        consumableCards.Add(nearestConsumable);
                    }

                    NotifyStatsChanged();
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        #endregion

        #region Stats Snapshot
        /// <summary>
        /// Calculates and returns a comprehensive snapshot of the current global game statistics.
        /// </summary>
        /// <remarks>
        /// This includes totals for nutrition, currency, cards owned, character count, 
        /// and the calculation of the dynamic card limit and any excess cards.
        /// </remarks>
        /// <returns>A <see cref="StatsSnapshot"/> struct containing the current state of tracked resources and limits.</returns>
        public StatsSnapshot GetStatsSnapshot()
        {
            var allCards = AllCards.ToList();

            int cardsOwned = CalculateCardsOwned(allCards);
            int totalBoost = CalculateTotalBoost(allCards);
            int cardLimit = cardSettings.BaseCardLimit + totalBoost;

            return new StatsSnapshot
            {
                TotalNutrition = CalculateTotalNutrition(allCards) +
                    CalculateBackpackNutrition(),
                NutritionNeed = CalculateNutritionNeed(allCards),
                Currency = CalculateCurrency(allCards) + CalculateBackpackCurrency(),
                CardsOwned = cardsOwned,
                TotalBoost = totalBoost,
                CardLimit = cardLimit,
                ExcessCards = cardsOwned - cardLimit,
                TotalCharacters = CalculateCharacter(allCards)
            };
        }

        private int CalculateTotalNutrition(List<CardInstance> allCards)
        {
            return allCards
                .Where(card => card.Definition.Category == CardCategory.Consumable)
                .Sum(card => card.CurrentNutrition);
        }

        private int CalculateNutritionNeed(List<CardInstance> allCards)
        {
            return GetSurvivalCharacters(allCards).Count() * cardSettings.HungerPerCharacter;
        }

        private int CalculateCurrency(List<CardInstance> allCards)
        {
            int total = 0;

            foreach (var card in allCards)
            {
                if (card.Definition.Category == CardCategory.Currency)
                    total++;

                if (card.TryGetComponent<ChestLogic>(out var chest))
                    total += chest.StoredCoins;
            }

            return total;
        }

        private int CalculateCardsOwned(List<CardInstance> allCards)
        {
            return GetCardsCountingTowardLimit(allCards).Count();
        }

        private int CalculateTotalBoost(List<CardInstance> allCards)
        {
            int totalBoost = 0;

            foreach (var card in allCards)
            {
                if (card.Definition is LimitBoosterDefinition boosterDef)
                {
                    totalBoost += boosterDef.BoostAmount;
                }
            }

            return totalBoost;
        }

        private int CalculateCharacter(List<CardInstance> allCards)
        {
            return GetSurvivalCharacters(allCards).Count();
        }

        public int CountOwnedCard(CardDefinition definition)
        {
            if (definition == null)
                return 0;

            int worldCount = AllCards.Count(card => card.BaseDefinition == definition);
            int backpackCount = GetBackpackEntries().Count(entry =>
                entry.Card.Id == definition.Id || entry.Card.OriginalId == definition.Id);
            return worldCount + backpackCount;
        }

        private int CalculateBackpackNutrition()
        {
            return GetBackpackEntries()
                .Where(entry => IsStoredCategory(entry, CardCategory.Consumable))
                .Sum(entry => Mathf.Max(0, entry.Card.CurrentNutrition));
        }

        private int CalculateBackpackCurrency()
        {
            int looseCoins = GetBackpackEntries()
                .Count(entry => IsStoredCategory(entry, CardCategory.Currency));
            int storedCoins = GetBackpackEntries()
                .Sum(entry => Mathf.Max(0, entry.Card.StoredCoins));
            return looseCoins + storedCoins;
        }

        private IEnumerable<BackpackEntryData> GetBackpackEntries()
        {
            return GameDirector.Instance?.GameData?.EnsureBackpack()?.Entries?
                .Where(entry => entry?.Card != null) ??
                Enumerable.Empty<BackpackEntryData>();
        }

        private bool IsStoredCategory(
            BackpackEntryData entry,
            CardCategory category)
        {
            return entry?.Card != null && definitionLookup != null &&
                definitionLookup.TryGetValue(entry.Card.Id, out CardDefinition definition) &&
                definition.Category == category;
        }

        public static IEnumerable<CardInstance> GetSurvivalCharacters(
            IEnumerable<CardInstance> cards)
        {
            return cards?.Where(card =>
                    card?.Definition != null &&
                    card.Definition.Category == CardCategory.Character &&
                    !card.Definition.IsLocationStatic) ??
                Enumerable.Empty<CardInstance>();
        }

        public static IEnumerable<CardInstance> GetCardsCountingTowardLimit(
            IEnumerable<CardInstance> cards)
        {
            return cards?.Where(card =>
                    card?.Definition != null &&
                    card is not PackInstance &&
                    card.Definition.Category != CardCategory.Currency &&
                    !card.Definition.IsLocationStatic) ??
                Enumerable.Empty<CardInstance>();
        }
        #endregion
    }

    public struct StatsSnapshot
    {
        public int TotalNutrition { get; set; }
        public int NutritionNeed { get; set; }
        public int Currency { get; set; }
        public int CardsOwned { get; set; }
        public int TotalBoost { get; set; }
        public int CardLimit { get; set; }
        public int ExcessCards { get; set; }
        public int TotalCharacters { get; set; }
    }

    [System.Serializable]
    public struct CategoryEntry
    {
        public CardCategory category;
        public CardInstance prefab;
    }
}
