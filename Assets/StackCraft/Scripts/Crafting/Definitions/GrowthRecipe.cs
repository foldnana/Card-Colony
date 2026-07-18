using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Special Recipes/Growth Recipe", fileName = "Recipe_Growth_")]
    public class GrowthRecipe : RecipeDefinition
    {
        public override void Execute(CardStack stack)
        {
            // 1. Identify Key Components
            // The "Grower" is the Planter Box/Farm. The "Seed" is the output card definition.
            var growerInstance = stack.Cards.FirstOrDefault(c => c.Definition is GrowerDefinition);
            var seedInstance = stack.Cards.FirstOrDefault(c => c.Definition == this.ResultingCard);

            // 2. Handle Consumption (Planter Box degrades, Water is used, etc.)
            var rules = GetIngredientRules();
            if (growerInstance != null && rules.TryGetValue(growerInstance.Definition, out var rule))
            {
                ApplyConsumptionRule(growerInstance, rule.consumptionMode, stack);
            }

            // 3. Handle the Seed (Split it off into a new stack)
            if (seedInstance != null)
            {
                var newSeedStack = stack.SplitAt(seedInstance);
                if (newSeedStack != null)
                {
                    CardManager.Instance.RegisterStack(newSeedStack);
                    // Move it slightly to the side visually
                    newSeedStack.ApplyTranslation(new Vector3(seedInstance.Size.x, 0, 0));
                    CardManager.Instance?.ResolveOverlaps();
                }
            }

            // 4. Spawn the actual Grown Result
            if (ResultingCard != null)
            {
                CardManager.Instance?.CreateCardInstance(ResultingCard, stack.TargetPosition.Flatten(), stack);
            }
        }
    }
}
