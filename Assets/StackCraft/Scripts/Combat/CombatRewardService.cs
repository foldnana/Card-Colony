using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public readonly struct CombatRewardResult
    {
        public bool ProtagonistCredited { get; }
        public CardInstance Protagonist { get; }
        public int Experience { get; }

        public CombatRewardResult(
            bool protagonistCredited,
            CardInstance protagonist,
            int experience)
        {
            ProtagonistCredited = protagonistCredited;
            Protagonist = protagonist;
            Experience = experience;
        }
    }

    public sealed class CombatRewardService
    {
        public CombatRewardResult ResolveDefeat(
            CardInstance defeated,
            IEnumerable<CardInstance> participants)
        {
            CardInstance protagonist = participants?.FirstOrDefault(
                ProtagonistRules.IsProtagonist);
            bool isEnemy = defeated?.Definition?.Faction == CardFaction.Mob;
            bool credited = isEnemy && protagonist != null;
            int experience = credited
                ? System.Math.Max(0, defeated.Definition.ExperienceReward)
                : 0;
            if (experience > 0)
                GameDirector.Instance?.GrantProtagonistExperience(experience);
            if (isEnemy)
                WorldQuestRuntime.Instance?.ReportEnemyDefeated(
                    defeated.Definition.Id,
                    credited);
            return new CombatRewardResult(credited, protagonist, experience);
        }
    }
}
