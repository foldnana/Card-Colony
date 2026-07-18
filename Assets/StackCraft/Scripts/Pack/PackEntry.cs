using UnityEngine;

namespace CryingSnow.StackCraft
{
    [System.Serializable]
    public class PackEntry
    {
        public CardDefinition Card;
        [Tooltip("Higher = More Likely")]
        public int Weight = 1;
    }
}
