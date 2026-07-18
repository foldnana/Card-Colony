using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(Canvas))]
    public class WorldCanvas : MonoBehaviour
    {
        public static WorldCanvas Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
        }
    }
}
