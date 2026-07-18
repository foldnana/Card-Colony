using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Special Cards/Enclosure Card", fileName = "Card_Enclosure_")]
    public class EnclosureDefinition : CardDefinition
    {
        [SerializeField, Tooltip("How many non-aggressive mobs can stay in this stack without moving away.")]
        private int capacity = 1;

        public int Capacity => capacity;
    }
}
