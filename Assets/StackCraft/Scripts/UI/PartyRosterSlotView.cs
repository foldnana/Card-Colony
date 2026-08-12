using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PartyRosterSlotView : MonoBehaviour
    {
        [SerializeField] private RawImage portraitImage;
        [SerializeField] private GameObject expandedContent;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text locationStateLabel;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image availabilityDot;
        [SerializeField] private Image selectionOutline;

        private Button button;
        private CardData memberData;
        private CardInstance liveCard;

        public string PersistentId => memberData?.PersistentId;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(SelectMember);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(SelectMember);
        }

        public void Bind(
            CardData member,
            CardDefinition definition,
            CardInstance card,
            string locationName,
            string state,
            bool selected)
        {
            memberData = member;
            liveCard = card;
            if (button == null)
                button = GetComponent<Button>();

            bool hasMember = member != null;
            button.interactable = hasMember;
            portraitImage.texture = hasMember ? definition?.ArtTexture : null;
            portraitImage.enabled = portraitImage.texture != null;
            nameLabel.text = hasMember
                ? GetDisplayName(definition, member.Id)
                : "空位";
            locationStateLabel.text = hasMember
                ? $"{locationName} · {(member.IsDowned ? "倒地" : state)}"
                : "等待成员加入";

            availabilityDot.color = !hasMember
                ? new Color(0.52f, 0.57f, 0.62f, 1f)
                : member.IsDowned
                    ? new Color(0.86f, 0.31f, 0.27f, 1f)
                    : new Color(0.25f, 0.67f, 0.45f, 1f);
            RefreshVitals();
            SetSelected(selected);
        }

        public void RefreshVitals()
        {
            if (memberData == null)
            {
                healthLabel.text = string.Empty;
                healthFill.fillAmount = 0f;
                return;
            }

            int current = Mathf.Max(
                0,
                liveCard != null
                    ? liveCard.CurrentHealth
                    : memberData.CurrentHealth);
            int maximum = liveCard?.Stats != null
                ? Mathf.Max(1, liveCard.Stats.MaxHealth.Value)
                : Mathf.Max(1, memberData.MaximumHealth > 0
                    ? memberData.MaximumHealth
                    : current);
            healthLabel.text = $"{current}/{maximum}";
            healthFill.fillAmount = Mathf.Clamp01((float)current / maximum);
        }

        public void SetSelected(bool selected)
        {
            selectionOutline.enabled = memberData != null && selected;
        }

        public void SetCollapsed(bool collapsed)
        {
            expandedContent.SetActive(!collapsed);
            RectTransform rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(collapsed ? 64f : 244f, rect.sizeDelta.y);

            RectTransform portraitRect = (RectTransform)portraitImage.transform.parent;
            portraitRect.anchorMin = collapsed
                ? new Vector2(0.5f, 0.5f)
                : new Vector2(0f, 0.5f);
            portraitRect.anchorMax = portraitRect.anchorMin;
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = collapsed
                ? Vector2.zero
                : new Vector2(34f, 0f);
        }

        private void SelectMember()
        {
            if (memberData != null)
                PartySelectionService.Select(memberData.PersistentId);
        }

        private static string GetDisplayName(
            CardDefinition definition,
            string fallbackId)
        {
            return definition != null &&
                !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : string.IsNullOrWhiteSpace(fallbackId)
                    ? "未知成员"
                    : fallbackId;
        }
    }
}
