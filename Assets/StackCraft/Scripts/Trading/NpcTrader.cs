using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class NpcTrader : MonoBehaviour, IPointerClickHandler
    {
        private static NpcTrader activeSelection;
        private readonly List<LocationMarketOffer> sellOffers = new();

        public static event Action<NpcTrader> SelectionChanged;
        public static NpcTrader ActiveSelection => activeSelection;

        public CardInstance Card { get; private set; }
        public ResolvedNpcTradeProfile Profile { get; private set; }
        public IReadOnlyList<LocationMarketOffer> SellOffers => sellOffers;
        public IReadOnlyList<NpcTradeStockData> AcquiredStock =>
            NpcTradeService.GetAcquiredStock(this);
        public CardDefinition Currency { get; private set; }
        public CardInstance PickupCard { get; private set; }
        public bool IsSelected => activeSelection == this;
        public int AvailableFunds => NpcTradeService.GetAvailableFunds(this);
        public CardStack PendingWorldSale { get; private set; }
        public string LastMessage { get; private set; }
        public string TradeStateId =>
            !string.IsNullOrWhiteSpace(Card?.PersistentId)
                ? Card.PersistentId
                : Card?.Definition?.Id;

        public void Configure(CardInstance card)
        {
            Configure(card, null, null, null);
        }

        public void Configure(
            CardInstance card,
            IEnumerable<LocationMarketOffer> offers,
            CardDefinition currency,
            CardInstance pickupCard)
        {
            Configure(card, offers, currency, pickupCard, null);
        }

        public void Configure(
            CardInstance card,
            IEnumerable<LocationMarketOffer> offers,
            CardDefinition currency,
            CardInstance pickupCard,
            NpcTradeProfile explicitProfile)
        {
            Card = card;
            Profile = NpcTradeProfileResolver.Resolve(
                card?.Definition,
                explicitProfile);
            Currency = currency;
            PickupCard = pickupCard;
            sellOffers.Clear();
            if (offers != null)
            {
                sellOffers.AddRange(offers.Where(offer =>
                    offer.ProductDefinition != null));
            }

            NpcTradeService.EnsureState(this);
        }

        public bool CanBuy(CardDefinition definition)
        {
            return definition != null &&
                Profile != null &&
                Profile.CanBuyDefinition(definition) &&
                MarketTradeRules.CanSell(definition);
        }

        public int GetPlayerBuyPrice(LocationMarketOffer offer)
        {
            if (NpcTradeService.TryGetMarketQuote(
                    this,
                    offer.ProductDefinition,
                    out MarketQuote quote))
            {
                return quote.PlayerBuyUnitPrice;
            }

            return Profile?.CalculatePlayerBuyPrice(offer.BuyPrice) ??
                offer.BuyPrice;
        }

        public int GetPlayerSellPrice(CardDefinition definition)
        {
            if (NpcTradeService.TryGetMarketQuote(
                    this,
                    definition,
                    out MarketQuote quote))
            {
                return quote.PlayerSellUnitPrice;
            }

            return Profile?.CalculatePlayerSellPrice(
                definition?.SellPrice ?? 0) ?? 0;
        }

        public int GetAcquiredBuybackPrice(CardDefinition definition)
        {
            int sellPrice = GetPlayerSellPrice(definition);
            int markedUpPrice = Profile?.CalculatePlayerBuyPrice(
                Mathf.CeilToInt((definition?.SellPrice ?? 0) * 1.5f)) ?? 0;
            return Mathf.Max(sellPrice + 1, markedUpPrice);
        }

        public int GetStock(LocationMarketOffer offer)
        {
            return NpcTradeService.GetStock(this, offer);
        }

        public bool TryPurchase(LocationMarketOffer offer, out string reason)
        {
            return NpcTradeService.TryPurchase(this, offer, out reason);
        }

        public bool TryPurchase(
            LocationMarketOffer offer,
            MarketQuote expectedQuote,
            out string reason)
        {
            return NpcTradeService.TryPurchase(
                this,
                offer,
                expectedQuote,
                out reason);
        }

        public bool TryPurchaseAcquired(
            string productId,
            out string reason)
        {
            return NpcTradeService.TryPurchaseAcquired(
                this,
                productId,
                out reason);
        }

        public void SelectForInteraction()
        {
            LastMessage = string.Empty;
            SetSelected(true);
        }

        public bool TrySellFromBackpack(
            string productId,
            int count,
            out string reason)
        {
            return NpcTradeService.TrySellFromBackpack(
                this,
                productId,
                count,
                out reason);
        }

        public bool TrySellFromBackpack(
            string productId,
            int count,
            MarketQuote expectedQuote,
            out string reason)
        {
            return NpcTradeService.TrySellFromBackpack(
                this,
                productId,
                count,
                expectedQuote,
                out reason);
        }

        public bool PreviewWorldSale(CardStack stack, out string reason)
        {
            reason = string.Empty;
            if (stack?.Cards == null || stack.Cards.Count == 0 ||
                stack.Cards.Any(card => card == null ||
                    !CanBuy(card.Definition) ||
                    (card.Combatant != null && card.Combatant.IsInCombat)))
            {
                reason = Profile?.CanBuyCategory(
                    stack?.TopCard?.Definition?.Category ?? CardCategory.None) ==
                    true
                    ? "该物品当前无法出售。"
                    : Profile?.RefusalText ?? "这个人物不收购该物品。";
                LastMessage = reason;
                SetSelected(true);
                return false;
            }

            PendingWorldSale = stack;
            LastMessage = "请在右侧确认出售，物品不会立即消失。";
            SetSelected(true);
            return true;
        }

        public bool ConfirmWorldSale(out string reason)
        {
            bool result = NpcTradeService.TrySellWorldStack(
                this,
                PendingWorldSale,
                out reason);
            if (result)
                PendingWorldSale = null;
            LastMessage = reason;
            return result;
        }

        public static bool TryFindDropTarget(
            CardInstance droppedCard,
            float radius,
            out NpcTrader trader)
        {
            trader = null;
            if (droppedCard?.Stack == null)
                return false;

            float bestDistance = Mathf.Max(0.1f, radius);
            foreach (NpcTrader candidate in FindObjectsOfType<NpcTrader>())
            {
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    candidate.Card == droppedCard)
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    candidate.transform.position.Flatten(),
                    droppedCard.transform.position.Flatten());
                if (distance > bestDistance)
                    continue;

                trader = candidate;
                bestDistance = distance;
            }

            return trader != null;
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

            LastMessage = string.Empty;
            PendingWorldSale = null;
            SetSelected(!IsSelected);
        }

        public static void NotifyCardClicked(CardInstance clickedCard)
        {
            if (activeSelection == null || clickedCard == null ||
                clickedCard == activeSelection.Card)
            {
                return;
            }

            activeSelection.SetSelected(false);
        }

        public static void ClearActiveSelection()
        {
            activeSelection?.SetSelected(false);
        }

        private void SetSelected(bool selected)
        {
            if (selected && activeSelection != null &&
                activeSelection != this)
            {
                activeSelection.SetSelected(false);
            }

            MarketProductVendor.ClearActiveSelection();
            MarketCardBuyer.ClearActiveSelection();
            activeSelection = selected ? this :
                activeSelection == this ? null : activeSelection;
            if (!selected)
                PendingWorldSale = null;
            Card?.SetHighlighted(selected);
            SelectionChanged?.Invoke(selected ? this : null);
        }

        private void OnDestroy()
        {
            if (IsSelected)
                SetSelected(false);
        }
    }
}
