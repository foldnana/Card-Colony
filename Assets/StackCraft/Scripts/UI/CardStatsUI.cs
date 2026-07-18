using UnityEngine;
using TMPro;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CardStatsUI : MonoBehaviour
    {
        [SerializeField, Tooltip("The TextMeshPro label displaying the current nutrition level versus the required nutrition.")]
        private TextMeshProUGUI nutritionLabel;

        [SerializeField, Tooltip("The TextMeshPro label displaying the player's current total currency/coins.")]
        private TextMeshProUGUI currencyLabel;

        [SerializeField, Tooltip("The TextMeshPro label displaying the number of cards currently owned versus the maximum capacity.")]
        private TextMeshProUGUI cardLabel;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnStatsChanged += UpdateStatsText;
                UpdateStatsText(CardManager.Instance.GetStatsSnapshot());
            }

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayEnded += HandleDayEnded;
                TimeManager.Instance.OnDayStarted += HandleDayStarted;
            }
        }

        private void OnDestroy()
        {
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnStatsChanged -= UpdateStatsText;
            }

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayEnded -= HandleDayEnded;
                TimeManager.Instance.OnDayStarted -= HandleDayStarted;
            }
        }

        private void HandleDayEnded(int _)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        private void HandleDayStarted(int _)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        private void UpdateStatsText(StatsSnapshot stats)
        {
            nutritionLabel.text = $"{stats.TotalNutrition}/{stats.NutritionNeed}";
            currencyLabel.text = $"{stats.Currency}";
            cardLabel.text = $"{stats.CardsOwned}/{stats.CardLimit}";
        }
    }
}
