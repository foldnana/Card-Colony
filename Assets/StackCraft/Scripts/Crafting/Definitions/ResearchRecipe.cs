using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Special Recipes/Research Recipe", fileName = "Recipe_Research_")]
    public class ResearchRecipe : RecipeDefinition
    {
        public override void Execute(CardStack stack)
        {
            // 1. Consume the inputs
            var rules = GetIngredientRules();
            ConsumeIngredients(stack, rules);

            // 2. Find an Undiscovered Recipe
            var manager = CraftingManager.Instance;
            var undiscovered = manager.AllRecipes
                .Where(r => !manager.IsRecipeDiscovered(r.Id)
                            && r is not ResearchRecipe
                            && r is not GrowthRecipe
                            && r is not ExplorationRecipe
                            && r.ResultingCard != null)
                .ToList();

            if (undiscovered.Count > 0)
            {
                // 3. Pick random & Spawn
                var randomRecipe = undiscovered[Random.Range(0, undiscovered.Count)];

                if (CardManager.Instance != null)
                {
                    CardManager.Instance.SpawnRecipeCard(randomRecipe, stack);
                    // 4. Mark Discovered
                    manager.MarkRecipeAsDiscovered(randomRecipe);
                }
            }
            else
            {
                Debug.Log("All research complete!");
            }
        }
    }
}
