using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Special Cards/Limit Booster Card", fileName = "Card_Booster_")]
    public class LimitBoosterDefinition : CardDefinition
    {
        [SerializeField, Tooltip("How much extra card capacity this card provides.")]
        private int boostAmount = 4;

        public int BoostAmount => boostAmount;
    }
}
