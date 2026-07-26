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

        private CanvasGroup canvasGroup;

        public WorldMapLocation SelectedLocation { get; private set; }
        public LocationEntrance SelectedBuilding { get; private set; }
        public MarketProductVendor SelectedMarketOffer { get; private set; }
        public MarketCardBuyer SelectedMarketBuyer { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            locationToggle?.onValueChanged.AddListener(ToggleView);
            enterLocationButton?.onClick.AddListener(PerformLocationAction);
            WorldMapLocation.SelectionChanged += HandleSelectionChanged;
            LocationEntrance.SelectionChanged += HandleBuildingSelectionChanged;
            MarketProductVendor.SelectionChanged +=
                HandleMarketOfferSelectionChanged;
            MarketCardBuyer.SelectionChanged +=
                HandleMarketBuyerSelectionChanged;
            WorldMapBootstrap.PartyMapStateChanged += HandlePartyMapStateChanged;
            BackpackService.Changed += HandleMarketFundsChanged;
            if (CardManager.Instance != null)
                CardManager.Instance.OnStatsChanged += HandleMarketStatsChanged;

            if (MarketProductVendor.ActiveSelection != null)
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
            WorldMapBootstrap.PartyMapStateChanged -= HandlePartyMapStateChanged;
            BackpackService.Changed -= HandleMarketFundsChanged;
            if (CardManager.Instance != null)
                CardManager.Instance.OnStatsChanged -= HandleMarketStatsChanged;
            locationToggle?.onValueChanged.RemoveListener(ToggleView);
            enterLocationButton?.onClick.RemoveListener(PerformLocationAction);
        }

        public void ShowLocation(WorldMapLocation location)
        {
            if (location == null || location.Card == null)
                return;

            SelectedLocation = location;
            SelectedBuilding = null;
            SelectedMarketOffer = null;
            SelectedMarketBuyer = null;
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

        private void PerformLocationAction()
        {
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
    }
}
