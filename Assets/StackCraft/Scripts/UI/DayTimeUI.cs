using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CanvasGroup))]
    public class DayTimeUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField, Tooltip("The TextMeshPro label displaying the current day number.")]
        private TextMeshProUGUI dayText;

        [SerializeField, Tooltip("The Image component (filled image) that visualizes the progress through the current day.")]
        private Image timeProgress;

        [SerializeField, Tooltip("The Image component that displays the icon corresponding to the current time pace.")]
        private Image paceImage;

        [SerializeField, Tooltip("An array of Sprites representing different Time Pace states ( 0 = Paused, 1 = Normal, 2 = Fast.")]
        private Sprite[] paceIcons;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayEnded += HandleDayEnded;
                TimeManager.Instance.OnDayStarted += HandleDayStarted;

                HandleDayStarted(TimeManager.Instance.CurrentDay);
            }
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayEnded -= HandleDayEnded;
                TimeManager.Instance.OnDayStarted -= HandleDayStarted;
            }
        }

        private void Update()
        {
            timeProgress.fillAmount = TimeManager.Instance.NormalizedTime;
        }

        private void HandleDayEnded(int _)
        {
            paceImage.sprite = paceIcons[0];

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        private void HandleDayStarted(int currentDay)
        {
            dayText.text = $"第 {currentDay} 天";
            paceImage.sprite = paceIcons[1];

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TimeManager.Instance.CycleTimePace(out int timePaceIndex);
            paceImage.sprite = paceIcons[timePaceIndex];

            AudioManager.Instance?.PlaySFX(AudioId.Click);
        }
    }
}
