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

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            locationToggle?.onValueChanged.AddListener(ToggleView);
            enterLocationButton?.onClick.AddListener(PerformLocationAction);
            WorldMapLocation.SelectionChanged += HandleSelectionChanged;
            WorldMapBootstrap.PartyMapStateChanged += HandlePartyMapStateChanged;

            if (WorldMapLocation.ActiveSelection != null)
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
            WorldMapBootstrap.PartyMapStateChanged -= HandlePartyMapStateChanged;
            locationToggle?.onValueChanged.RemoveListener(ToggleView);
            enterLocationButton?.onClick.RemoveListener(PerformLocationAction);
        }

        public void ShowLocation(WorldMapLocation location)
        {
            if (location == null || location.Card == null)
                return;

            SelectedLocation = location;
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

        private void HandlePartyMapStateChanged()
        {
            RefreshLocationAction();
        }

        private void PerformLocationAction()
        {
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
    }
}
