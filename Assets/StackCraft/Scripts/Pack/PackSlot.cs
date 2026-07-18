using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [System.Serializable]
    public class PackSlot
    {
        [Tooltip("Weighted entries for this slot")]
        public List<PackEntry> Entries;

        [Tooltip("A list of possible recipes that can be drawn instead")]
        public List<RecipeDefinition> PossibleRecipes;

        [Range(0f, 1f), Tooltip("Chance (0-1) of giving a recipe card instead of a weighted entry")]
        public float RecipeChance = 0.1f;

        public CardDefinition GetRandomCard()
        {
            // 1. First, check the RecipeChance
            if (PossibleRecipes != null && PossibleRecipes.Count > 0 && Random.value < RecipeChance)
            {
                // 2. If the chance hits, find ALL available undiscovered recipes.
                var undiscoveredRecipes = PossibleRecipes
                    .Where(recipe => recipe != null && !CraftingManager.Instance.IsRecipeDiscovered(recipe.Id))
                    .ToList();

                // 3. If we found any undiscovered recipes...
                if (undiscoveredRecipes.Count > 0)
                {
                    // ...pick one at random from the 'filtered' list and return it.
                    int index = Random.Range(0, undiscoveredRecipes.Count);
                    var undiscoveredRecipe = undiscoveredRecipes[index];
                    CraftingManager.Instance.MarkRecipeAsDiscovered(undiscoveredRecipe);
                    return CardManager.Instance?.CreateRecipeCardDefinition(undiscoveredRecipe);
                }
            }

            // --- Fallback to Weighted Entries ---
            // (This code runs if RecipeChance failed, or if no undiscovered recipes were found)
            if (Entries == null || Entries.Count == 0)
                return null;

            int totalWeight = 0;
            foreach (var entry in Entries)
            {
                if (entry != null && entry.Card != null)
                {
                    totalWeight += Mathf.Max(1, entry.Weight);
                }
            }

            if (totalWeight == 0)
                return null;

            int roll = Random.Range(0, totalWeight);
            foreach (var entry in Entries)
            {
                if (entry == null || entry.Card == null)
                    continue;

                int entryWeight = Mathf.Max(1, entry.Weight);
                if (roll < entryWeight)
                {
                    return entry.Card;
                }
                roll -= entryWeight;
            }

            return null; // Should ideally not be reached if totalWeight > 0
        }
    }
}
