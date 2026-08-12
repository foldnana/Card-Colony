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
        [SerializeField] private Button npcActionTabButton;
        [SerializeField] private RectTransform npcTradeListRoot;
        [SerializeField] private NpcTradeListRowView npcTradeRowTemplate;
        [SerializeField] private TMP_Text npcTradeHint;

        private CanvasGroup canvasGroup;
        private MenuToggle sidebarToggle;
        private NpcTradeTab npcTradeTab;
        private string pendingSellProductId;
        private int pendingSellCount;
        private MarketQuote pendingSellMarketQuote;
        private bool hasPendingSellMarketQuote;
        private bool pendingWorldSale;
        private bool standardDescriptionColorCaptured;
        private Color standardDescriptionColor;

        public WorldMapLocation SelectedLocation { get; private set; }
        public LocationEntrance SelectedBuilding { get; private set; }
        public MarketProductVendor SelectedMarketOffer { get; private set; }
        public MarketCardBuyer SelectedMarketBuyer { get; private set; }
        public NpcTrader SelectedNpcTrader { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            sidebarToggle = transform.parent?
                .GetComponentInChildren<MenuToggle>(true);
            locationToggle?.onValueChanged.AddListener(ToggleView);
            enterLocationButton?.onClick.AddListener(PerformLocationAction);
            npcBuyTabButton?.onClick.AddListener(ShowNpcBuyList);
            npcSellTabButton?.onClick.AddListener(ShowNpcSellList);
            npcActionTabButton?.onClick.AddListener(ShowNpcActions);
            WorldMapLocation.SelectionChanged += HandleSelectionChanged;
            LocationEntrance.SelectionChanged += HandleBuildingSelectionChanged;
            MarketProductVendor.SelectionChanged +=
                HandleMarketOfferSelectionChanged;
            MarketCardBuyer.SelectionChanged +=
                HandleMarketBuyerSelectionChanged;
            NpcTrader.SelectionChanged += HandleNpcTraderSelectionChanged;
            NpcInteractionManager.SessionChanged +=
                HandleNpcInteractionSessionChanged;
            NpcInteractionManager.StateChanged +=
                HandleNpcInteractionStateChanged;
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
            NpcInteractionManager.SessionChanged -=
                HandleNpcInteractionSessionChanged;
            NpcInteractionManager.StateChanged -=
                HandleNpcInteractionStateChanged;
            WorldMapBootstrap.PartyMapStateChanged -= HandlePartyMapStateChanged;
            BackpackService.Changed -= HandleMarketFundsChanged;
            if (CardManager.Instance != null)
                CardManager.Instance.OnStatsChanged -= HandleMarketStatsChanged;
            locationToggle?.onValueChanged.RemoveListener(ToggleView);
            enterLocationButton?.onClick.RemoveListener(PerformLocationAction);
            npcBuyTabButton?.onClick.RemoveListener(ShowNpcBuyList);
            npcSellTabButton?.onClick.RemoveListener(ShowNpcSellList);
            npcActionTabButton?.onClick.RemoveListener(ShowNpcActions);
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

            if (show)
            {
                if (sidebarToggle == null)
                {
                    sidebarToggle = transform.parent?
                        .GetComponentInChildren<MenuToggle>(true);
                }
                sidebarToggle?.Open();
            }

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
            hasPendingSellMarketQuote = false;
            pendingWorldSale = trader.PendingWorldSale != null;
            SetLocationTabLabel("人物");

            CardDefinition definition = trader.Card.Definition;
            titleLabel.text = definition.DisplayName;
            artImage.texture = definition.ArtTexture;
            artImage.enabled = artImage.texture != null;
            typeAndDangerLabel.text =
                IsSelectedNpcApproaching()
                    ? "正在接近 · 请稍候"
                    : HasActiveInteractionWithSelectedNpc()
                    ? "互动中 · 请选择行动"
                    : "可互动 · 拖入人物卡开始";
            discoveryLabel.text =
                $"● 可用资金 {trader.AvailableFunds} 金币";
            travelTimeLabel.text = HasActiveInteractionWithSelectedNpc()
                ? "双方已进入人物交互框"
                : "拖动玩家人物卡到 NPC 身边开始互动";
            resourcesLabel.text = string.Empty;
            descriptionLabel.text = definition.Description ?? string.Empty;
            SetNpcTradePanelVisible(true);

            bool isTradeState = IsSelectedNpcTradeState();
            npcTradeTab = isTradeState
                ? pendingWorldSale
                    ? NpcTradeTab.Sell
                    : NpcTradeTab.Buy
                : NpcTradeTab.Actions;
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
            if (!IsSelectedNpcTradeState())
                return;

            npcTradeTab = NpcTradeTab.Buy;
            pendingSellProductId = null;
            pendingSellCount = 0;
            hasPendingSellMarketQuote = false;
            pendingWorldSale = false;
            RefreshNpcTradeView();
        }

        public void ShowNpcSellList()
        {
            if (!IsSelectedNpcTradeState())
                return;

            npcTradeTab = NpcTradeTab.Sell;
            pendingWorldSale = SelectedNpcTrader?.PendingWorldSale != null;
            RefreshNpcTradeView();
        }

        public void ShowNpcActions()
        {
            if (HasActiveInteractionWithSelectedNpc())
                NpcInteractionManager.Instance.ShowActions();

            npcTradeTab = NpcTradeTab.Actions;
            pendingSellProductId = null;
            pendingSellCount = 0;
            hasPendingSellMarketQuote = false;
            pendingWorldSale = false;
            RefreshNpcTradeView();
        }

        public void StartSelectedNpcInteraction()
        {
            if (SelectedNpcTrader?.Card == null)
                return;

            NpcInteractionManager interaction =
                NpcInteractionManager.Instance ??
                NpcInteractionManager.Ensure(
                    DialogueManager.Instance?.gameObject);
            if (interaction == null)
            {
                npcTradeHint.text = "当前无法创建人物互动。";
                return;
            }

            CardInstance player = CardManager.Instance?.AllCards
                .Where(card =>
                    NpcInteractionManager.CanStartInteraction(
                        card,
                        SelectedNpcTrader.Card))
                .OrderByDescending(card =>
                    card.PersistentId ==
                    PartySelectionService.SelectedPersistentId)
                .ThenBy(card =>
                    (card.transform.position -
                     SelectedNpcTrader.Card.transform.position)
                    .sqrMagnitude)
                .FirstOrDefault();
            string summonReason = null;
            if (player == null &&
                LocationSceneController.Instance != null &&
                LocationSceneController.Instance
                    .TryBringSelectedPartyMemberToActiveBuilding(
                        out CardInstance summoned,
                        out summonReason))
            {
                player = summoned;
            }
            if (player == null)
            {
                npcTradeHint.text = !string.IsNullOrWhiteSpace(summonReason)
                    ? summonReason
                    : "当前场景中没有可用于互动的玩家人物。";
                return;
            }

            if (!interaction.StartInteraction(
                    player,
                    SelectedNpcTrader.Card))
            {
                npcTradeHint.text =
                    "现在无法开始人物互动。";
                return;
            }

            npcTradeTab = NpcTradeTab.Actions;
            RefreshNpcTradeView();
        }

        public void EndSelectedNpcInteraction()
        {
            if (!HasActiveInteractionWithSelectedNpc())
                return;

            NpcInteractionManager.Instance.EndInteraction();
            npcTradeTab = NpcTradeTab.Actions;
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

        private void HandleNpcInteractionSessionChanged(
            NpcInteractionManager interaction)
        {
            if (interaction != null &&
                !interaction.IsPlayerInvolved)
            {
                return;
            }

            if (interaction?.Npc != null)
            {
                NpcTrader trader =
                    interaction.Npc.GetComponent<NpcTrader>();
                if (trader != null)
                    ShowNpcTrader(trader);
                return;
            }

            if (SelectedNpcTrader != null)
            {
                npcTradeTab = NpcTradeTab.Actions;
                ShowNpcTrader(SelectedNpcTrader);
            }
        }

        private void HandleNpcInteractionStateChanged(
            NpcInteractionState state)
        {
            if (SelectedNpcTrader == null ||
                !HasActiveInteractionWithSelectedNpc())
            {
                return;
            }

            switch (state)
            {
                case NpcInteractionState.Approaching:
                    npcTradeTab = NpcTradeTab.Actions;
                    break;
                case NpcInteractionState.Dialogue:
                    ToggleView(false);
                    return;
                case NpcInteractionState.Trade:
                    if (npcTradeTab == NpcTradeTab.Actions)
                        npcTradeTab = NpcTradeTab.Buy;
                    break;
                case NpcInteractionState.ChoosingAction:
                    npcTradeTab = NpcTradeTab.Actions;
                    break;
            }

            ToggleView(true);
            RefreshNpcTradeView();
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
                    HasActiveInteractionWithSelectedNpc() &&
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
                    npcTradeTab == NpcTradeTab.Sell &&
                    HasActiveInteractionWithSelectedNpc());
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
                        : "该建筑暂未开放";
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

            for (int index = npcTradeListRoot.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = npcTradeListRoot.GetChild(index);
                if (child != npcTradeRowTemplate.transform)
                {
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }

            bool hasInteraction =
                HasActiveInteractionWithSelectedNpc();
            bool isTradePage =
                IsSelectedNpcTradeState() &&
                npcTradeTab != NpcTradeTab.Actions;
            SetNpcTradeNavigationVisible(isTradePage);
            if (npcBuyTabButton != null)
            {
                npcBuyTabButton.interactable =
                    hasInteraction &&
                    npcTradeTab != NpcTradeTab.Buy;
            }
            if (npcSellTabButton != null)
            {
                npcSellTabButton.interactable =
                    hasInteraction &&
                    npcTradeTab != NpcTradeTab.Sell;
            }
            if (npcActionTabButton != null)
            {
                npcActionTabButton.interactable =
                    npcTradeTab != NpcTradeTab.Actions;
            }
            enterLocationButton.gameObject.SetActive(
                hasInteraction &&
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
                    PopulateNpcActions();
                    break;
            }

            discoveryLabel.text =
                hasInteraction
                    ? $"● 互动中 · 可用资金 {SelectedNpcTrader.AvailableFunds} 金币"
                    : $"● 可用资金 {SelectedNpcTrader.AvailableFunds} 金币";
            RefreshLocationAction();
        }

        private void PopulateNpcBuyList()
        {
            int count = 0;
            foreach (LocationMarketOffer offer in
                     SelectedNpcTrader.SellOffers)
            {
                LocationMarketOffer capturedOffer = offer;
                bool hasMarketQuote =
                    NpcTradeService.TryGetMarketQuote(
                        SelectedNpcTrader,
                        capturedOffer.ProductDefinition,
                        out MarketQuote marketQuote);
                int stock = hasMarketQuote
                    ? marketQuote.AvailableStock
                    : SelectedNpcTrader.GetStock(capturedOffer);
                int price = hasMarketQuote
                    ? marketQuote.PlayerBuyUnitPrice
                    : SelectedNpcTrader.GetPlayerBuyPrice(capturedOffer);
                int owned = BackpackService.Current?.Entries?.Count(entry =>
                    entry?.Card?.Id ==
                    capturedOffer.ProductDefinition.Id) ?? 0;
                NpcTradeListRowView row = CreateNpcTradeRow();
                row.Bind(
                    capturedOffer.ProductDefinition,
                    $"{capturedOffer.ProductDefinition.DisplayName}\n" +
                    (hasMarketQuote
                        ? $"{price} 金币 · {GetTrendLabel(marketQuote.Trend)}" +
                          $" · 库存 {stock} · 持有 {owned}"
                        : $"{price} 金币 · 库存 {stock}"),
                    stock > 0 ? "购买" : null,
                    stock > 0
                        ? () =>
                        {
                            bool purchased = hasMarketQuote
                                ? SelectedNpcTrader.TryPurchase(
                                    capturedOffer,
                                    marketQuote,
                                    out string reason)
                                : SelectedNpcTrader.TryPurchase(
                                    capturedOffer,
                                    out reason);
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
                    bool hasMarketQuote =
                        NpcTradeService.TryGetMarketQuote(
                            SelectedNpcTrader,
                            definition,
                            out MarketQuote marketQuote);
                    int unitPrice = hasMarketQuote
                        ? marketQuote.PlayerSellUnitPrice
                        : SelectedNpcTrader.GetPlayerSellPrice(definition);
                    NpcTradeListRowView row = CreateNpcTradeRow();
                    row.Bind(
                        definition,
                        $"{definition.DisplayName}\n" +
                        (hasMarketQuote
                            ? $"卖价 {unitPrice} · " +
                              $"{GetTrendLabel(marketQuote.Trend)} · " +
                              $"最多收购 " +
                              $"{marketQuote.MarketAffordableQuantity} · " +
                              $"持有 {owned}"
                            : $"持有 {owned} · 单价 {unitPrice}"),
                        "卖 1",
                        () => SelectNpcSale(
                            productId,
                            1,
                            hasMarketQuote,
                            marketQuote),
                        owned > 1 ? "卖全部" : null,
                        owned > 1
                            ? () => SelectNpcSale(
                                productId,
                                owned,
                                hasMarketQuote,
                                marketQuote)
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

        private NpcTradeListRowView CreateNpcTradeRow()
        {
            NpcTradeListRowView row = Instantiate(
                npcTradeRowTemplate,
                npcTradeListRoot);
            row.gameObject.SetActive(true);
            return row;
        }

        private void SelectNpcSale(
            string productId,
            int count,
            bool hasMarketQuote,
            MarketQuote marketQuote)
        {
            pendingSellProductId = productId;
            pendingSellCount = Mathf.Max(1, count);
            hasPendingSellMarketQuote = hasMarketQuote;
            pendingSellMarketQuote = marketQuote;
            CardDefinition definition =
                CardManager.Instance?.GetDefinitionById(productId);
            int unitPrice = hasMarketQuote
                ? marketQuote.PlayerSellUnitPrice
                : SelectedNpcTrader.GetPlayerSellPrice(definition);
            npcTradeHint.text =
                $"待售：{definition?.DisplayName ?? productId} ×{pendingSellCount}，" +
                $"可得 {unitPrice * pendingSellCount} 金币。";
            RefreshLocationAction();
        }

        private void PopulateNpcActions()
        {
            CardDefinition definition =
                SelectedNpcTrader?.Card?.Definition;
            if (definition == null)
            {
                npcTradeHint.text = "这个人物当前无法互动。";
                return;
            }

            if (IsSelectedNpcApproaching())
            {
                NpcTradeListRowView approachRow =
                    CreateNpcTradeRow();
                approachRow.BindAction(
                    "正在接近\n人物抵达后即可选择行动",
                    null,
                    null);
                npcTradeHint.text =
                    "玩家人物正在前往 NPC 所在位置。";
                return;
            }

            if (!HasActiveInteractionWithSelectedNpc())
            {
                NpcTradeListRowView startRow = CreateNpcTradeRow();
                startRow.BindAction(
                    "开始互动\n选择一名玩家人物参与",
                    "开始互动",
                    StartSelectedNpcInteraction);
                npcTradeHint.text =
                    "拖动玩家人物卡到 NPC 身边，或点击“开始互动”。";
                return;
            }

            CardInstance actor = NpcInteractionManager.Instance.Player;
            if (definition.DialogueEnabled)
            {
                NpcTradeListRowView talkRow = CreateNpcTradeRow();
                talkRow.BindAction(
                    "交谈\n了解人物和当前地点的信息",
                    "交谈",
                    StartNpcDialogue);
            }

            bool canTrade = NpcTradeService.CanTradeNow(
                SelectedNpcTrader,
                out string tradeReason);
            NpcTradeListRowView tradeRow = CreateNpcTradeRow();
            tradeRow.BindAction(
                canTrade
                    ? "交易\n查看对方的购买与出售列表"
                    : $"交易\n{tradeReason}",
                canTrade ? "交易" : null,
                canTrade ? BeginSelectedNpcTradeFromActions : null);

            NpcTradeListRowView endRow = CreateNpcTradeRow();
            endRow.BindAction(
                "结束互动\n双方人物卡将返回原来的位置",
                "结束",
                EndSelectedNpcInteraction);

            npcTradeHint.text =
                $"当前参与者：{actor?.Definition?.DisplayName ?? "玩家人物"}。" +
                "选择一项行动继续。";
        }

        private void StartNpcDialogue()
        {
            if (!HasActiveInteractionWithSelectedNpc())
            {
                npcTradeHint.text =
                    "请先开始人物互动。";
                return;
            }

            if (!NpcInteractionManager.Instance.BeginDialogue(
                    out string reason))
            {
                npcTradeHint.text = reason;
                return;
            }

            ToggleView(false);
        }

        private void BeginSelectedNpcTradeFromActions()
        {
            if (!TryBeginSelectedNpcTrade())
                return;

            npcTradeTab = NpcTradeTab.Buy;
            RefreshNpcTradeView();
        }

        private bool TryBeginSelectedNpcTrade()
        {
            if (!HasActiveInteractionWithSelectedNpc())
            {
                npcTradeTab = NpcTradeTab.Actions;
                RefreshNpcTradeView();
                npcTradeHint.text =
                    "请先拖动玩家人物卡到 NPC 身边开始互动。";
                return false;
            }

            if (!NpcInteractionManager.Instance.BeginTrade(
                    out string reason))
            {
                npcTradeTab = NpcTradeTab.Actions;
                RefreshNpcTradeView();
                npcTradeHint.text = reason;
                return false;
            }

            return true;
        }

        private bool HasActiveInteractionWithSelectedNpc()
        {
            return SelectedNpcTrader?.Card != null &&
                NpcInteractionManager.Instance?.IsActive == true &&
                NpcInteractionManager.Instance.IsPlayerInvolved &&
                NpcInteractionManager.Instance.Npc ==
                    SelectedNpcTrader.Card;
        }

        private bool IsSelectedNpcApproaching()
        {
            return HasActiveInteractionWithSelectedNpc() &&
                NpcInteractionManager.Instance.State ==
                    NpcInteractionState.Approaching;
        }

        private bool IsSelectedNpcTradeState()
        {
            return HasActiveInteractionWithSelectedNpc() &&
                NpcInteractionManager.Instance.State ==
                    NpcInteractionState.Trade;
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
            bool sold;
            if (pendingWorldSale)
            {
                sold = SelectedNpcTrader.ConfirmWorldSale(out reason);
            }
            else if (hasPendingSellMarketQuote)
            {
                sold = SelectedNpcTrader.TrySellFromBackpack(
                    pendingSellProductId,
                    pendingSellCount,
                    pendingSellMarketQuote,
                    out reason);
            }
            else
            {
                sold = SelectedNpcTrader.TrySellFromBackpack(
                    pendingSellProductId,
                    pendingSellCount,
                    out reason);
            }
            if (sold)
            {
                pendingSellProductId = null;
                pendingSellCount = 0;
                hasPendingSellMarketQuote = false;
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
            ApplyNpcPresentationLayout(visible);
            if (!visible && enterLocationButton != null)
                enterLocationButton.gameObject.SetActive(true);
        }

        private void SetNpcTradeNavigationVisible(bool visible)
        {
            if (npcBuyTabButton != null)
                npcBuyTabButton.gameObject.SetActive(visible);
            if (npcSellTabButton != null)
                npcSellTabButton.gameObject.SetActive(visible);
            if (npcActionTabButton != null)
            {
                npcActionTabButton.gameObject.SetActive(visible);
                TMP_Text label = npcActionTabButton
                    .GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = "返回";
            }

            RectTransform scrollRect =
                npcTradeListRoot?.parent?.parent as RectTransform;
            if (scrollRect != null)
            {
                Vector2 anchorMax = scrollRect.anchorMax;
                anchorMax.y = visible ? 0.84f : 0.98f;
                scrollRect.anchorMax = anchorMax;
            }
        }

        private void ApplyNpcPresentationLayout(bool npcMode)
        {
            if (!standardDescriptionColorCaptured &&
                descriptionLabel != null)
            {
                standardDescriptionColor = descriptionLabel.color;
                standardDescriptionColorCaptured = true;
            }

            SetActive(discoveryLabel, !npcMode);
            SetActive(travelTimeLabel, !npcMode);
            SetActive(resourcesLabel, !npcMode);

            if (npcMode)
            {
                SetAnchors(titleLabel?.rectTransform, 0.36f, 0.93f, 0.88f, 0.96f);
                SetAnchors(artImage?.rectTransform, 0.07f, 0.31f, 0.77f, 0.91f);
                SetAnchors(typeAndDangerLabel?.rectTransform, 0.36f, 0.93f, 0.80f, 0.87f);
                SetAnchors(descriptionLabel?.rectTransform, 0.07f, 0.93f, 0.62f, 0.76f);

                ConfigureText(titleLabel, 30f, TextAlignmentOptions.MidlineLeft);
                ConfigureText(typeAndDangerLabel, 20f, TextAlignmentOptions.MidlineLeft);
                ConfigureText(descriptionLabel, 19f, TextAlignmentOptions.TopLeft);
                if (descriptionLabel != null)
                    descriptionLabel.color = new Color(0.28f, 0.31f, 0.34f, 1f);
                return;
            }

            SetAnchors(titleLabel?.rectTransform, 0.06f, 0.94f, 0.88f, 0.97f);
            SetAnchors(artImage?.rectTransform, 0.32f, 0.68f, 0.67f, 0.87f);
            SetAnchors(typeAndDangerLabel?.rectTransform, 0.06f, 0.94f, 0.61f, 0.67f);
            SetAnchors(descriptionLabel?.rectTransform, 0.07f, 0.93f, 0.115f, 0.30f);

            ConfigureText(titleLabel, 36f, TextAlignmentOptions.Center);
            ConfigureText(typeAndDangerLabel, 24f, TextAlignmentOptions.Center);
            ConfigureText(descriptionLabel, 21f, TextAlignmentOptions.TopLeft);
            if (descriptionLabel != null &&
                standardDescriptionColorCaptured)
            {
                descriptionLabel.color = standardDescriptionColor;
            }
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

        private static void ConfigureText(
            TMP_Text label,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            if (label == null)
                return;

            label.fontSize = fontSize;
            label.alignment = alignment;
        }

        private static void SetAnchors(
            RectTransform rect,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
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
            Actions
        }
    }
}
