using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Combat/Item")]
    public sealed class CombatItemDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private CombatTargetRule targetRule = CombatTargetRule.SingleLivingAlly;
        [SerializeField] private CombatItemEffectType effectType = CombatItemEffectType.Heal;
        [SerializeField, Min(0)] private int magnitude;
        [SerializeField] private bool consumesAction = true;

        public string Id => id;
        public CombatTargetRule TargetRule => targetRule;
        public CombatItemEffectType EffectType => effectType;
        public int Magnitude => magnitude;
        public bool ConsumesAction => consumesAction;

        public int Healing => effectType == CombatItemEffectType.Heal ? magnitude : 0;
    }
}
