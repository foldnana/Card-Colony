using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class NpcActionRowView : MonoBehaviour
    {
        [SerializeField] private Button rowButton;
        [SerializeField] private TMP_Text iconLabel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text arrowLabel;

        public void Bind(
            string icon,
            string title,
            string description,
            UnityAction action)
        {
            if (iconLabel != null)
                iconLabel.text = icon ?? string.Empty;
            if (titleLabel != null)
                titleLabel.text = title ?? string.Empty;
            if (descriptionLabel != null)
                descriptionLabel.text = description ?? string.Empty;

            bool available = action != null;
            if (arrowLabel != null)
                arrowLabel.gameObject.SetActive(available);
            if (rowButton == null)
                return;

            rowButton.onClick.RemoveAllListeners();
            rowButton.interactable = available;
            if (available)
                rowButton.onClick.AddListener(action);
        }
    }
}
