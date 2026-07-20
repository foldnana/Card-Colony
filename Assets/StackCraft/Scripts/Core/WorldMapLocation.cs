using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class WorldMapLocation : MonoBehaviour
    {
        public int Index { get; private set; } = -1;
        public CardInstance Card { get; private set; }

        public void Initialize(int index, CardInstance card)
        {
            Index = index;
            Card = card;

            if (Card?.Stack != null)
                Card.Stack.IsLocked = true;
        }
    }
}
