using UnityEngine;

namespace CryingSnow.StackCraft
{
    public readonly struct CharacterProgressionResult
    {
        public int LevelsGained { get; }

        public CharacterProgressionResult(int levelsGained)
        {
            LevelsGained = levelsGained;
        }
    }

    public static class CharacterProgressionService
    {
        public const int MaxLevel = 10;

        public static int GetExperienceRequiredForNextLevel(int level)
        {
            return 15 + (Mathf.Max(1, level) - 1) * 10;
        }

        public static CharacterProgressionResult GrantExperience(
            CardData character,
            int amount)
        {
            if (character == null || amount <= 0)
                return new CharacterProgressionResult(0);

            character.NormalizeProgression();
            int previousLevel = character.Level;
            character.Experience += amount;
            int levelsGained = 0;
            while (character.Level < MaxLevel)
            {
                int required =
                    GetExperienceRequiredForNextLevel(character.Level);
                if (character.Experience < required)
                    break;

                character.Experience -= required;
                character.Level++;
                levelsGained++;
            }

            if (character.Level >= MaxLevel)
                character.Experience = 0;
            if (character.MaximumHealth > 0 &&
                character.Level != previousLevel)
            {
                int healthIncrease =
                    GetMaxHealthBonus(character.Level) -
                    GetMaxHealthBonus(previousLevel);
                character.MaximumHealth += healthIncrease;
                character.CurrentHealth = Mathf.Min(
                    character.MaximumHealth,
                    character.CurrentHealth + healthIncrease);
            }

            return new CharacterProgressionResult(levelsGained);
        }

        public static int GetMaxHealthBonus(int level)
        {
            return (Mathf.Max(1, level) - 1) * 2;
        }

        public static int GetAttackBonus(int level)
        {
            return Mathf.Max(1, level) / 2;
        }

        public static int GetDefenseBonus(int level)
        {
            return (Mathf.Max(1, level) - 1) / 2;
        }
    }
}
