using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public readonly struct CharacterProgressionNotification
    {
        public int ExperienceGained { get; }
        public int PreviousLevel { get; }
        public int CurrentLevel { get; }
        public int CurrentExperience { get; }
        public int RequiredExperience { get; }
        public string Message { get; }
        public bool LeveledUp => CurrentLevel > PreviousLevel;

        public CharacterProgressionNotification(
            int experienceGained,
            int previousLevel,
            int currentLevel,
            int currentExperience,
            string message)
        {
            ExperienceGained = System.Math.Max(0, experienceGained);
            PreviousLevel = System.Math.Max(1, previousLevel);
            CurrentLevel = System.Math.Max(PreviousLevel, currentLevel);
            CurrentExperience = System.Math.Max(0, currentExperience);
            RequiredExperience = CharacterProgressionService
                .GetExperienceRequiredForNextLevel(CurrentLevel);
            Message = message ?? string.Empty;
        }
    }

    public static class CharacterProgressionFeedback
    {
        public static string BuildLevelUpMessage(
            string displayName,
            int previousLevel,
            int currentLevel)
        {
            previousLevel = System.Math.Max(1, previousLevel);
            currentLevel = System.Math.Max(previousLevel, currentLevel);
            var lines = new List<string>
            {
                $"{displayName}升到 {currentLevel} 级"
            };

            int healthIncrease =
                CharacterProgressionService.GetMaxHealthBonus(currentLevel) -
                CharacterProgressionService.GetMaxHealthBonus(previousLevel);
            int attackIncrease =
                CharacterProgressionService.GetAttackBonus(currentLevel) -
                CharacterProgressionService.GetAttackBonus(previousLevel);
            int defenseIncrease =
                CharacterProgressionService.GetDefenseBonus(currentLevel) -
                CharacterProgressionService.GetDefenseBonus(previousLevel);

            if (healthIncrease > 0)
                lines.Add($"最大生命 +{healthIncrease}");
            if (attackIncrease > 0)
                lines.Add($"攻击 +{attackIncrease}");
            if (defenseIncrease > 0)
                lines.Add($"防御 +{defenseIncrease}");

            return string.Join("\n", lines);
        }

        public static string BuildExperienceBar(
            int level,
            int experience,
            int segmentCount)
        {
            segmentCount = System.Math.Max(1, segmentCount);
            int required = CharacterProgressionService
                .GetExperienceRequiredForNextLevel(level);
            experience = System.Math.Clamp(experience, 0, required);
            int filled = (int)System.Math.Floor(
                experience / (double)required * segmentCount);
            return $"经验：[{"".PadLeft(filled, '■')}" +
                $"{"".PadLeft(segmentCount - filled, '□')}] " +
                $"{experience}/{required}";
        }
    }
}
