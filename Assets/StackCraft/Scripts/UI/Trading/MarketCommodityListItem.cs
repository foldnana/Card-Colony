using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    public sealed class MarketCommodityListItem : MonoBehaviour
    {
        [SerializeField] private RawImage icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private TMP_Text detailsLabel;
        [SerializeField] private TMP_Text trendLabel;
        [SerializeField] private Button selectionButton;

        public void Bind(
            CommodityDefinition commodity,
            MarketQuote quote,
            int owned,
            MarketTradeDirection direction,
            bool directionOpen,
            UnityAction action)
        {
            if (icon != null)
            {
                icon.texture =
                    commodity?.CardDefinition?.ArtTexture;
                icon.enabled = icon.texture != null;
            }
            if (nameLabel != null)
            {
                nameLabel.text = commodity?.DisplayName ??
                    quote.CommodityId ??
                    string.Empty;
            }
            bool playerBuys =
                direction == MarketTradeDirection.PlayerBuys;
            if (priceLabel != null)
            {
                priceLabel.text = playerBuys
                    ? $"买入价 {quote.PlayerBuyUnitPrice} 金币/枚"
                    : $"本地收购 {quote.PlayerSellUnitPrice} 金币/枚";
            }
            if (quantityLabel != null)
            {
                quantityLabel.text = playerBuys
                    ? $"市场库存 {quote.AvailableStock}"
                    : $"背包持有 {Mathf.Max(0, owned)}";
            }
            if (detailsLabel != null)
            {
                detailsLabel.text = playerBuys
                    ? $"你有 {Mathf.Max(0, owned)} 件"
                    : $"全部出售可得 " +
                        $"{(long)quote.PlayerSellUnitPrice * Mathf.Max(0, owned)} 金币";
            }
            if (trendLabel != null)
            {
                trendLabel.text = playerBuys
                    ? GetTrendLabel(quote.Trend)
                    : "点击出售";
            }

            if (selectionButton == null)
                return;

            selectionButton.onClick.RemoveAllListeners();
            selectionButton.interactable =
                directionOpen && action != null;
            if (selectionButton.targetGraphic is Image background)
            {
                background.color = playerBuys
                    ? new Color(0.075f, 0.145f, 0.135f, 0.98f)
                    : new Color(0.12f, 0.13f, 0.16f, 0.98f);
            }
            if (directionOpen && action != null)
                selectionButton.onClick.AddListener(action);
        }

        private static string GetTrendLabel(MarketTrend trend)
        {
            return trend switch
            {
                MarketTrend.Abundant => "盛产",
                MarketTrend.Shortage => "短缺",
                MarketTrend.Emergency => "紧急",
                _ => "正常"
            };
        }
    }
}
