using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class MarketCardBuyer : TradeZone, IPointerClickHandler
    {
        private static readonly Color PendingOutlineColor =
            new(1f, 0.67f, 0.12f, 1f);
        private static MarketCardBuyer activeSelection;

        public static event Action<MarketCardBuyer> SelectionChanged;
        public static MarketCardBuyer ActiveSelection => activeSelection;

        public CardStack PendingStack { get; private set; }
        public int PendingSellValue =>
            MarketTradeRules.CalculateSellValue(PendingStack?.Cards);
        public bool IsSelected => activeSelection == this;

        private CardDefinition currencyCard;

        public void Configure(CardDefinition currency)
        {
            currencyCard = currency;
            base.Initialize(currency, new Vector3(-1.4f, 0f, -0.25f));
            GetComponent<CardInstance>()?.UpdatePriceText("收购");
        }

        public override bool CanTrade(CardStack droppedStack)
        {
            if (currencyCard == null ||
                droppedStack?.Cards == null ||
                droppedStack.Cards.Count == 0)
            {
                return false;
            }

            return droppedStack.Cards.All(card =>
                card != null &&
                MarketTradeRules.CanSell(card.Definition) &&
                (card.Combatant == null || !card.Combatant.IsInCombat) &&
                (!card.TryGetComponent<ChestLogic>(out ChestLogic chest) ||
                    chest.StoredCoins <= 0));
        }

        protected override void ProcessTransaction(CardStack droppedStack)
        {
            if (!CanTrade(droppedStack))
                return;

            MarketProductVendor.ClearActiveSelection();
            if (PendingStack == null ||
                PendingStack.Cards == null ||
                PendingStack.Cards.Count == 0)
            {
                PendingStack = droppedStack;
            }
            else if (PendingStack != droppedStack)
            {
                foreach (CardInstance card in droppedStack.Cards.ToList())
                {
                    droppedStack.RemoveCard(card);
                    PendingStack.AddCard(card);
                }
            }
            PendingStack.SetTargetPosition(spawnPosition);
            RefreshPendingPresentation();
            SetSelected(true);
        }

        public static bool TryFindDropTarget(
            Vector3 worldPosition,
            float attachRadius,
            out MarketCardBuyer target)
        {
            target = null;
            float bestSqrDistance = float.PositiveInfinity;
            float radiusSqr =
                Mathf.Max(0f, attachRadius) *
                Mathf.Max(0f, attachRadius);
            Vector3 flattenedPosition = worldPosition.Flatten();

            foreach (MarketCardBuyer buyer in
                     FindObjectsOfType<MarketCardBuyer>())
            {
                if (buyer == null || !buyer.isActiveAndEnabled)
                    continue;

                float sqrDistance = buyer.GetDropDistanceSqr(
                    flattenedPosition);
                if (sqrDistance > radiusSqr ||
                    sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                target = buyer;
                bestSqrDistance = sqrDistance;
            }

            return target != null;
        }

        private float GetDropDistanceSqr(Vector3 flattenedPosition)
        {
            float bestSqrDistance = (
                transform.position.Flatten() -
                flattenedPosition).sqrMagnitude;
            if (PendingStack?.Cards == null)
                return bestSqrDistance;

            Vector3 anchor = PendingStack.TargetPosition.Flatten();
            for (int index = 0; index < PendingStack.Cards.Count; index++)
            {
                CardInstance card = PendingStack.Cards[index];
                if (card == null)
                    continue;

                Vector3 targetCenter = anchor +
                    card.Settings.StackStep.Flatten() * index;
                bestSqrDistance = Mathf.Min(
                    bestSqrDistance,
                    GetCardFootprintDistanceSqr(
                        flattenedPosition,
                        targetCenter,
                        card.Size));
                bestSqrDistance = Mathf.Min(
                    bestSqrDistance,
                    GetCardFootprintDistanceSqr(
                        flattenedPosition,
                        card.transform.position.Flatten(),
                        card.Size));
            }

            return bestSqrDistance;
        }

        private static float GetCardFootprintDistanceSqr(
            Vector3 flattenedPosition,
            Vector3 cardCenter,
            Vector2 cardSize)
        {
            Vector3 delta = flattenedPosition - cardCenter;
            float outsideX = Mathf.Max(
                0f,
                Mathf.Abs(delta.x) - cardSize.x * 0.5f);
            float outsideZ = Mathf.Max(
                0f,
                Mathf.Abs(delta.z) - cardSize.y * 0.5f);
            return outsideX * outsideX + outsideZ * outsideZ;
        }

        protected override bool WasTransactionHandled(CardStack droppedStack)
        {
            return PendingStack == droppedStack ||
                droppedStack?.Cards == null ||
                droppedStack.Cards.Count == 0;
        }

        public bool ConfirmSale()
        {
            if (!CanTrade(PendingStack) ||
                CardManager.Instance == null ||
                BackpackService.Current == null)
            {
                return false;
            }

            int sellValue = PendingSellValue;
            if (!BackpackService.TryStoreGeneratedCardsDeferred(
                    currencyCard,
                    sellValue,
                    BackpackService.Current))
                return false;

            CardStack soldStack = PendingStack;
            ClearPendingPresentation();
            TradeManager.Instance?.NotifyCardsSold(soldStack);
            soldStack.DestroyAllCards();
            PendingStack = null;
            BackpackService.NotifyContentsChanged();

            AudioManager.Instance?.PlaySFX(AudioId.Coins);
            SetSelected(false);
            return true;
        }

        public void CancelPendingSale()
        {
            ClearPendingPresentation();
            PendingStack = null;
            SetSelected(false);
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

            MarketProductVendor.ClearActiveSelection();
            if (IsSelected)
            {
                CancelPendingSale();
                return;
            }

            SetSelected(true);
        }

        public static void NotifyCardClicked(CardInstance clickedCard)
        {
            if (activeSelection == null || clickedCard == null)
                return;

            if (clickedCard == activeSelection.GetComponent<CardInstance>())
                return;

            if (activeSelection.PendingStack != null &&
                !activeSelection.PendingStack.Cards.Contains(clickedCard) &&
                activeSelection.CanTrade(clickedCard.Stack))
            {
                return;
            }

            activeSelection.CancelPendingSale();
        }

        public static void ClearActiveSelection()
        {
            activeSelection?.CancelPendingSale();
        }

        public override (string, string) GetInfo()
        {
            if (PendingStack != null)
            {
                return (
                    "市场收购台",
                    $"待售 {PendingStack.Cards.Count} 张卡牌\n出售价格：{PendingSellValue} 金币\n请在右侧确认出售。");
            }

            return (
                "市场收购台",
                "把食物、材料、装备或贵重物品拖到这里，确认后金币会进入背包。");
        }

        private void SetSelected(bool selected)
        {
            if (selected && activeSelection != null &&
                activeSelection != this)
            {
                activeSelection.CancelPendingSale();
            }

            activeSelection = selected ? this :
                activeSelection == this ? null : activeSelection;
            GetComponent<CardInstance>()?.SetHighlighted(selected);
            SelectionChanged?.Invoke(selected ? this : null);
        }

        private void RefreshPendingPresentation()
        {
            int count = PendingStack?.Cards?.Count ?? 0;
            GetComponent<CardInstance>()?.UpdatePriceText(
                count > 0 ? $"待售 {count}" : "收购");

            if (PendingStack?.Cards == null)
                return;

            foreach (CardInstance card in PendingStack.Cards)
            {
                if (CanShowPendingOutline(card))
                    card.SetHighlighted(true, PendingOutlineColor);
            }
        }

        private void ClearPendingPresentation()
        {
            GetComponent<CardInstance>()?.UpdatePriceText("收购");
            if (PendingStack?.Cards == null)
                return;

            foreach (CardInstance card in PendingStack.Cards)
            {
                if (CanShowPendingOutline(card))
                    card.SetHighlighted(false);
            }
        }

        private static bool CanShowPendingOutline(CardInstance card)
        {
            return card != null &&
                card.Settings?.OutlineMaterial != null &&
                card.TryGetComponent(out MeshFilter _);
        }

        private void OnDestroy()
        {
            if (IsSelected)
                CancelPendingSale();
        }
    }
}
