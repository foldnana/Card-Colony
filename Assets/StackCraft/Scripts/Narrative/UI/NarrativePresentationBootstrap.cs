using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class NarrativePresentationBootstrap : MonoBehaviour
    {
        [SerializeField] private NarrativePresentationView presentationPrefab;

        private NarrativePresentationView instance;

        private void Awake()
        {
            if (NarrativePresentationView.Instance != null ||
                presentationPrefab == null)
                return;
            instance = Instantiate(presentationPrefab, transform, false);
            RectTransform rect = instance.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            instance = null;
        }
    }
}
