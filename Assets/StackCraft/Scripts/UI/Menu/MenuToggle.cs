using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(Button))]
    public class MenuToggle : MonoBehaviour
    {
        [SerializeField, Tooltip("The UI element to animate (slide in/out). Assumed to be right-aligned.")]
        private RectTransform targetRect;

        [SerializeField, Tooltip("The text label on the button (e.g., '>>' or '<<').")]
        private TextMeshProUGUI labelText;

        private Button button;

        private bool isAnimating;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void Start()
        {
            button?.onClick.AddListener(Toggle);

            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayEnded += HandleDayEnded;
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveAllListeners();

            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayEnded -= HandleDayEnded;
        }

        /// <summary>
        /// Toggles the target menu's visibility with a sliding animation.
        /// Assumes "open" is at anchoredPosition.x = 0 and "closed" is at x = targetWidth.
        /// </summary>
        private void Toggle()
        {
            if (isAnimating) return;
            isAnimating = true;

            float targetWidth = targetRect.sizeDelta.x;

            // Determine target position: if open (at or left of 0), move to closed (targetWidth).
            // If closed (at targetWidth), move to open (0).
            float targetPosX = targetRect.anchoredPosition.x <= 0f ? targetWidth : 0f;

            targetRect.DOAnchorPosX(targetPosX, 0.5f)
                .SetUpdate(true) // Ensure animation plays even if Time.timeScale is 0
                .OnComplete(() =>
                {
                    isAnimating = false;
                    // Update label to reflect the new state
                    // If targetPosX > 0, it just closed, so show "<<" (to open).
                    // If targetPosX = 0, it just opened, so show ">>" (to close).
                    labelText.text = targetPosX > 0f ? "<<" : ">>";
                });

            AudioManager.Instance?.PlaySFX(AudioId.Click);
        }

        /// <summary>
        /// Automatically closes the menu if it's open when the day ends.
        /// </summary>
        private void HandleDayEnded(int _)
        {
            // If the menu is open (at or beyond x=0)
            if (targetRect.anchoredPosition.x <= 0f)
            {
                Toggle(); // Close it
            }
        }
    }
}
