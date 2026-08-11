using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class PublicMarketTradeScreen : MonoBehaviour
    {
        [Header("Entry")]
        [SerializeField] private Button marketButton;

        [Header("Modal")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text summaryLabel;

        [Header("Dual inventory")]
        [SerializeField] private RectTransform backpackListRoot;
        [SerializeField] private MarketCommodityListItem backpackRowTemplate;
        [SerializeField] private GameObject backpackEmptyState;
        [SerializeField] private RectTransform marketListRoot;
        [SerializeField] private MarketCommodityListItem marketRowTemplate;
        [SerializeField] private GameObject marketEmptyState;

        [Header("Selection")]
        [SerializeField] private TMP_Text selectionLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button maximumButton;
        [SerializeField] private Button confirmButton;

        private LocationDefinition activeLocation;
        private ResourcesMarketCatalog catalog;
        private MarketService marketService;
        private LocalMarketTradeSession session;
        private MarketTradeDirection direction =
            MarketTradeDirection.PlayerBuys;
        private MarketQuote selectedQuote;
        private bool hasSelectedQuote;
        private int quantity = 1;
        private bool isSubmitting;
        private readonly MarketSubmitGate submitGate = new(0.25f);

        public bool IsOpen => modalRoot != null &&
            modalRoot.activeSelf;
        public LocationDefinition ActiveLocation => activeLocation;

        private void Awake()
        {
            marketButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
            decreaseButton?.onClick.AddListener(DecreaseQuantity);
            increaseButton?.onClick.AddListener(IncreaseQuantity);
            maximumButton?.onClick.AddListener(SelectMaximumQuantity);
            confirmButton?.onClick.AddListener(ConfirmTrade);
            BackpackService.Changed += HandleBackpackChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady +=
                    HandleSceneDataReady;
            }

            HideRowTemplates();
            if (modalRoot != null)
                modalRoot.SetActive(false);
            RefreshAvailability();
        }

        private void Start()
        {
            RefreshAvailability();
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void OnDestroy()
        {
            marketButton?.onClick.RemoveListener(Open);
            closeButton?.onClick.RemoveListener(Close);
            decreaseButton?.onClick.RemoveListener(DecreaseQuantity);
            increaseButton?.onClick.RemoveListener(IncreaseQuantity);
            maximumButton?.onClick.RemoveListener(
                SelectMaximumQuantity);
            confirmButton?.onClick.RemoveListener(ConfirmTrade);
            BackpackService.Changed -= HandleBackpackChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady -=
                    HandleSceneDataReady;
            }
            InputManager.Instance?.RemoveLock(this);
        }

        public void RefreshAvailability()
        {
            activeLocation = ResolveActiveLocation();
            bool available = HasValidLocalMarket(activeLocation);
            if (marketButton != null)
                marketButton.gameObject.SetActive(available);
            if (!available && IsOpen)
                Close();
        }

        public void Open()
        {
            activeLocation = ResolveActiveLocation();
            if (!HasValidLocalMarket(activeLocation))
            {
                SetHint("当前地点没有开放公共市场。");
                return;
            }
            if (!MarketRuntime.TryGet(
                    out catalog,
                    out marketService))
            {
                SetHint("市场服务尚未就绪。");
                return;
            }

            var context = new LocalMarketContext(
                activeLocation.Id,
                activeLocation.DisplayName,
                activeLocation.PublicMarketProfile,
                LocalMarketOpenSource.LocationButton);
            session = new LocalMarketTradeSession(
                context,
                marketService);
            direction = MarketTradeDirection.PlayerBuys;
            hasSelectedQuote = false;
            selectedQuote = default;
            quantity = 1;
            isSubmitting = false;
            submitGate.Reset();
            if (modalRoot != null)
            {
                modalRoot.transform.SetAsLastSibling();
                modalRoot.SetActive(true);
            }
            InputManager.Instance?.AddLock(this);
            RefreshAll();
        }

        public void Close()
        {
            InputManager.Instance?.RemoveLock(this);
            if (modalRoot != null)
                modalRoot.SetActive(false);
            session = null;
            catalog = null;
            marketService = null;
            hasSelectedQuote = false;
            selectedQuote = default;
            quantity = 1;
            isSubmitting = false;
            submitGate.Reset();
        }

        public void DecreaseQuantity()
        {
            quantity = Mathf.Max(1, quantity - 1);
            RefreshSelection();
        }

        public void IncreaseQuantity()
        {
            quantity = Mathf.Min(
                GetMaximumQuantity(),
                quantity + 1);
            RefreshSelection();
        }

        public void SelectMaximumQuantity()
        {
            quantity = Mathf.Max(1, GetMaximumQuantity());
            RefreshSelection();
        }

        public void ConfirmTrade()
        {
            if (isSubmitting ||
                session == null ||
                !hasSelectedQuote)
            {
                return;
            }

            CommodityDefinition commodity =
                catalog?.GetCommodity(selectedQuote.CommodityId);
            if (session.IsQuoteStale(selectedQuote))
            {
                selectedQuote = session.GetQuote(
                    selectedQuote.CommodityId);
                hasSelectedQuote =
                    !string.IsNullOrWhiteSpace(
                        selectedQuote.CommodityId);
                quantity = 1;
                RefreshAll(preserveHint: true);
                SetHint(
                    "市场行情刚刚发生变化，请重新确认。");
                return;
            }

            int maximum = GetMaximumQuantity();
            if (commodity?.CardDefinition == null || maximum <= 0)
            {
                SetHint(GetUnavailableReason());
                RefreshSelection();
                return;
            }
            if (!submitGate.TryEnter(Time.unscaledTime))
            {
                SetHint("操作过快，请确认当前报价后再试。");
                return;
            }

            quantity = Mathf.Clamp(quantity, 1, maximum);
            isSubmitting = true;
            SetButtonsInteractable(false);
            var inventory = new CardBackpackTradeInventory(
                catalog,
                session.MarketProfile.Currency,
                commodity.Id,
                commodity.CardDefinition);
            MarketTradeResult result = session.Execute(
                selectedQuote,
                direction,
                quantity,
                inventory);
            isSubmitting = false;

            if (result.Success)
            {
                if (direction == MarketTradeDirection.PlayerBuys)
                {
                    WorldQuestRuntime.Instance?.ReportMarketPurchase(
                        session.MarketProfile.Id,
                        commodity.Id,
                        quantity);
                }
                else
                {
                    WorldQuestRuntime.Instance?.ReportMarketSale(
                        session.MarketProfile.Id,
                        commodity.Id,
                        quantity);
                }
                SetHint(direction ==
                    MarketTradeDirection.PlayerBuys
                        ? $"购买成功：{commodity.DisplayName} ×{quantity}。"
                        : $"出售成功：{commodity.DisplayName} ×{quantity}。");
                AudioManager.Instance?.PlaySFX(direction ==
                    MarketTradeDirection.PlayerBuys
                        ? AudioId.CashRegister
                        : AudioId.Coins);
            }
            else
            {
                SetHint(DescribeFailure(result.Failure));
            }

            CardManager.Instance?.NotifyStatsChanged();
            selectedQuote = session.GetQuote(commodity.Id);
            hasSelectedQuote =
                !string.IsNullOrWhiteSpace(
                    selectedQuote.CommodityId);
            quantity = 1;
            RefreshAll(preserveHint: true);
        }

        private void RefreshAll(bool preserveHint = false)
        {
            if (session == null)
                return;

            MarketStateData state =
                marketService.GetOrCreateState(
                    session.MarketProfile.Id);
            ReconcileSelectedQuote();
            int coins = MarketCurrencyService.CountAvailable(
                session.MarketProfile.Currency,
                BackpackService.Current,
                CardManager.Instance?.AllCards);
            if (titleLabel != null)
            {
                titleLabel.text =
                    $"{session.Context.LocationDisplayName} · 公共市场";
            }
            if (summaryLabel != null)
            {
                long worldHour =
                    GameDirector.Instance?.GameData?.WorldElapsedHours ??
                    0L;
                long refreshHours = state == null ||
                    state.NextRefreshWorldHour == long.MaxValue
                        ? long.MaxValue
                        : Math.Max(
                            0L,
                            state.NextRefreshWorldHour - worldHour);
                string refreshText = refreshHours == long.MaxValue
                    ? "刷新已停止"
                    : $"{refreshHours}小时后刷新";
                summaryLabel.text =
                    $"金币 {coins}    市场资金 " +
                    $"{state?.AvailableFunds ?? 0}    背包 " +
                    $"{BackpackService.Current?.Count ?? 0}/" +
                    $"{BackpackService.Current?.Capacity ?? 0}    " +
                    refreshText;
            }

            RefreshTradeLists();
            RefreshSelection();
            if (!preserveHint)
            {
                SetHint("点击左侧背包出售，点击右侧市场购买。");
            }
        }

        private void ReconcileSelectedQuote()
        {
            if (!hasSelectedQuote ||
                string.IsNullOrWhiteSpace(
                    selectedQuote.CommodityId))
            {
                return;
            }

            MarketQuote currentQuote = session.GetQuote(
                selectedQuote.CommodityId);
            hasSelectedQuote =
                !string.IsNullOrWhiteSpace(
                    currentQuote.CommodityId);
            selectedQuote = hasSelectedQuote
                ? currentQuote
                : default;
        }

        private void RefreshTradeLists()
        {
            if (backpackListRoot == null ||
                backpackRowTemplate == null ||
                marketListRoot == null ||
                marketRowTemplate == null ||
                session == null)
            {
                return;
            }

            ClearRows(backpackListRoot, backpackRowTemplate);
            ClearRows(marketListRoot, marketRowTemplate);
            int backpackRowCount = 0;
            int marketRowCount = 0;

            foreach (MarketQuote quote in session.GetQuotes())
            {
                MarketQuote capturedQuote = quote;
                CommodityDefinition commodity =
                    catalog?.GetCommodity(quote.CommodityId);
                if (commodity?.CardDefinition == null)
                    continue;

                int owned = CountOwned(commodity);
                bool canBuy = session.AllowsDirection(
                    commodity.Id,
                    MarketTradeDirection.PlayerBuys);
                MarketCommodityListItem marketRow = Instantiate(
                    marketRowTemplate,
                    marketListRoot);
                marketRow.gameObject.SetActive(true);
                marketRowCount++;
                marketRow.Bind(
                    commodity,
                    quote,
                    owned,
                    MarketTradeDirection.PlayerBuys,
                    canBuy,
                    canBuy
                        ? () => SelectQuote(
                            capturedQuote,
                            MarketTradeDirection.PlayerBuys)
                        : null);

                bool canSell = owned > 0 &&
                    session.AllowsDirection(
                        commodity.Id,
                        MarketTradeDirection.PlayerSells);
                if (!canSell)
                    continue;

                MarketCommodityListItem backpackRow = Instantiate(
                    backpackRowTemplate,
                    backpackListRoot);
                backpackRow.gameObject.SetActive(true);
                backpackRowCount++;
                backpackRow.Bind(
                    commodity,
                    quote,
                    owned,
                    MarketTradeDirection.PlayerSells,
                    true,
                    () => SelectQuote(
                        capturedQuote,
                        MarketTradeDirection.PlayerSells));
            }

            backpackEmptyState?.SetActive(backpackRowCount == 0);
            marketEmptyState?.SetActive(marketRowCount == 0);
        }

        private static void ClearRows(
            RectTransform root,
            MarketCommodityListItem template)
        {
            template.gameObject.SetActive(false);
            for (int index = root.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = root.GetChild(index);
                if (child == template.transform)
                    continue;

                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private void HideRowTemplates()
        {
            backpackRowTemplate?.gameObject.SetActive(false);
            marketRowTemplate?.gameObject.SetActive(false);
        }

        private void SelectQuote(
            MarketQuote quote,
            MarketTradeDirection selectedDirection)
        {
            direction = selectedDirection;
            selectedQuote = quote;
            hasSelectedQuote =
                !string.IsNullOrWhiteSpace(quote.CommodityId);
            quantity = 1;
            SetHint(string.Empty);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (!hasSelectedQuote ||
                session == null ||
                catalog == null)
            {
                if (selectionLabel != null)
                {
                    selectionLabel.text =
                        "请选择左侧商品出售\n或选择右侧商品购买";
                }
                if (quantityLabel != null)
                    quantityLabel.text = "数量 —\n交易总额 —";
                SetButtonsInteractable(false);
                return;
            }

            CommodityDefinition commodity =
                catalog.GetCommodity(selectedQuote.CommodityId);
            int maximum = GetMaximumQuantity();
            quantity = maximum > 0
                ? Mathf.Clamp(quantity, 1, maximum)
                : 1;
            int unitPrice = direction ==
                MarketTradeDirection.PlayerBuys
                    ? selectedQuote.PlayerBuyUnitPrice
                    : selectedQuote.PlayerSellUnitPrice;
            int owned = CountOwned(commodity);
            int coins = MarketCurrencyService.CountAvailable(
                session.MarketProfile.Currency,
                BackpackService.Current,
                CardManager.Instance?.AllCards);
            long total = (long)unitPrice * quantity;
            bool playerBuys =
                direction == MarketTradeDirection.PlayerBuys;
            if (selectionLabel != null)
            {
                selectionLabel.text =
                    $"{commodity?.DisplayName ?? selectedQuote.CommodityId}\n" +
                    $"{(playerBuys ? "从当地市场买入" : "出售给当地市场")}\n" +
                    $"{(playerBuys ? "买入单价" : "出售单价")} " +
                    $"{unitPrice} 金币/枚\n" +
                    $"市场库存 {selectedQuote.AvailableStock}    " +
                    $"背包持有 {owned}\n" +
                    $"当前行情：{DescribeTrend(selectedQuote.Trend)}";
            }
            if (quantityLabel != null)
            {
                quantityLabel.text =
                    $"数量 {quantity} / {maximum}\n" +
                    $"{(playerBuys ? "总支出" : "总收入")} {total} 金币\n" +
                    $"交易后  金币 " +
                    $"{(playerBuys ? coins - total : coins + total)}    " +
                    $"持有 {Mathf.Max(0, owned + (playerBuys ? quantity : -quantity))}";
            }

            bool canTrade = maximum > 0 && !isSubmitting;
            if (decreaseButton != null)
                decreaseButton.interactable =
                    canTrade && quantity > 1;
            if (increaseButton != null)
                increaseButton.interactable =
                    canTrade && quantity < maximum;
            if (maximumButton != null)
                maximumButton.interactable =
                    canTrade && quantity != maximum;
            if (confirmButton != null)
            {
                confirmButton.interactable = canTrade;
                TMP_Text label = confirmButton
                    .GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = direction ==
                        MarketTradeDirection.PlayerBuys
                            ? "确认购买"
                            : "确认出售";
                }
            }

            if (!canTrade && !isSubmitting)
                SetHint(GetUnavailableReason());
        }

        private int GetMaximumQuantity()
        {
            if (!hasSelectedQuote ||
                session == null ||
                catalog == null)
            {
                return 0;
            }

            CommodityDefinition commodity =
                catalog.GetCommodity(selectedQuote.CommodityId);
            if (commodity?.CardDefinition == null)
                return 0;

            var inventory = new CardBackpackTradeInventory(
                catalog,
                session.MarketProfile.Currency,
                commodity.Id,
                commodity.CardDefinition);
            return session.GetMaximumQuantity(
                selectedQuote,
                direction,
                inventory);
        }

        private int CountOwned(CommodityDefinition commodity)
        {
            string cardId = commodity?.CardDefinition?.Id;
            return string.IsNullOrWhiteSpace(cardId)
                ? 0
                : BackpackService.Current?.Entries?.Count(entry =>
                    entry?.Card?.Id == cardId) ?? 0;
        }

        private string GetUnavailableReason()
        {
            if (!hasSelectedQuote)
                return "请选择一种商品。";

            CommodityDefinition commodity =
                catalog?.GetCommodity(selectedQuote.CommodityId);
            if (direction == MarketTradeDirection.PlayerBuys)
            {
                if (session?.AllowsDirection(
                        selectedQuote.CommodityId,
                        direction) != true)
                    return "当地市场不向玩家出售这种商品。";
                if (selectedQuote.AvailableStock <= 0)
                    return "这种商品当前已经售罄。";
                return "金币不足或背包无法接收这种商品。";
            }

            if (session?.AllowsDirection(
                    selectedQuote.CommodityId,
                    direction) != true)
                return "当地市场不收购这种商品。";
            if (CountOwned(commodity) <= 0)
                return "背包中没有这种商品。";
            if (selectedQuote.MarketAffordableQuantity <= 0)
                return "市场暂时没有足够资金收购。";
            return "市场仓库已满，暂时停止收购。";
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (decreaseButton != null)
                decreaseButton.interactable = interactable;
            if (increaseButton != null)
                increaseButton.interactable = interactable;
            if (maximumButton != null)
                maximumButton.interactable = interactable;
            if (confirmButton != null)
                confirmButton.interactable = interactable;
        }

        private void SetHint(string message)
        {
            if (hintLabel != null)
                hintLabel.text = message ?? string.Empty;
        }

        private void HandleBackpackChanged()
        {
            if (IsOpen && !isSubmitting)
                RefreshAll(preserveHint: true);
        }

        private void HandleSceneLoaded(
            Scene _,
            LoadSceneMode __)
        {
            RefreshAvailability();
        }

        private void HandleSceneDataReady(
            SceneData _,
            bool __)
        {
            RefreshAvailability();
        }

        private static LocationDefinition ResolveActiveLocation()
        {
            LocationSceneController controller =
                FindObjectOfType<LocationSceneController>(true);
            if (controller == null)
                return null;
            if (controller.ActiveDefinition != null)
                return controller.ActiveDefinition;

            string locationId =
                GameDirector.Instance?.GameData?.ActiveLocationId;
            return Resources
                .LoadAll<LocationDefinition>("Locations")
                .FirstOrDefault(definition =>
                    definition != null &&
                    definition.Id == locationId);
        }

        private static bool HasValidLocalMarket(
            LocationDefinition location)
        {
            return location?.PublicMarketProfile != null &&
                string.Equals(
                    location.Id,
                    location.PublicMarketProfile.LocationId,
                    StringComparison.Ordinal);
        }

        private static string DescribeFailure(
            MarketTradeFailure failure)
        {
            return failure switch
            {
                MarketTradeFailure.StaleQuote =>
                    "市场行情刚刚发生变化，请重新确认。",
                MarketTradeFailure.InsufficientMarketStock =>
                    "市场库存不足。",
                MarketTradeFailure.InsufficientMarketFunds =>
                    "市场没有足够资金收购。",
                MarketTradeFailure.InsufficientPlayerCurrency =>
                    "金币不足。",
                MarketTradeFailure.InsufficientPlayerGoods =>
                    "背包中的商品数量不足。",
                MarketTradeFailure.InventoryCannotReceive =>
                    "背包无法接收这些商品。",
                MarketTradeFailure.MarketFundsCapacityReached =>
                    "市场资金已达到上限。",
                MarketTradeFailure.MarketStockCapacityReached =>
                    "市场仓库已满。",
                MarketTradeFailure.DirectionClosed =>
                    "当地市场没有开放这项交易。",
                _ => "交易没有完成，资产未发生变化。"
            };
        }

        private static string DescribeTrend(MarketTrend trend)
        {
            return trend switch
            {
                MarketTrend.Abundant => "盛产·价格偏低",
                MarketTrend.Shortage => "短缺·价格偏高",
                MarketTrend.Emergency => "紧急·价格高涨",
                _ => "供需平稳"
            };
        }
    }
}
