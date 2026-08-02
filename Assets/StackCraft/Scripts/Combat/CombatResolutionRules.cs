using System;

namespace CryingSnow.StackCraft
{
    public static class CombatResolutionRules
    {
        public static float CalculateHitChance(float accuracy, float dodge)
        {
            return Math.Clamp((accuracy - dodge) / 100f, 0.05f, 0.95f);
        }

        public static int CalculateBaseDamage(float attack, float defense)
        {
            return Math.Max(1, Round(attack) - Round(defense));
        }

        public static int CalculateSkillBaseDamage(
            float attack,
            float defense,
            float powerMultiplier,
            int flatPower)
        {
            return Math.Max(
                1,
                Round(attack * Math.Max(0f, powerMultiplier)) +
                flatPower - Round(defense));
        }

        public static float CalculateRetreatChance(float dodge, int aliveEnemyCount)
        {
            return Math.Clamp(
                0.55f + dodge / 100f - Math.Max(0, aliveEnemyCount) * 0.05f,
                0.25f,
                0.90f);
        }

        public static CombatTypeAdvantage GetAdvantage(
            CombatType attacker,
            CombatType defender)
        {
            if (attacker == CombatType.None || defender == CombatType.None ||
                attacker == defender)
                return CombatTypeAdvantage.None;

            bool advantageous =
                attacker == CombatType.Melee && defender == CombatType.Ranged ||
                attacker == CombatType.Ranged && defender == CombatType.Magic ||
                attacker == CombatType.Magic && defender == CombatType.Melee;
            return advantageous
                ? CombatTypeAdvantage.Advantage
                : CombatTypeAdvantage.Disadvantage;
        }

        public static HitResult ResolveAttack(
            float attack,
            float defense,
            float accuracy,
            float dodge,
            float criticalChance,
            float criticalMultiplier,
            CombatType attackerType,
            CombatType defenderType,
            float advantageMultiplier,
            float disadvantageMultiplier,
            CombatRandom random,
            float powerMultiplier = 1f,
            int flatPower = 0,
            bool canCritical = true)
        {
            if (random.NextFloat() > CalculateHitChance(accuracy, dodge))
                return new HitResult(HitType.Miss, 0);

            bool critical = canCritical &&
                random.NextFloat() <= Math.Clamp(criticalChance / 100f, 0f, 1f);
            float damage = powerMultiplier == 1f && flatPower == 0
                ? CalculateBaseDamage(attack, defense)
                : CalculateSkillBaseDamage(
                    attack,
                    defense,
                    powerMultiplier,
                    flatPower);
            CombatTypeAdvantage advantage = GetAdvantage(attackerType, defenderType);
            if (advantage == CombatTypeAdvantage.Advantage)
                damage *= advantageMultiplier;
            else if (advantage == CombatTypeAdvantage.Disadvantage)
                damage *= disadvantageMultiplier;
            if (critical)
                damage *= criticalMultiplier / 100f;

            return new HitResult(
                critical ? HitType.Critical : HitType.Normal,
                Math.Max(1, Round(damage)),
                advantage);
        }

        private static int Round(float value) =>
            (int)Math.Round(value, MidpointRounding.ToEven);
    }
}
