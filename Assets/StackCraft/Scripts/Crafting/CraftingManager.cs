using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class CraftingManager : MonoBehaviour
    {
        public static CraftingManager Instance { get; private set; }

        public event System.Action<string> OnRecipeDiscovered;

        public event System.Action<CardDefinition> OnCraftingFinished;
        public void NotifyCraftingFinished(CardDefinition definition) => OnCraftingFinished?.Invoke(definition);

        public event System.Action<CardDefinition> OnExplorationFinished;
        public void NotifyExplorationFinished(CardDefinition definition) => OnExplorationFinished?.Invoke(definition);

        [Header("UI References")]
        [SerializeField, Tooltip("UI prefab displayed above a card stack to show crafting progress.")]
        private ProgressUI progressUIPrefab;

        public List<RecipeDefinition> AllRecipes { get; private set; } = new();
        public HashSet<string> DiscoveredRecipes { get; private set; } = new();

        private readonly List<CraftingTask> activeCraftingTasks = new();
        private readonly Dictionary<CraftingTask, ProgressUI> activeCraftingUIs = new();

        #region Unity Lifecycle
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            AllRecipes = Resources.LoadAll<RecipeDefinition>("Recipes").ToList();

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;

                var gameData = GameDirector.Instance.GameData;
                if (gameData != null)
                {
                    DiscoveredRecipes.UnionWith(gameData.DiscoveredRecipes);
                }
            }
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;
            }
        }

        void Update()
        {
            for (int i = activeCraftingTasks.Count - 1; i >= 0; i--)
            {
                var task = activeCraftingTasks[i];

                if (!task.IsCanceled) // Skip ticking canceled tasks
                {
                    task.UpdateProgress(Time.deltaTime);

                    if (activeCraftingUIs.TryGetValue(task, out ProgressUI ui))
                    {
                        ui.UpdateUI(task);
                    }
                }

                if (task.IsCanceled || task.IsComplete) // Remove on cancel or complete
                {
                    if (task.IsComplete)
                    {
                        PerformCraftingAction(task);
                    }

                    // Clean up the UI (if any still exists)
                    if (activeCraftingUIs.ContainsKey(task))
                    {
                        Destroy(activeCraftingUIs[task].gameObject);
                        activeCraftingUIs.Remove(task);
                    }

                    activeCraftingTasks.RemoveAt(i);
                }
            }
        }
        #endregion

        #region Save & Load
        private void HandleBeforeSave(GameData gameData)
        {
            gameData.DiscoveredRecipes.UnionWith(this.DiscoveredRecipes);
        }

        /// <summary>
        /// Re-initializes a crafting task from saved data, used when loading a game session. 
        /// It recreates the task and its associated UI, then sets the progress to the saved value.
        /// </summary>
        /// <param name="stack">The <see cref="CardStack"/> that was performing the craft.</param>
        /// <param name="recipeId">The unique string ID used to look up the <see cref="RecipeDefinition"/>.</param>
        /// <param name="progress">The normalized or raw time value representing how much of the craft was finished.</param>
        public void RestoreCraftingTask(CardStack stack, string recipeId, float progress)
        {
            RecipeDefinition recipe = GetRecipeById(recipeId);
            if (recipe == null)
            {
                Debug.LogWarning($"Could not find recipe with ID {recipeId} to restore.");
                return;
            }

            StartCraftingTask(stack, recipe);

            var task = GetCraftingTask(stack);
            if (task != null)
            {
                task.SetProgress(progress);

                if (activeCraftingUIs.TryGetValue(task, out ProgressUI ui))
                {
                    ui.UpdateUI(task);
                }
            }
        }
        #endregion

        #region Recipe Matching
        /// <summary>
        /// Evaluates a <see cref="CardStack"/> to identify any valid recipes it matches.
        /// If multiple recipes are valid, it performs a weighted random selection based on their 
        /// <see cref="RecipeDefinition.RandomWeight"/> and initiates a <see cref="CraftingTask"/>.
        /// </summary>
        /// <param name="stack">The stack of cards to evaluate for recipe ingredients.</param>
        /// <remarks>
        /// This method handles logic for:
        /// <list type="bullet">
        /// <item><description>Filtering <see cref="AllRecipes"/> against the current stack composition.</description></item>
        /// <item><description>Calculating total weights for probabilistic outcomes.</description></item>
        /// <item><description>Handling fallback selection if weights are zero.</description></item>
        /// </list>
        /// </remarks>
        public void CheckForRecipe(CardStack stack)
        {
            if (stack == null || stack.Cards.Count == 0) return;

            // 1. Find ALL recipes that match the stack.
            List<RecipeDefinition> matchingRecipes = AllRecipes
                .Where(recipe => DoesStackMatchRecipe(stack, recipe))
                .ToList();

            // 2. If no recipes matched, we're done.
            if (matchingRecipes.Count == 0)
            {
                return;
            }

            // 3. Calculate the total weight of all possible recipes.
            float totalWeight = matchingRecipes.Sum(recipe => recipe.RandomWeight);

            // 4. If total weight is zero (e.g., all matches have 0 weight),
            //    we can't do a weighted random, so just pick one at random to avoid errors.
            if (totalWeight <= 0)
            {
                // Fallback to simple random selection
                int randomIndex = Random.Range(0, matchingRecipes.Count);
                StartCraftingTask(stack, matchingRecipes[randomIndex]);
                return;
            }

            // 5. Pick a random number between 0 and the total weight.
            float randomRoll = Random.Range(0f, totalWeight);

            // 6. Iterate until our "roll" is "used up".
            RecipeDefinition chosenRecipe = null;
            foreach (var recipe in matchingRecipes)
            {
                // Subtract this recipe's weight from the roll.
                randomRoll -= recipe.RandomWeight;

                // If the roll is now 0 or less, this is the one we've landed on.
                if (randomRoll <= 0f)
                {
                    chosenRecipe = recipe;
                    break;
                }
            }

            // (Safety check in case of floating point issues, defaulting to the last item)
            if (chosenRecipe == null)
            {
                chosenRecipe = matchingRecipes.Last();
            }

            // 7. Start the crafting task with the weighted-randomly chosen recipe.
            StartCraftingTask(stack, chosenRecipe);
        }

        private bool DoesStackMatchRecipe(CardStack stack, RecipeDefinition recipe)
        {
            // Group the cards in the stack by their base definition and count them.
            var stackComposition = stack.Cards
                .GroupBy(c => c.BaseDefinition)
                .ToDictionary(g => g.Key, g => g.Count());

            var recipeIngredients = recipe.RequiredIngredients;

            // 1. Verify the stack contains all necessary ingredients with the correct counts.
            foreach (var ingredient in recipeIngredients)
            {
                // Check if missing
                if (!stackComposition.TryGetValue(ingredient.card, out int countInStack))
                {
                    return false;
                }

                // If it is a Workstation recipe (AllowExcessIngredients), we only care about "At Least" the amount.
                // If it is a Standard recipe, we enforce strict counts for non-Resources (e.g. Tree, Rock).
                if (recipe.AllowExcessIngredients || ingredient.card.Category == CardCategory.Resource)
                {
                    if (countInStack < ingredient.count) return false;
                }
                else
                {
                    // Strict match for standard recipes
                    if (countInStack != ingredient.count) return false;
                }
            }

            // 2. Verify the stack does NOT contain any extra, non-recipe cards.

            // If AllowExcessIngredients is true, we SKIP this check. 
            // This allows the stack to contain [Sawmill, Wood x10, Plank x3] and still match the [Sawmill, Wood x2] recipe.
            if (!recipe.AllowExcessIngredients)
            {
                var recipeIngredientSet = new HashSet<CardDefinition>(recipeIngredients.Select(i => i.card));

                if (stackComposition.Keys.Any(cardDef => !recipeIngredientSet.Contains(cardDef)))
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Task Management
        private void StartCraftingTask(CardStack stack, RecipeDefinition recipe)
        {
            // Create and add a new crafting task to the list
            var newTask = new CraftingTask(recipe, stack);
            activeCraftingTasks.Add(newTask);
            stack.SetCraftingState(true);

            // Instantiate and track the UI for the new task
            ProgressUI newUI = Instantiate(
                progressUIPrefab,
                stack.TargetPosition + progressUIPrefab.DisplayOffset,
                Quaternion.identity
            );

            newUI.transform.SetParent(WorldCanvas.Instance?.transform);
            newUI.transform.localRotation = Quaternion.identity;

            activeCraftingUIs.Add(newTask, newUI);

            // Debug.Log($"Started crafting task for {recipe.ResultingCard.DisplayName}.");

            if (!DiscoveredRecipes.Contains(recipe.Id))
            {
                DiscoveredRecipes.Add(recipe.Id);
                OnRecipeDiscovered?.Invoke(recipe.Id);
            }
        }

        public void PauseCraftingTask(CardStack stack)
        {
            CraftingTask task = GetCraftingTask(stack);
            task?.Pause();
        }

        public void ResumeCraftingTask(CardStack stack)
        {
            CraftingTask task = GetCraftingTask(stack);
            task?.Resume();
        }

        public void StopCraftingTask(CardStack stack)
        {
            CraftingTask task = GetCraftingTask(stack);

            if (task != null)
            {
                task.Cancel();
                stack.SetCraftingState(false);

                // Clean up the UI for the stopped task
                if (activeCraftingUIs.ContainsKey(task))
                {
                    Destroy(activeCraftingUIs[task].gameObject);
                    activeCraftingUIs.Remove(task);
                }
            }
        }

        private void PerformCraftingAction(CraftingTask task)
        {
            var recipe = task.Recipe;
            var stack = task.TargetStack;

            recipe.Execute(stack);

            stack.SetCraftingState(false);
            CardManager.Instance?.NotifyStatsChanged();

            // 1. Stop if we created a Living Being (prevents infinite breeding)
            bool resultIsLiving = recipe.ResultingCard != null &&
                (recipe.ResultingCard.Category == CardCategory.Character ||
                 recipe.ResultingCard.Category == CardCategory.Mob);

            if (resultIsLiving) return;

            // 2. Determine if we should Auto-Repeat
            bool shouldRepeat = false;

            if (recipe.IsContinuous)
            {
                // Explicitly set to continuous (e.g. Logging Camp)
                shouldRepeat = true;
            }
            else if (recipe.HasConsumableIngredients() && stack.Cards.Count > 0)
            {
                // Not continuous, BUT we consumed something (Sawmill + 10 Wood).
                // If there are cards left, we should check if we can craft again.
                shouldRepeat = true;
            }

            // 3. Execute Repeat
            if (shouldRepeat)
            {
                CheckForRecipe(stack);
            }
        }
        #endregion

        #region Validation
        /// <summary>
        /// Determines if an incoming card can be added to a stack that is already crafting.
        /// </summary>
        /// <param name="targetStack">The stack currently processing a craft.</param>
        /// <param name="incomingCard">The definition of the card attempting to join the stack.</param>
        /// <returns>
        /// True if the recipe allows excess ingredients (e.g., a Workstation) and the card is a valid ingredient; 
        /// otherwise, false to block interaction.
        /// </returns>
        public bool CanJoinActiveCraft(CardStack targetStack, CardDefinition incomingCard)
        {
            // 1. Get the task running on this stack
            var task = GetCraftingTask(targetStack);
            if (task == null) return false;

            // 2. If the recipe allows excess (Workstation), check if the card is a valid ingredient
            if (task.Recipe.AllowExcessIngredients)
            {
                // Check if the incoming card is part of the recipe requirements
                bool isIngredient = task.Recipe.RequiredIngredients.Any(i => i.card == incomingCard);

                // Optional: Allow the "Result" card to stack 
                // (e.g. stacking Planks on top of the Sawmill output while it works)
                // bool isOutput = task.Recipe.ResultingCard == incomingCard;

                return isIngredient /*|| isOutput*/;
            }

            // 3. If strict recipe (not a workstation), we generally block interaction
            return false;
        }

        /// <summary>
        /// Re-evaluates an active crafting task after a card has been removed from its stack.
        /// </summary>
        /// <param name="stack">The stack whose composition has changed.</param>
        /// <remarks>
        /// If the remaining cards still satisfy the recipe requirements (common in "Workstation" recipes), 
        /// the task resumes. If the stack is no longer valid, the crafting task is stopped.
        /// </remarks>
        public void ValidateAndResumeTask(CardStack stack)
        {
            var task = GetCraftingTask(stack);
            if (task == null) return;

            // Checks if the stack (minus the card we just took away) still satisfies the recipe.
            if (DoesStackMatchRecipe(stack, task.Recipe))
            {
                // The remaining stack is still valid (e.g., Sawmill + 2 Wood).
                // Unpause the task!
                task.Resume();
            }
            else
            {
                // The remaining stack is broken (e.g., Sawmill + 1 Wood).
                // Stop the task.
                StopCraftingTask(stack);
            }
        }
        #endregion

        #region Recipe Discovery
        public bool IsRecipeDiscovered(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                // This isn't a valid recipe id, so it can't be 'undiscovered'.
                // Returning 'true' ensures it gets filtered out by the '!IsRecipeDiscovered' check.
                return true;
            }

            // Check if the list contains the ID.
            return DiscoveredRecipes.Contains(recipeId);
        }

        public void MarkRecipeAsDiscovered(RecipeDefinition recipe)
        {
            if (!string.IsNullOrEmpty(recipe.Id) && !DiscoveredRecipes.Contains(recipe.Id))
            {
                DiscoveredRecipes.Add(recipe.Id);
                OnRecipeDiscovered?.Invoke(recipe.Id);
            }
        }
        #endregion

        #region  Helper Methods
        public RecipeDefinition GetRecipeById(string recipeId)
        {
            return AllRecipes.FirstOrDefault(r => r.Id == recipeId);
        }

        public CraftingTask GetCraftingTask(CardStack stack)
        {
            return activeCraftingTasks.FirstOrDefault(task => task.TargetStack == stack);
        }

        /// <summary>
        /// Returns the ingredient list in readable string format.
        /// </summary>
        public string GetFormattedIngredients(RecipeDefinition recipe)
        {
            if (!AllRecipes.Contains(recipe))
            {
                return string.Empty;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < recipe.RequiredIngredients.Count; i++)
            {
                var ing = recipe.RequiredIngredients[i];
                if (ing.card == null) continue;

                sb.Append($"{ing.card.DisplayName} x{ing.count}");
                if (i < recipe.RequiredIngredients.Count - 1)
                    sb.Append(", ");
            }

            if (sb.Length > 0) sb.Append(".");

            return sb.ToString();
        }
        #endregion 
    }
}
