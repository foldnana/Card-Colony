using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Combat/Skill")]
    public sealed class CombatSkillDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Texture2D icon;
        [SerializeField] private CombatTargetRule targetRule = CombatTargetRule.SingleLivingEnemy;
        [SerializeField, Min(0)] private int energyCost;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(0f)] private float powerMultiplier = 1f;
        [SerializeField] private int flatPower;
        [SerializeField] private bool canCritical = true;
        [SerializeField] private bool retargetIfInvalid = true;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Texture2D Icon => icon;
        public CombatTargetRule TargetRule => targetRule;
        public int EnergyCost => energyCost;
        public float CooldownSeconds => cooldownSeconds;
        public float PowerMultiplier => powerMultiplier;
        public int FlatPower => flatPower;
        public bool CanCritical => canCritical;
        public bool RetargetIfInvalid => retargetIfInvalid;

        public float DamageMultiplier => PowerMultiplier;
    }
}
