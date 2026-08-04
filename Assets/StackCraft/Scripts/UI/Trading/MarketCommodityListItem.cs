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
        [SerializeField] private Sprite buyButtonSprite;
        [SerializeField] private Sprite sellButtonSprite;

        private static readonly Color WarmIvory =
            new(0.14f, 0.16f, 0.19f, 1f);
        private static readonly Color PriceGold =
            new(0.60f, 0.36f, 0.08f, 1f);
        private static readonly Color StockBlue =
            new(0.10f, 0.38f, 0.58f, 1f);
        private static readonly Color DetailsSilver =
            new(0.34f, 0.38f, 0.42f, 1f);
        private static readonly Color TrendGreen =
            new(0.15f, 0.44f, 0.25f, 1f);
        private static readonly Color SellPromptGold =
            new(0.60f, 0.36f, 0.08f, 1f);
        private static readonly Color LightRow =
            new(0.84f, 0.86f, 0.87f, 1f);

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
                nameLabel.color = WarmIvory;
            }
            bool playerBuys =
                direction == MarketTradeDirection.PlayerBuys;
            if (priceLabel != null)
            {
                priceLabel.text = playerBuys
                    ? $"买入价 {quote.PlayerBuyUnitPrice} 金币/枚"
                    : $"本地收购 {quote.PlayerSellUnitPrice} 金币/枚";
                priceLabel.color = PriceGold;
            }
            if (quantityLabel != null)
            {
                quantityLabel.text = playerBuys
                    ? $"市场库存 {quote.AvailableStock}"
                    : $"背包持有 {Mathf.Max(0, owned)}";
                quantityLabel.color = StockBlue;
            }
            if (detailsLabel != null)
            {
                detailsLabel.text = playerBuys
                    ? $"你有 {Mathf.Max(0, owned)} 件"
                    : $"全部出售可得 " +
                        $"{(long)quote.PlayerSellUnitPrice * Mathf.Max(0, owned)} 金币";
                detailsLabel.color = DetailsSilver;
            }
            if (trendLabel != null)
            {
                trendLabel.text = playerBuys
                    ? GetTrendLabel(quote.Trend)
                    : "点击出售";
                trendLabel.color = playerBuys
                    ? TrendGreen
                    : SellPromptGold;
            }

            if (selectionButton == null)
                return;

            selectionButton.onClick.RemoveAllListeners();
            selectionButton.interactable =
                directionOpen && action != null;
            if (selectionButton.targetGraphic is Image background)
            {
                Sprite buttonSprite = playerBuys
                    ? buyButtonSprite
                    : sellButtonSprite;
                if (buttonSprite != null)
                {
                    background.sprite = buttonSprite;
                    background.type = Image.Type.Sliced;
                    background.color = LightRow;
                }
            }
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor =
                new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor =
                new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor =
                new Color(0.50f, 0.50f, 0.50f, 0.68f);
            colors.fadeDuration = 0.08f;
            selectionButton.colors = colors;
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
