namespace CryingSnow.StackCraft
{
    /// <summary>Builds immutable-in-practice action plans without mutating cards.</summary>
    public sealed class CombatActionResolver
    {
        public CombatActionPlan PlanAttack(
            CombatCommand command,
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
            return new CombatActionPlan
            {
                Command = command,
                Hit = CombatResolutionRules.ResolveAttack(
                    attack,
                    defense,
                    accuracy,
                    dodge,
                    criticalChance,
                    criticalMultiplier,
                    attackerType,
                    defenderType,
                    advantageMultiplier,
                    disadvantageMultiplier,
                    random,
                    powerMultiplier,
                    flatPower,
                    canCritical)
            };
        }

        public CombatActionPlan PlanHealing(CombatCommand command, int amount)
        {
            return new CombatActionPlan
            {
                Command = command,
                Healing = System.Math.Max(0, amount)
            };
        }
    }
}
