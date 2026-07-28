using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    public sealed class NpcTradeListRowView : MonoBehaviour
    {
        [SerializeField] private RawImage icon;
        [SerializeField] private TMP_Text detailsLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryButtonLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryButtonLabel;

        private bool primaryLabelDefaultsCaptured;
        private bool primaryLabelDefaultAutoSizing;
        private float primaryLabelDefaultFontSize;
        private float primaryLabelDefaultFontSizeMin;
        private float primaryLabelDefaultFontSizeMax;

        public void Bind(
            CardDefinition definition,
            string details,
            string primaryText,
            UnityEngine.Events.UnityAction primaryAction,
            string secondaryText = null,
            UnityEngine.Events.UnityAction secondaryAction = null)
        {
            ApplyMerchandiseLayout();
            if (icon != null)
            {
                icon.gameObject.SetActive(true);
                icon.texture = definition?.ArtTexture;
                icon.enabled = icon.texture != null;
            }

            if (detailsLabel != null)
                detailsLabel.text = details ?? string.Empty;
            BindButton(
                primaryButton,
                primaryButtonLabel,
                primaryText,
                primaryAction);
            BindButton(
                secondaryButton,
                secondaryButtonLabel,
                secondaryText,
                secondaryAction);
        }

        public void BindAction(
            string details,
            string primaryText,
            UnityEngine.Events.UnityAction primaryAction)
        {
            ApplyActionLayout();
            if (detailsLabel != null)
                detailsLabel.text = details ?? string.Empty;
            BindButton(
                primaryButton,
                primaryButtonLabel,
                primaryText,
                primaryAction);
            BindButton(
                secondaryButton,
                secondaryButtonLabel,
                null,
                null);
        }

        private void ApplyMerchandiseLayout()
        {
            SetAnchors(icon?.rectTransform, 0.02f, 0.18f, 0.12f, 0.88f);
            SetAnchors(detailsLabel?.rectTransform, 0.20f, 0.56f, 0.08f, 0.92f);
            SetAnchors(primaryButton?.transform as RectTransform, 0.58f, 0.78f, 0.16f, 0.84f);
            SetAnchors(secondaryButton?.transform as RectTransform, 0.80f, 0.98f, 0.16f, 0.84f);

            if (TryGetComponent(out LayoutElement layout))
                layout.preferredHeight = 104f;
            ConfigurePrimaryLabel(false);
        }

        private void ApplyActionLayout()
        {
            if (icon != null)
                icon.gameObject.SetActive(false);

            SetAnchors(detailsLabel?.rectTransform, 0.04f, 0.68f, 0.08f, 0.92f);
            SetAnchors(primaryButton?.transform as RectTransform, 0.72f, 0.98f, 0.16f, 0.84f);

            if (TryGetComponent(out LayoutElement layout))
                layout.preferredHeight = 88f;
            ConfigurePrimaryLabel(true);
        }

        private void ConfigurePrimaryLabel(bool autoSize)
        {
            if (primaryButtonLabel == null)
                return;

            if (!primaryLabelDefaultsCaptured)
            {
                primaryLabelDefaultAutoSizing =
                    primaryButtonLabel.enableAutoSizing;
                primaryLabelDefaultFontSize =
                    primaryButtonLabel.fontSize;
                primaryLabelDefaultFontSizeMin =
                    primaryButtonLabel.fontSizeMin;
                primaryLabelDefaultFontSizeMax =
                    primaryButtonLabel.fontSizeMax;
                primaryLabelDefaultsCaptured = true;
            }

            if (autoSize)
            {
                primaryButtonLabel.enableAutoSizing = true;
                primaryButtonLabel.fontSizeMin = 14f;
                primaryButtonLabel.fontSizeMax = 20f;
                return;
            }

            primaryButtonLabel.enableAutoSizing =
                primaryLabelDefaultAutoSizing;
            primaryButtonLabel.fontSize =
                primaryLabelDefaultFontSize;
            primaryButtonLabel.fontSizeMin =
                primaryLabelDefaultFontSizeMin;
            primaryButtonLabel.fontSizeMax =
                primaryLabelDefaultFontSizeMax;
        }

        private static void SetAnchors(
            RectTransform rect,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void BindButton(
            Button button,
            TMP_Text label,
            string text,
            UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            bool visible = !string.IsNullOrWhiteSpace(text) &&
                action != null;
            button.gameObject.SetActive(visible);
            button.onClick.RemoveAllListeners();
            if (!visible)
                return;

            if (label != null)
                label.text = text;
            button.onClick.AddListener(action);
        }
    }
}
