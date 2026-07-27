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

        public void Bind(
            CardDefinition definition,
            string details,
            string primaryText,
            UnityEngine.Events.UnityAction primaryAction,
            string secondaryText = null,
            UnityEngine.Events.UnityAction secondaryAction = null)
        {
            if (icon != null)
            {
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
