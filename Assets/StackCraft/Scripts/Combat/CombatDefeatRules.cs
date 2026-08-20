using UnityEngine;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Controls what happens when a combat side receives lethal damage.
    /// Narrative background combats can preserve actors or keep both sides
    /// fighting without weakening the rules used by ordinary combat.
    /// </summary>
    public enum CombatDefeatRule
    {
        Lethal = 0,
        PreserveAtOne = 1,
        LockAtOne = 2
    }

    public static class CombatDefeatRules
    {
        public static int ResolveDamage(
            int currentHealth,
            int incomingDamage,
            CombatDefeatRule rule)
        {
            int health = Mathf.Max(0, currentHealth);
            int damage = Mathf.Max(0, incomingDamage);
            if (rule is CombatDefeatRule.PreserveAtOne or
                CombatDefeatRule.LockAtOne)
            {
                return Mathf.Min(damage, Mathf.Max(0, health - 1));
            }
            return Mathf.Min(damage, health);
        }

        public static bool ShouldResolveDefeat(
            int remainingHealth,
            CombatDefeatRule rule)
        {
            return rule == CombatDefeatRule.PreserveAtOne
                ? remainingHealth <= 1
                : rule == CombatDefeatRule.Lethal && remainingHealth <= 0;
        }

        public static bool PreservesCard(CombatDefeatRule rule) =>
            rule == CombatDefeatRule.PreserveAtOne;
    }
}
