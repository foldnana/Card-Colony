namespace CryingSnow.StackCraft
{
    public class CombatStats
    {
        public Stat MaxHealth { get; private set; }
        public Stat Attack { get; private set; }
        public Stat Defense { get; private set; }
        public Stat AttackSpeed { get; private set; }
        public Stat Accuracy { get; private set; }
        public Stat Dodge { get; private set; }
        public Stat CriticalChance { get; private set; }
        public Stat CriticalMultiplier { get; private set; }

        public CombatStats(float maxHealth, float attack, float defense, float attackSpeed,
                           float accuracy, float dodge, float criticalChance, float criticalMultiplier)
        {
            MaxHealth = new Stat(maxHealth);
            Attack = new Stat(attack);
            Defense = new Stat(defense);
            AttackSpeed = new Stat(attackSpeed);
            Accuracy = new Stat(accuracy);
            Dodge = new Stat(dodge);
            CriticalChance = new Stat(criticalChance);
            CriticalMultiplier = new Stat(criticalMultiplier);
        }

        /// <summary>
        /// Generates a multiline string containing the current base value of every combat statistic,
        /// formatted for display in a UI tooltip or debug log.
        /// </summary>
        /// <returns>A string with each combat stat and its value on a new line.</returns>
        public string GetFormattedStats()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.Append($"生命上限（{MaxHealth.Value.ToString()}）\n");
            sb.Append($"攻击（{Attack.Value.ToString()}）\n");
            sb.Append($"防御（{Defense.Value.ToString()}）\n");
            sb.Append($"攻击速度（{AttackSpeed.Value.ToString()}）\n");
            sb.Append($"命中率（{Accuracy.Value.ToString()}）\n");
            sb.Append($"闪避率（{Dodge.Value.ToString()}）\n");
            sb.Append($"暴击率（{CriticalChance.Value.ToString()}）\n");
            sb.Append($"暴击倍率（{CriticalMultiplier.Value.ToString()}）");

            return sb.ToString();
        }
    }
}
