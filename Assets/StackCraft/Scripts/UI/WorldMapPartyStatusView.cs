using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class WorldMapPartyStatusView : MonoBehaviour
    {
        public static WorldMapPartyStatusView Instance { get; private set; }

        [SerializeField] private RawImage portraitImage;
        [SerializeField] private TMP_Text partyNameLabel;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text locationLabel;
        [SerializeField] private TMP_Text membersLabel;
        [SerializeField] private TMP_Text stateLabel;

        private CanvasGroup canvasGroup;
        private CardInstance displayedParty;
        private int displayedCurrentHealth = -1;
        private int displayedMaxHealth = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            canvasGroup = GetComponent<CanvasGroup>();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            RefreshHealth();
        }

        public void ShowParty(
            CardInstance partyCard,
            string locationName,
            string state,
            int memberCount)
        {
            if (partyCard == null)
            {
                Hide();
                return;
            }

            displayedParty = partyCard;
            displayedCurrentHealth = -1;
            displayedMaxHealth = -1;
            CardDefinition definition = partyCard.Definition;

            partyNameLabel.text = definition != null &&
                !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : "旅行小队";
            portraitImage.texture = definition?.ArtTexture;
            portraitImage.enabled = portraitImage.texture != null;
            locationLabel.text = $"所在地点：{locationName}";
            membersLabel.text = $"成员：{Mathf.Max(0, memberCount)}";
            stateLabel.text = $"状态：{state}";

            RefreshHealth();
            SetVisible(true);
        }

        public void Hide()
        {
            displayedParty = null;
            SetVisible(false);
        }

        private void RefreshHealth()
        {
            if (displayedParty == null)
                return;

            int currentHealth = Mathf.Max(0, displayedParty.CurrentHealth);
            int maxHealth = displayedParty.Stats != null
                ? Mathf.Max(1, displayedParty.Stats.MaxHealth.Value)
                : Mathf.Max(1, currentHealth);
            if (currentHealth == displayedCurrentHealth && maxHealth == displayedMaxHealth)
                return;

            displayedCurrentHealth = currentHealth;
            displayedMaxHealth = maxHealth;
            healthLabel.text = $"生命  {currentHealth}/{maxHealth}";
            healthFill.fillAmount = Mathf.Clamp01((float)currentHealth / maxHealth);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
