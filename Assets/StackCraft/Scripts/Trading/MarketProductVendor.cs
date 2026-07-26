using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class MarketProductVendor : TradeZone, IPointerClickHandler
    {
        private static MarketProductVendor activeSelection;

        public static event Action<MarketProductVendor> SelectionChanged;
        public static MarketProductVendor ActiveSelection => activeSelection;

        public CardDefinition Product { get; private set; }
        public int BuyPrice { get; private set; }
        public int StockRemaining => stockState?.Remaining ?? 0;
        public bool IsSelected => activeSelection == this;
        public static CardStack DeliveryStackToIgnore => CardStack.RefuseAll;
        public bool CanPurchase => Product != null &&
            currencyCard != null &&
            StockRemaining > 0 &&
            MarketCurrencyService.CountAvailable(
                currencyCard,
                BackpackService.Current,
                CardManager.Instance?.AllCards) >= BuyPrice;

        private CardDefinition currencyCard;
        private CardInstance pickupCard;
        private int minimumDailyStock;
        private int maximumDailyStock;
        private string offerId;
        private SceneData stockSceneData;
        private MarketStockData stockState;

        public void Configure(
            CardDefinition product,
            int buyPrice,
            CardDefinition currency)
        {
            Configure(
                product,
                buyPrice,
                1,
                1,
                currency,
                null);
        }

        public void Configure(
            CardDefinition product,
            int buyPrice,
            int minimumStock,
            int maximumStock,
            CardDefinition currency,
            CardInstance pickup)
        {
            Product = product;
            BuyPrice = Mathf.Max(1, buyPrice);
            minimumDailyStock = Mathf.Max(1, minimumStock);
            maximumDailyStock = Mathf.Max(
                minimumDailyStock,
                maximumStock);
            currencyCard = currency;
            pickupCard = pickup;
            base.Initialize(product, new Vector3(0f, 0f, -1.15f));

            CardInstance card = GetComponent<CardInstance>();
            offerId = card?.Definition?.Id ?? product?.Id;
            stockSceneData = ResolveStockSceneData();
            RefreshStock(CurrentDay);
            SubscribeToDayChanges();
        }

        public override bool CanTrade(CardStack droppedStack)
        {
            return false;
        }

        protected override void ProcessTransaction(CardStack droppedStack) { }

        public bool TryPurchase()
        {
            RefreshStock(CurrentDay);
            if (!CanPurchase || CardManager.Instance == null)
                return false;

            Vector3 deliveryPosition = pickupCard != null
                ? pickupCard.transform.position + new Vector3(0f, 0f, -1.1f)
                : spawnPosition;
            CardInstance productCard = CardManager.Instance.CreateCardInstance(
                Product,
                deliveryPosition,
                DeliveryStackToIgnore,
                notifyCreated: true,
                notifyStats: false);
            if (productCard == null)
                return false;

            if (!MarketCurrencyService.TrySpend(
                    currencyCard,
                    BuyPrice,
                    BackpackService.Current,
                    CardManager.Instance.AllCards))
            {
                productCard.Stack?.DestroyCard(productCard);
                return false;
            }

            if (!MarketStockLedger.TryConsume(
                    stockSceneData,
                    offerId,
                    CurrentDay,
                    minimumDailyStock,
                    maximumDailyStock))
            {
                productCard.Stack?.DestroyCard(productCard);
                return false;
            }

            RefreshStock(CurrentDay);
            CardManager.Instance.NotifyStatsChanged();
            AudioManager.Instance?.PlaySFX(AudioId.CashRegister);
            SelectionChanged?.Invoke(this);
            return true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            if (InputManager.Instance != null &&
                !InputManager.Instance.IsInputEnabled)
            {
                return;
            }

            MarketCardBuyer.ClearActiveSelection();
            SetSelected(!IsSelected);
        }

        public static void NotifyCardClicked(CardInstance clickedCard)
        {
            if (activeSelection == null || clickedCard == null ||
                clickedCard == activeSelection.GetComponent<CardInstance>())
            {
                return;
            }

            activeSelection.SetSelected(false);
        }

        public static void ClearActiveSelection()
        {
            activeSelection?.SetSelected(false);
        }

        public override (string, string) GetInfo()
        {
            if (Product == null)
                return (string.Empty, string.Empty);

            return (
                Product.DisplayName,
                $"价格：{BuyPrice} 金币\n库存：{StockRemaining}\n点击摊位查看并购买。");
        }

        private int CurrentDay
        {
            get
            {
                int sceneDay = TimeManager.Instance?.CurrentDay ?? 1;
                return GameDirector.Instance?.GameData
                    ?.GetWorldDay(sceneDay) ?? sceneDay;
            }
        }

        private void SetSelected(bool selected)
        {
            if (selected && activeSelection != null &&
                activeSelection != this)
            {
                activeSelection.SetSelected(false);
            }

            activeSelection = selected ? this :
                activeSelection == this ? null : activeSelection;
            GetComponent<CardInstance>()?.SetHighlighted(selected);
            SelectionChanged?.Invoke(selected ? this : null);
        }

        private void RefreshStock(int day)
        {
            if (stockSceneData == null || string.IsNullOrWhiteSpace(offerId))
                return;

            stockState = MarketStockLedger.GetOrRefresh(
                stockSceneData,
                offerId,
                day,
                minimumDailyStock,
                maximumDailyStock);
            GetComponent<CardInstance>()?.UpdatePriceText(
                $"{BuyPrice} / {StockRemaining}");
        }

        private SceneData ResolveStockSceneData()
        {
            if (GameDirector.Instance?.GameData != null)
            {
                GameDirector.Instance.GameData.TryGetScene(out SceneData data);
                return data;
            }

            return new SceneData("Market/Runtime");
        }

        private void SubscribeToDayChanges()
        {
            if (TimeManager.Instance == null)
                return;

            TimeManager.Instance.OnDayStarted -= HandleDayStarted;
            TimeManager.Instance.OnDayStarted += HandleDayStarted;
        }

        private void HandleDayStarted(int day)
        {
            RefreshStock(day);
            if (IsSelected)
                SelectionChanged?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayStarted -= HandleDayStarted;
            if (IsSelected)
                SetSelected(false);
        }
    }
}
