using System.Collections;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            canvasGroup = GetComponent<CanvasGroup>();
        }

        public IEnumerator Fade(float startAlpha, float endAlpha, float fadeDuration = 1.0f)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            canvasGroup.blocksRaycasts = (endAlpha > 0.01f);

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = endAlpha;
        }
    }
}
