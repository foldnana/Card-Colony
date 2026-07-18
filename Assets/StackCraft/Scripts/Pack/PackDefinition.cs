using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Pack", fileName = "Pack_")]
    public class PackDefinition : CardDefinition
    {
        [SerializeField, Min(0)] private int buyPrice = 3;
        [SerializeField, Min(0)] private int minQuests = 3;
        [SerializeField] private List<PackSlot> slots;

        public int BuyPrice => buyPrice;
        public int MinQuests => minQuests;
        public List<PackSlot> Slots => slots;
    }
}
