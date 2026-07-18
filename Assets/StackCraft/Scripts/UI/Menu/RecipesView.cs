using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public class RecipesView : MenuView
    {
        // Map: Recipe ID -> UI Button
        private readonly Dictionary<string, TextButton> recipeButtons = new();

        // Map: Recipe ID -> Recipe Definition (fast lookup)
        private readonly Dictionary<string, RecipeDefinition> recipeDefs = new();

        // Map: Category -> Header Button
        private readonly Dictionary<RecipeCategory, TextButton> categoryHeaderButtons = new();

        // Map: Category -> Is Expanded?
        private readonly Dictionary<RecipeCategory, bool> categoryToggleState = new();

        private void Start()
        {
            if (CraftingManager.Instance != null)
                CraftingManager.Instance.OnRecipeDiscovered += HandleRecipeDiscovered;

            PopulateView();
        }

        private void OnDestroy()
        {
            if (CraftingManager.Instance != null)
                CraftingManager.Instance.OnRecipeDiscovered -= HandleRecipeDiscovered;
        }

        private void PopulateView()
        {
            recipeButtons.Clear();
            recipeDefs.Clear();
            categoryHeaderButtons.Clear();
            categoryToggleState.Clear();

            // 1. Sort recipes by Category, then by Name
            var sortedRecipes = CraftingManager.Instance.AllRecipes
                .OrderBy(r => r.Category)
                .ThenBy(r => r.DisplayName)
                .ToList();

            // 2. Group them to create headers
            var groups = sortedRecipes.GroupBy(r => r.Category);

            foreach (var group in groups)
            {
                RecipeCategory category = group.Key;

                // Default state: Expanded
                categoryToggleState[category] = true;

                // Create the Header Button
                // We initially hide it. It only appears if we have discovered recipes in it.
                TextButton headerBtn = CreateItemButton(
                    $"{ChineseLocalization.RecipeCategoryName(category)} {SYMBOL_EXPANDED}",
                    null, // No hover info for headers
                    35f
                );

                headerBtn.SetOnClick(() => ToggleCategory(category));
                headerBtn.SetColor(headerColor);
                headerBtn.gameObject.SetActive(false); // Hidden by default

                categoryHeaderButtons.Add(category, headerBtn);

                // 3. Create buttons for all recipes in this group
                foreach (var recipe in group)
                {
                    recipeDefs[recipe.Id] = recipe;

                    TextButton recipeBtn = CreateRecipeButton(recipe);
                    if (recipeBtn != null)
                    {
                        recipeButtons.Add(recipe.Id, recipeBtn);

                        // Set initial visibility based on discovery status
                        bool isDiscovered = CraftingManager.Instance.IsRecipeDiscovered(recipe.Id);

                        // Logic: Visible if Discovered AND Category Expanded. 
                        // Since we just started, expanded is true. So just check discovered.
                        recipeBtn.gameObject.SetActive(isDiscovered);

                        // If at least one is discovered, show the header
                        if (isDiscovered)
                        {
                            headerBtn.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        private TextButton CreateRecipeButton(RecipeDefinition recipe)
        {
            if (recipe.ResultingCard == null) return null;

            string resultName = $"{SYMBOL_BULLET} {recipe.ResultingCard.DisplayName}";
            return CreateItemButton(resultName, recipe, 30f);
        }

        private void HandleRecipeDiscovered(string recipeId)
        {
            if (recipeButtons.TryGetValue(recipeId, out var recipeBtn))
            {
                // 1. Ensure the button is technically capable of being shown
                // We don't just SetActive(true) because the group might be collapsed.
                if (recipeDefs.TryGetValue(recipeId, out var recipe))
                {
                    // 2. Ensure the category header is visible (now that we have a recipe)
                    if (categoryHeaderButtons.TryGetValue(recipe.Category, out var headerBtn))
                    {
                        headerBtn.gameObject.SetActive(true);
                    }

                    // 3. Set recipe visibility based on group toggle state
                    bool isExpanded = categoryToggleState[recipe.Category];
                    recipeBtn.gameObject.SetActive(isExpanded);
                }
            }
        }

        private void ToggleCategory(RecipeCategory category)
        {
            // Flip State
            bool newState = !categoryToggleState[category];
            categoryToggleState[category] = newState;

            // Update Header Text
            if (categoryHeaderButtons.TryGetValue(category, out var headerBtn))
            {
                headerBtn.SetText($"{ChineseLocalization.RecipeCategoryName(category)} {(newState ? SYMBOL_EXPANDED : SYMBOL_COLLAPSED)}");
            }

            // Update Visibility of all recipes in this category
            foreach (var kvp in recipeDefs)
            {
                string id = kvp.Key;
                RecipeDefinition recipe = kvp.Value;

                if (recipe.Category == category)
                {
                    // Only manipulate buttons for Discovered recipes
                    // Undiscovered recipes remain hidden regardless of toggle
                    if (CraftingManager.Instance.IsRecipeDiscovered(id))
                    {
                        if (recipeButtons.TryGetValue(id, out var btn))
                        {
                            btn.gameObject.SetActive(newState);
                        }
                    }
                }
            }
        }

        protected override (string header, string body) GetItemInfo(object item)
        {
            if (item is not RecipeDefinition recipe)
                return ("", "");

            return (
                $"配方：{recipe.ResultingCard.DisplayName}",
                CraftingManager.Instance?.GetFormattedIngredients(recipe) ?? ""
            );
        }

        protected override string GetItemId(object item)
        {
            if (item is RecipeDefinition recipe)
                return recipe.Id;

            return null;
        }
    }
}
