using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Special Cards/Chest Card", fileName = "Card_Chest_")]
    public class ChestDefinition : CardDefinition
    {
        [SerializeField, Tooltip("How many currency units this chest can hold (e.g., Coins, Corals).")]
        private int capacity = 50;

        public int Capacity => capacity;
    }
}
