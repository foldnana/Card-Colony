using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardColony.Gameplay;
using CardColony.Inventory;

namespace CardColony.UnityIntegration.UI
{
    public sealed class ItemCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private Button takeButton;
        [SerializeField] private Button splitButton;

        public void Bind(ItemCardStack card, Action onTake, Action onSplit)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            titleText.text = $"{GetDisplayName(card.ItemId)}\n数量{card.Quantity}";
            titleText.enableWordWrapping = true;
            detailsText.text = $"品质{card.Quality}\n重量{card.TotalWeight:0.##}\n批次{card.BatchId}";
            detailsText.enableWordWrapping = true;

            takeButton.onClick.RemoveAllListeners();
            takeButton.onClick.AddListener(() => onTake?.Invoke());
            splitButton.onClick.RemoveAllListeners();
            splitButton.onClick.AddListener(() => onSplit?.Invoke());
            splitButton.interactable = card.Quantity > 1;
        }

        private static string GetDisplayName(string itemId)
        {
            switch (itemId)
            {
                case PlayableLoopSession.HerbItemId:
                    return "野生草药";
                case PlayableLoopSession.PotionItemId:
                    return "治疗药水";
                default:
                    return itemId;
            }
        }
    }
}
