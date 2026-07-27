using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class WorldMapLocationView : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Toggle locationToggle;
        [SerializeField] private Toggle questsToggle;

        [Header("Location")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private RawImage artImage;
        [SerializeField] private TMP_Text typeAndDangerLabel;
        [SerializeField] private TMP_Text discoveryLabel;
        [SerializeField] private TMP_Text travelTimeLabel;
        [SerializeField] private TMP_Text resourcesLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Button enterLocationButton;

        [Header("NPC Trade")]
        [SerializeField] private GameObject npcTradePanel;
        [SerializeField] private Button npcBuyTabButton;
        [SerializeField] private Button npcSellTabButton;
        [SerializeField] private Button npcTalkTabButton;
        [SerializeField] private RectTransform npcTradeListRoot;
        [SerializeField] private NpcTradeListRowView npcTradeRowTemplate;
        [SerializeField] private TMP_Text npcTradeHint;

        private CanvasGroup canvasGroup;
        private NpcTradeTab npcTradeTab;
        private string pendingSellProductId;
        private int pendingSellCount;
        private bool pendingWorldSale;

        public WorldMapLocation SelectedLocation { get; private set; }
        public LocationEntrance SelectedBuilding { get; private set; }
        public MarketProductVendor SelectedMarketOffer { get; private set; }
        public MarketCardBuyer SelectedMarketBuyer { get; private set; }
        public NpcTrader SelectedNpcTrader { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            locationToggle?.onValueChanged.AddListener(ToggleView);
            enterLocationButton?.onClick.AddListener(PerformLocationAction);
            npcBuyTabButton?.onClick.AddListener(ShowNpcBuyList);
            npcSellTabButton?.onClick.AddListener(ShowNpcSellList);
            npcTalkTabButton?.onClick.AddListener(ShowNpcTalk);
            WorldMapLocation.SelectionChanged += HandleSelectionChanged;
            LocationEntrance.SelectionChanged += HandleBuildingSelectionChanged;
            MarketProductVendor.SelectionChanged +=
                HandleMarketOfferSelectionChanged;
            MarketCardBuyer.SelectionChanged +=
                HandleMarketBuyerSelectionChanged;
            NpcTrader.SelectionChanged += HandleNpcTraderSelectionChanged;
            WorldMapBootstrap.PartyMapStateChanged += HandlePartyMapStateChanged;
            BackpackService.Changed += HandleMarketFundsChanged;
            if (CardManager.Instance != null)
                CardManager.Instance.OnStatsChanged += HandleMarketStatsChanged;

            if (NpcTrader.ActiveSelection != null)
                ShowNpcTrader(NpcTrader.ActiveSelection);
            else if (MarketProductVendor.ActiveSelection != null)
                ShowMarketOffer(MarketProductVendor.ActiveSelection);
            else if (MarketCardBuyer.ActiveSelection != null)
                ShowMarketBuyer(MarketCardBuyer.ActiveSelection);
            else if (LocationEntrance.ActiveSelection != null)
                ShowBuilding(LocationEntrance.ActiveSelection);
            else if (WorldMapLocation.ActiveSelection != null)
                ShowLocation(WorldMapLocation.ActiveSelection);
            else
            {
                ShowEmptyState();
                ToggleView(false);
            }
        }

        private void OnDestroy()
        {
            WorldMapLocation.SelectionChanged -= HandleSelectionChanged;
            LocationEntrance.SelectionChanged -= HandleBuildingSelectionChanged;
            MarketProductVendor.SelectionChanged -=
                HandleMarketOfferSelectionChanged;
            MarketCardBuyer.SelectionChanged -=
                HandleMarketBuyerSelectionChanged;
            NpcTrader.SelectionChanged -= HandleNpcTraderSelectionChanged;
            WorldMapBootstrap.PartyMapStateChanged -= HandlePartyMapStateChanged;
            BackpackService.Changed -= HandleMarketFundsChanged;
            if (CardManager.Instance != null)
                CardManager.Instance.OnStatsChanged -= HandleMarketStatsChanged;
            locationToggle?.onValueChanged.RemoveListener(ToggleView);
            enterLocationButton?.onClick.RemoveListener(PerformLocationAction);
            npcBuyTabButton?.onClick.RemoveListener(ShowNpcBuyList);
            npcSellTabButton?.onClick.RemoveListener(ShowNpcSellList);
            npcTalkTabButton?.onClick.RemoveListener(ShowNpcTalk);
        }

        public void ShowLocation(WorldMapLocation location)
        {
            if (location == null || location.Card == null)
                return;

            SelectedLocation = location;
            SelectedBuilding = null;
            SelectedMarketOffer = null;
            SelectedMarketBuyer = null;
            SelectedNpcTrader = null;
            SetNpcTradePanelVisible(false);
            SetLocationTabLabel("地点");
            WorldMapLocationDetails details = location.Details ??
                WorldMapLocationDetails.CreateFallback(location.Card.Definition);

            titleLabel.text = location.Card.Definition != null
                ? location.Card.Definition.DisplayName
                : location.Card.gameObject.name;
            artImage.texture = location.Card.Definition?.ArtTexture;
            artImage.enabled = artImage.texture != null;
            typeAndDangerLabel.text = $"{details.locationType} · 危险 {details.dangerLevel}";
            discoveryLabel.text = "● 已发现";
            travelTimeLabel.text = $"旅行时间    {details.travelTime}";
            resourcesLabel.text = "可能资源\n" + string.Join(
                "\n",
                (details.possibleResources ?? Enumerable.Empty<string>())
                    .Where(resource => !string.IsNullOrWhiteSpace(resource))
                    .Select(resource => $"• {resource}"));
            descriptionLabel.text = string.IsNullOrWhiteSpace(details.description)
                ? location.Card.Definition?.Description ?? string.Empty
                : details.description;
            RefreshLocationAction();

            if (locationToggle != null)
            {
                locationToggle.interactable = true;
                locationToggle.isOn = true;
            }
            else
                ToggleView(true);
        }

        public void ShowBuilding(LocationEntrance building)
        {
            if (building == null || building.Card == null)
                return;

            SelectedBuilding = building;
            SelectedLocation = null;
            SelectedMarketOffer = null;
            SelectedMarketBuyer = null;
            SelectedNpcTrader = null;
            SetNpcTradePanelVisible(false);
            SetLocationTabLabel("建筑");

            CardDefinition definition = building.Card.Definition;
            string displayName = definition != null
                ? definition.DisplayName
                : building.Card.gameObject.name;
            titleLabel.text = displayName;
            artImage.texture = definition?.ArtTexture;
            artImage.enabled = artImage.texture != null;
            typeAndDangerLabel.text = "建筑 · 可进入";
            discoveryLabel.text = "● 已开放";
            travelTimeLabel.text = building.Occupant == null
                ? "人物槽    空"
                : $"人物槽    {building.Occupant.Definition?.DisplayName ?? "已占用"}";
            resourcesLabel.text = "可用服务\n• 进入建筑";
            descriptionLabel.text = definition?.Description ?? string.Empty;
            RefreshLocationAction();

            if (locationToggle != null)
            {
                locationToggle.interactable = true;
                locationToggle.isOn = true;
            }
            else
                ToggleView(true);
        }

        public void ShowMarketOffer(MarketProductVendor vendor)
        {
            if (vendor?.Product == null)
                return;

            SelectedMarketOffer = vendor;
            SelectedMarketBuyer = null;
            SelectedLocation = null;
            SelectedBuilding = null;
            SelectedNpcTrader = null;
            SetNpcTradePanelVisible(false);
            SetLocationTabLabel("商品");

            CardDefinition product = vendor.Product;
            titleLabel.text = product.DisplayName;
            artImage.texture = product.ArtTexture;
            artImage.enabled = artImage.texture != null;
            typeAndDangerLabel.text =
                $"市场商品 · {GetCategoryLabel(product.Category)}";
            discoveryLabel.text = vendor.StockRemaining > 0
                ? $"● 今日库存 {vendor.StockRemaining}"
                : "● 今日售罄";
            travelTimeLabel.text = $"购买价格    {vendor.BuyPrice} 金币";
            resourcesLabel.text =
                "购买方式\n• 点击下方购买按钮\n• 金币从背包与桌面扣除";
            descriptionLabel.text = product.Description ?? string.Empty;
            RefreshLocationAction();

            if (locationToggle != null)
            {
                locationToggle.interactable = true;
                locationToggle.isOn = true;
            }
            ToggleView(true);
        }

        public void ShowMarketBuyer(MarketCardBuyer buyer)
        {
            if (buyer == null)
                return;

            SelectedMarketBuyer = buyer;
            SelectedMarketOffer = null;
            SelectedLocation = null;
            SelectedBuilding = null;
            SelectedNpcTrader = null;
            SetNpcTradePanelVisible(false);
            SetLocationTabLabel("收购");

            CardDefinition definition =
                buyer.GetComponent<CardInstance>()?.Definition;
            titleLabel.text = definition?.DisplayName ?? "收购柜台";
            artImage.texture = definition?.ArtTexture;
            artImage.enabled = artImage.texture != null;
            typeAndDangerLabel.text = "市场服务 · 物品收购";
            discoveryLabel.text = buyer.PendingStack == null
                ? "● 等待物品"
                : $"● 待售 {buyer.PendingStack.Cards.Count} 张";
            travelTimeLabel.text = buyer.PendingStack == null
                ? "出售价格    —"
                : $"出售价格    {buyer.PendingSellValue} 金币";
            resourcesLabel.text =
                "出售方式\n• 把物品拖到收购柜台\n• 查看价格后确认出售";
            descriptionLabel.text = buyer.PendingStack == null
                ? "收购食物、材料、装备和贵重物品。"
                : "确认后物品会消失，金币直接进入背包。";
            RefreshLocationAction();

            if (locationToggle != null)
            {
                locationToggle.interactable = true;
                locationToggle.isOn = true;
            }
            ToggleView(true);
        }

        public void ToggleView(bool show)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
        }

        public void ShowNpcTrader(NpcTrader trader)
        {
            if (trader?.Card?.Definition == null)
                return;

            SelectedNpcTrader = trader;
            SelectedLocation = null;
            SelectedBuilding = null;
            SelectedMarketOffer = null;
            SelectedMarketBuyer = null;
            pendingSellProductId = null;
            pendingSellCount = 0;
            pendingWorldSale = trader.PendingWorldSale != null;
            SetLocationTabLabel("人物");

            CardDefinition definition = trader.Card.Definition;
            titleLabel.text = definition.DisplayName;
            artImage.texture = definition.ArtTexture;
            artImage.enabled = artImage.texture != null;
            typeAndDangerLabel.text =
                $"{trader.Profile?.RoleLabel ?? "居民"} · 可交易";
            discoveryLabel.text =
                $"● 可用资金 {trader.AvailableFunds} 金币";
            travelTimeLabel.text = string.Empty;
            resourcesLabel.text = string.Empty;
            descriptionLabel.text = definition.Description ?? string.Empty;
            SetNpcTradePanelVisible(true);

            npcTradeTab = pendingWorldSale ||
                !string.IsNullOrWhiteSpace(trader.LastMessage) ||
                trader.SellOffers.Count == 0
                ? NpcTradeTab.Sell
                : NpcTradeTab.Buy;
            RefreshNpcTradeView();

            if (locationToggle != null)
            {
                locationToggle.interactable = true;
                locationToggle.isOn = true;
            }
            ToggleView(true);
        }

        public void ShowNpcBuyList()
        {
            npcTradeTab = NpcTradeTab.Buy;
            pendingSellProductId = null;
            pendingSellCount = 0;
            pendingWorldSale = false;
            RefreshNpcTradeView();
        }

        public void ShowNpcSellList()
        {
            npcTradeTab = NpcTradeTab.Sell;
            pendingWorldSale = SelectedNpcTrader?.PendingWorldSale != null;
            RefreshNpcTradeView();
        }

        public void ShowNpcTalk()
        {
            npcTradeTab = NpcTradeTab.Talk;
            pendingSellProductId = null;
            pendingSellCount = 0;
            pendingWorldSale = false;
            RefreshNpcTradeView();
        }

        private void HandleSelectionChanged(WorldMapLocation location)
        {
            if (location != null)
            {
                ShowLocation(location);
                return;
            }

            SelectedLocation = null;
            ShowEmptyState();
            if (locationToggle != null && locationToggle.isOn && questsToggle != null)
                questsToggle.isOn = true;
            else
                ToggleView(false);
        }

        private void HandleBuildingSelectionChanged(LocationEntrance building)
        {
            if (building != null)
            {
                ShowBuilding(building);
                return;
            }

            if (SelectedBuilding == null)
                return;

            SelectedBuilding = null;
            ShowEmptyState();
            if (locationToggle != null && locationToggle.isOn && questsToggle != null)
                questsToggle.isOn = true;
            else
                ToggleView(false);
        }

        private void HandlePartyMapStateChanged()
        {
            RefreshLocationAction();
        }

        private void HandleMarketFundsChanged()
        {
            if (SelectedMarketOffer != null)
                ShowMarketOffer(SelectedMarketOffer);
            else if (SelectedNpcTrader != null)
                RefreshNpcTradeView();
        }

        private void HandleMarketStatsChanged(StatsSnapshot _)
        {
            HandleMarketFundsChanged();
        }

        private void HandleMarketOfferSelectionChanged(
            MarketProductVendor vendor)
        {
            if (vendor != null)
            {
                ShowMarketOffer(vendor);
                return;
            }

            if (SelectedMarketOffer == null)
                return;

            ShowEmptyState();
            ToggleView(false);
        }

        private void HandleMarketBuyerSelectionChanged(
            MarketCardBuyer buyer)
        {
            if (buyer != null)
            {
                ShowMarketBuyer(buyer);
                return;
            }

            if (SelectedMarketBuyer == null)
                return;

            ShowEmptyState();
            ToggleView(false);
        }

        private void HandleNpcTraderSelectionChanged(NpcTrader trader)
        {
            if (trader != null)
            {
                ShowNpcTrader(trader);
                return;
            }

            if (SelectedNpcTrader == null)
                return;

            ShowEmptyState();
            ToggleView(false);
        }

        private void PerformLocationAction()
        {
            if (SelectedNpcTrader != null)
            {
                ConfirmNpcSale();
                return;
            }

            if (SelectedMarketOffer != null)
            {
                SelectedMarketOffer.TryPurchase();
                ShowMarketOffer(SelectedMarketOffer);
                return;
            }

            if (SelectedMarketBuyer != null)
            {
                MarketCardBuyer buyer = SelectedMarketBuyer;
                if (!buyer.ConfirmSale())
                    ShowMarketBuyer(buyer);
                return;
            }

            if (SelectedBuilding != null)
            {
                SelectedBuilding.TryEnter();
                RefreshLocationAction();
                return;
            }

            if (SelectedLocation == null)
                return;

            WorldMapBootstrap worldMap = WorldMapBootstrap.Instance;
            if (worldMap == null)
                return;

            if (worldMap.IsPartyAtLocation(SelectedLocation.Index))
                worldMap.TryEnterPartyLocation(SelectedLocation.Index);
            else
                worldMap.TryTravelPartyTo(SelectedLocation.Index);

            RefreshLocationAction();
        }

        private void RefreshLocationAction()
        {
            if (enterLocationButton == null)
                return;

            TMP_Text actionLabel = enterLocationButton.GetComponentInChildren<TMP_Text>(true);
            if (SelectedNpcTrader != null)
            {
                bool canConfirm = npcTradeTab == NpcTradeTab.Sell &&
                    (pendingWorldSale ||
                        (!string.IsNullOrWhiteSpace(pendingSellProductId) &&
                         pendingSellCount > 0));
                if (actionLabel != null)
                {
                    CardDefinition pendingDefinition =
                        string.IsNullOrWhiteSpace(pendingSellProductId)
                            ? null
                            : CardManager.Instance?.GetDefinitionById(
                                pendingSellProductId);
                    int pendingValue =
                        (SelectedNpcTrader?.GetPlayerSellPrice(
                            pendingDefinition) ?? 0) *
                        pendingSellCount;
                    actionLabel.text = canConfirm
                        ? pendingWorldSale
                            ? "确认出售桌面物品"
                            : $"确认出售 {pendingDefinition?.DisplayName ?? "物品"}" +
                              $" ×{pendingSellCount}（{pendingValue} 金币）"
                        : "请选择要出售的物品";
                }
                enterLocationButton.gameObject.SetActive(
                    npcTradeTab == NpcTradeTab.Sell);
                enterLocationButton.interactable = canConfirm;
                return;
            }

            enterLocationButton.gameObject.SetActive(true);
            if (SelectedMarketOffer != null)
            {
                if (actionLabel != null)
                {
                    actionLabel.text = SelectedMarketOffer.StockRemaining <= 0
                        ? "今日售罄"
                        : SelectedMarketOffer.CanPurchase
                            ? $"购买（{SelectedMarketOffer.BuyPrice} 金币）"
                            : $"金币不足（需要 {SelectedMarketOffer.BuyPrice}）";
                }
                enterLocationButton.interactable =
                    SelectedMarketOffer.CanPurchase;
                return;
            }

            if (SelectedMarketBuyer != null)
            {
                if (actionLabel != null)
                {
                    actionLabel.text = SelectedMarketBuyer.PendingStack == null
                        ? "请先放入物品"
                        : $"确认出售（{SelectedMarketBuyer.PendingSellValue} 金币）";
                }
                enterLocationButton.interactable =
                    SelectedMarketBuyer.PendingStack != null;
                return;
            }

            if (SelectedBuilding != null)
            {
                string buildingName = SelectedBuilding.Card?.Definition?.DisplayName ?? "建筑";
                if (actionLabel != null)
                {
                    actionLabel.text = SelectedBuilding.CanEnter
                        ? $"进入{buildingName}"
                        : "请先放入人物";
                }

                enterLocationButton.interactable = SelectedBuilding.CanEnter;
                return;
            }

            WorldMapBootstrap worldMap = WorldMapBootstrap.Instance;
            if (SelectedLocation == null || worldMap == null)
            {
                if (actionLabel != null)
                    actionLabel.text = "进入地点";
                enterLocationButton.interactable = false;
                return;
            }

            bool isCurrentLocation = worldMap.IsPartyAtLocation(SelectedLocation.Index);
            bool localMapImplemented = worldMap.IsLocationMapImplemented(SelectedLocation.Index);
            if (actionLabel != null)
            {
                actionLabel.text = isCurrentLocation
                    ? localMapImplemented
                        ? "进入地点"
                        : "地点地图开发中"
                    : worldMap.IsPartyTraveling
                        ? "旅行中…"
                        : "旅行到这个地点";
            }

            enterLocationButton.interactable = worldMap.CanEnterPartyLocation(SelectedLocation.Index) ||
                worldMap.CanTravelPartyTo(SelectedLocation.Index);
        }

        private void ShowEmptyState()
        {
            SelectedLocation = null;
            SelectedBuilding = null;
            SelectedMarketOffer = null;
            SelectedMarketBuyer = null;
            SelectedNpcTrader = null;
            SetNpcTradePanelVisible(false);
            locationToggle.interactable = false;
            RefreshLocationAction();
            titleLabel.text = "请选择地点";
            artImage.texture = null;
            artImage.enabled = false;
            typeAndDangerLabel.text = string.Empty;
            discoveryLabel.text = string.Empty;
            travelTimeLabel.text = string.Empty;
            resourcesLabel.text = string.Empty;
            descriptionLabel.text = "点选世界地图上的地点卡以查看详情。";
        }

        private void RefreshNpcTradeView()
        {
            if (SelectedNpcTrader == null || npcTradeListRoot == null ||
                npcTradeRowTemplate == null)
            {
                return;
            }

            foreach (Transform child in npcTradeListRoot)
            {
                if (child != npcTradeRowTemplate.transform)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            if (npcBuyTabButton != null)
                npcBuyTabButton.interactable = npcTradeTab != NpcTradeTab.Buy;
            if (npcSellTabButton != null)
                npcSellTabButton.interactable = npcTradeTab != NpcTradeTab.Sell;
            if (npcTalkTabButton != null)
                npcTalkTabButton.interactable = npcTradeTab != NpcTradeTab.Talk;
            enterLocationButton.gameObject.SetActive(
                npcTradeTab == NpcTradeTab.Sell);

            switch (npcTradeTab)
            {
                case NpcTradeTab.Buy:
                    PopulateNpcBuyList();
                    break;
                case NpcTradeTab.Sell:
                    PopulateNpcSellList();
                    break;
                default:
                    PopulateNpcTalk();
                    break;
            }

            discoveryLabel.text =
                $"● 可用资金 {SelectedNpcTrader.AvailableFunds} 金币";
            RefreshLocationAction();
        }

        private void PopulateNpcBuyList()
        {
            int count = 0;
            foreach (LocationMarketOffer offer in
                     SelectedNpcTrader.SellOffers)
            {
                LocationMarketOffer capturedOffer = offer;
                int stock = SelectedNpcTrader.GetStock(capturedOffer);
                int price =
                    SelectedNpcTrader.GetPlayerBuyPrice(capturedOffer);
                NpcTradeListRowView row = CreateNpcTradeRow();
                row.Bind(
                    capturedOffer.ProductDefinition,
                    $"{capturedOffer.ProductDefinition.DisplayName}\n" +
                    $"{price} 金币 · 库存 {stock}",
                    stock > 0 ? "购买" : null,
                    stock > 0
                        ? () =>
                        {
                            bool purchased = SelectedNpcTrader.TryPurchase(
                                capturedOffer,
                                out string reason);
                            RefreshNpcTradeView();
                            npcTradeHint.text = purchased
                                ? "购买成功，商品已放入背包。"
                                : reason;
                        }
                        : null);
                count++;
            }

            foreach (NpcTradeStockData acquired in
                     SelectedNpcTrader.AcquiredStock)
            {
                CardDefinition definition =
                    CardManager.Instance?.GetDefinitionById(
                        acquired.ProductId);
                if (definition == null || acquired.Remaining <= 0)
                    continue;

                string productId = acquired.ProductId;
                int price =
                    SelectedNpcTrader.GetAcquiredBuybackPrice(definition);
                NpcTradeListRowView row = CreateNpcTradeRow();
                row.Bind(
                    definition,
                    $"{definition.DisplayName}\n" +
                    $"{price} 金币 · 个人库存 {acquired.Remaining}",
                    "买回",
                    () =>
                    {
                        bool purchased =
                            SelectedNpcTrader.TryPurchaseAcquired(
                                productId,
                                out string reason);
                        RefreshNpcTradeView();
                        npcTradeHint.text = purchased
                            ? "买回成功，商品已放入背包。"
                            : reason;
                    });
                count++;
            }

            npcTradeHint.text = count == 0
                ? "这个人物今天没有出售商品。"
                : "购买后，商品会直接放入背包。";
        }

        private void PopulateNpcSellList()
        {
            bool hasTraderMessage = !string.IsNullOrWhiteSpace(
                SelectedNpcTrader.LastMessage);
            if (hasTraderMessage)
                npcTradeHint.text = SelectedNpcTrader.LastMessage;

            if (pendingWorldSale &&
                SelectedNpcTrader.PendingWorldSale?.Cards != null)
            {
                var worldCards = SelectedNpcTrader.PendingWorldSale.Cards;
                int value = worldCards.Sum(card =>
                    SelectedNpcTrader.GetPlayerSellPrice(card.Definition));
                NpcTradeListRowView pendingRow = CreateNpcTradeRow();
                pendingRow.Bind(
                    worldCards[0].Definition,
                    $"桌面待售 {worldCards.Count} 张\n可得 {value} 金币",
                    null,
                    null);
                npcTradeHint.text = string.IsNullOrWhiteSpace(
                    SelectedNpcTrader.LastMessage)
                    ? "物品已返回原位，确认后才会出售。"
                    : SelectedNpcTrader.LastMessage;
            }

            BackpackData backpack = BackpackService.Current;
            var groups = backpack?.Entries?
                .Where(entry => entry?.Card != null)
                .GroupBy(entry => entry.Card.Id)
                .ToList();
            int count = 0;
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    CardDefinition definition =
                        CardManager.Instance?.GetDefinitionById(group.Key);
                    if (!SelectedNpcTrader.CanBuy(definition))
                        continue;

                    string productId = group.Key;
                    int owned = group.Count();
                    int unitPrice =
                        SelectedNpcTrader.GetPlayerSellPrice(definition);
                    NpcTradeListRowView row = CreateNpcTradeRow();
                    row.Bind(
                        definition,
                        $"{definition.DisplayName}\n持有 {owned} · 单价 {unitPrice}",
                        "卖 1",
                        () => SelectNpcSale(productId, 1),
                        owned > 1 ? "卖全部" : null,
                        owned > 1
                            ? () => SelectNpcSale(productId, owned)
                            : null);
                    count++;
                }
            }

            npcTradeHint.text = pendingWorldSale || hasTraderMessage
                ? npcTradeHint.text
                : count == 0
                ? "背包中没有这个人物愿意收购的物品。"
                : string.IsNullOrWhiteSpace(pendingSellProductId)
                    ? "先选择数量，再点击下方确认出售。"
                    : npcTradeHint.text;
        }

        private NpcTradeListRowView CreateNpcTradeRow()
        {
            NpcTradeListRowView row = Instantiate(
                npcTradeRowTemplate,
                npcTradeListRoot);
            row.gameObject.SetActive(true);
            return row;
        }

        private void SelectNpcSale(string productId, int count)
        {
            pendingSellProductId = productId;
            pendingSellCount = Mathf.Max(1, count);
            CardDefinition definition =
                CardManager.Instance?.GetDefinitionById(productId);
            npcTradeHint.text =
                $"待售：{definition?.DisplayName ?? productId} ×{pendingSellCount}，" +
                $"可得 {SelectedNpcTrader.GetPlayerSellPrice(definition) * pendingSellCount} 金币。";
            RefreshLocationAction();
        }

        private void PopulateNpcTalk()
        {
            CardDefinition definition =
                SelectedNpcTrader?.Card?.Definition;
            if (definition == null)
            {
                npcTradeHint.text = "这个人物当前无法交谈。";
                return;
            }

            bool canTalk = definition.DialogueEnabled;
            NpcTradeListRowView row = CreateNpcTradeRow();
            row.Bind(
                definition,
                canTalk
                    ? $"{definition.DisplayName}\n查看人物对话"
                    : $"{definition.DisplayName}\n暂无可用对话",
                canTalk ? "开始交谈" : null,
                canTalk ? StartNpcDialogue : null);
            npcTradeHint.text = canTalk
                ? "点击按钮后，将由当前场景中的玩家人物开始交谈。"
                : "这个人物暂时没有配置对话内容。";
        }

        private void StartNpcDialogue()
        {
            CardInstance npc = SelectedNpcTrader?.Card;
            DialogueManager dialogue = DialogueManager.Instance;
            CardInstance player = CardManager.Instance?.AllCards
                .FirstOrDefault(card =>
                    DialogueManager.CanStartDialogue(card, npc));
            if (dialogue == null || player == null)
            {
                npcTradeHint.text =
                    "当前场景中没有可用于交谈的玩家人物。";
                return;
            }

            if (!dialogue.StartDialogue(player, npc))
            {
                npcTradeHint.text =
                    "现在无法开始交谈，请先结束其他互动。";
                return;
            }

            ToggleView(false);
        }

        private void ConfirmNpcSale()
        {
            if (SelectedNpcTrader == null ||
                (!pendingWorldSale &&
                 (string.IsNullOrWhiteSpace(pendingSellProductId) ||
                  pendingSellCount <= 0)))
            {
                return;
            }

            string reason;
            bool sold = pendingWorldSale
                ? SelectedNpcTrader.ConfirmWorldSale(out reason)
                : SelectedNpcTrader.TrySellFromBackpack(
                    pendingSellProductId,
                    pendingSellCount,
                    out reason);
            if (sold)
            {
                pendingSellProductId = null;
                pendingSellCount = 0;
                pendingWorldSale = false;
            }

            RefreshNpcTradeView();
            npcTradeHint.text = sold
                ? "出售成功，金币已放入背包。"
                : reason;
        }

        private void SetNpcTradePanelVisible(bool visible)
        {
            if (npcTradePanel != null)
                npcTradePanel.SetActive(visible);
            if (!visible && enterLocationButton != null)
                enterLocationButton.gameObject.SetActive(true);
        }

        private static string GetCategoryLabel(CardCategory category)
        {
            return category switch
            {
                CardCategory.Consumable => "食物",
                CardCategory.Material => "材料",
                CardCategory.Equipment => "装备",
                CardCategory.Valuable => "贵重物品",
                _ => "商品"
            };
        }

        private void SetLocationTabLabel(string text)
        {
            TMP_Text label = locationToggle?.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = text;
        }

        private enum NpcTradeTab
        {
            Buy,
            Sell,
            Talk
        }
    }
}
