namespace CryingSnow.StackCraft
{
    public enum HitType
    {
        Miss,
        Normal,
        Critical
    }

    public enum CombatTypeAdvantage
    {
        None,
        Advantage,
        Disadvantage
    }

    public struct HitResult
    {
        public HitType Type;
        public int Damage;
        public CombatTypeAdvantage Advantage;

        public HitResult(HitType type, int damage, CombatTypeAdvantage advantage = CombatTypeAdvantage.None)
        {
            Type = type;
            Damage = damage;
            Advantage = advantage;
        }

        public bool IsHit => Type != HitType.Miss;
        public bool IsCritical => Type == HitType.Critical;
    }
}
