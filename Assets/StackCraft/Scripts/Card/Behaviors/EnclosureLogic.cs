using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class EnclosureLogic : MonoBehaviour
    {
        public int Capacity { get; private set; }

        public void Initialize(int capacity)
        {
            Capacity = capacity;
        }
    }
}
