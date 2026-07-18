using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Special Recipes/Travel Recipe", fileName = "Recipe_Travel_")]
    public class TravelRecipe : RecipeDefinition
    {
        [SerializeField] private List<string> targetScenes;

        public override void Execute(CardStack stack)
        {
            List<CardInstance> travelers = stack.Cards.ToList();
            var rules = GetIngredientRules();
            ConsumeIngredients(stack, rules);
            GameDirector.Instance?.InitiateTravel(targetScenes, travelers);
        }
    }
}
