using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardColony.Gameplay;
using CardColony.Inventory;
using CardColony.Presentation;
using CardColony.TimeSystem;

namespace CardColony.UnityIntegration.UI
{
    public sealed class GameUiPresenter : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private WorldClockDriver driver;

        [Header("Clock")]
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text actionProgressText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button normalSpeedButton;
        [SerializeField] private Button fastSpeedButton;
        [SerializeField] private Button waitingButton;

        [Header("World Views")]
        [SerializeField] private GameObject worldMapView;
        [SerializeField] private GameObject forestView;
        [SerializeField] private RectTransform playerCard;
        [SerializeField] private Image actionProgressFill;
        [SerializeField] private Button mapViewButton;
        [SerializeField] private Button forestViewButton;

        [Header("Journal")]
        [SerializeField] private GameObject questContent;
        [SerializeField] private GameObject recipeContent;
        [SerializeField] private Button questTabButton;
        [SerializeField] private Button recipeTabButton;

        [Header("Actions")]
        [SerializeField] private Button exploreButton;
        [SerializeField] private Button gatherButton;
        [SerializeField] private Button brewButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;

        [Header("Backpack")]
        [SerializeField] private TMP_Text inventorySummaryText;
        [SerializeField] private TMP_Text heldCardText;
        [SerializeField] private Transform inventoryContent;
        [SerializeField] private ItemCardView itemCardPrefab;
        [SerializeField] private Button putBackButton;

        private BackpackInteraction backpackInteraction;
        private string inventoryFingerprint;
        private bool buttonsConfigured;
        private bool showingForest;
        private bool showingRecipes;
        private bool forestWasDiscovered;

        public Button ExploreButton => exploreButton;
        public Button GatherButton => gatherButton;
        public Button BrewButton => brewButton;
        public TMP_Text TimeText => timeText;
        public Transform InventoryContent => inventoryContent;

        private void Awake()
        {
            Bind(driver != null ? driver : GetComponentInParent<WorldClockDriver>());
        }

        private void OnDestroy()
        {
            if (driver != null)
                driver.StateChanged -= HandleDriverStateChanged;
        }

        public void Bind(WorldClockDriver newDriver)
        {
            if (newDriver == null)
                throw new ArgumentNullException(nameof(newDriver));

            if (driver != null)
                driver.StateChanged -= HandleDriverStateChanged;

            driver = newDriver;
            EnsureButtonsConfigured();
            driver.StateChanged += HandleDriverStateChanged;
            backpackInteraction = null;
            inventoryFingerprint = null;
            HandleDriverStateChanged();
        }

        private void HandleDriverStateChanged()
        {
            if (driver == null || driver.Session == null)
                return;

            if (backpackInteraction == null)
            {
                backpackInteraction = new BackpackInteraction(driver.Session);
                inventoryFingerprint = null;
                ResetViewStateFromSession();
            }

            Refresh();
        }

        private void ConfigureButtons()
        {
            pauseButton.onClick.AddListener(() => driver.SetPaused(!driver.Session.Clock.IsPaused));
            normalSpeedButton.onClick.AddListener(() => driver.SetNormalSpeed());
            fastSpeedButton.onClick.AddListener(() => driver.SetFastSpeed());
            waitingButton.onClick.AddListener(() => driver.SetWaiting(!driver.Session.Clock.IsWaiting));
            mapViewButton.onClick.AddListener(() => SetForestView(false));
            forestViewButton.onClick.AddListener(() => SetForestView(true));
            questTabButton.onClick.AddListener(() => SetRecipeView(false));
            recipeTabButton.onClick.AddListener(() => SetRecipeView(true));
            exploreButton.onClick.AddListener(() => driver.StartExplore());
            gatherButton.onClick.AddListener(() => driver.StartGather());
            brewButton.onClick.AddListener(() => driver.StartBrew());
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            putBackButton.onClick.AddListener(PutHeldCardBack);
        }

        private void EnsureButtonsConfigured()
        {
            if (buttonsConfigured)
                return;

            ConfigureButtons();
            buttonsConfigured = true;
        }

        private void Refresh()
        {
            PlayableLoopSession session = driver.Session;
            timeText.text = WorldClockTextFormatter.Format(session.Clock);
            statusText.text = driver.StatusMessage;

            if (session.ActiveAction == null)
            {
                actionProgressText.text = session.Clock.IsWaiting ? "主动等待中" : "当前无耗时行动";
            }
            else
            {
                actionProgressText.text =
                    $"{GetActionName(session.ActiveAction.Type)}  {session.ActiveAction.Progress01:P0}";
            }

            SetButtonLabel(pauseButton, session.Clock.IsPaused ? "继续" : "暂停");
            SetButtonLabel(waitingButton, session.Clock.IsWaiting ? "停止等待" : "主动等待");
            normalSpeedButton.interactable = session.Clock.Speed != WorldClockSpeed.Normal;
            fastSpeedButton.interactable = session.Clock.Speed != WorldClockSpeed.Fast;

            bool isIdle = session.ActiveAction == null && session.HeldCard == null;
            bool forestDiscovered = session.World.GetOrCreateLocation(PlayableLoopSession.ForestLocationId).IsDiscovered;
            if (forestDiscovered && !forestWasDiscovered)
                showingForest = true;
            forestWasDiscovered = forestDiscovered;

            worldMapView.SetActive(!showingForest);
            forestView.SetActive(showingForest);
            questContent.SetActive(!showingRecipes);
            recipeContent.SetActive(showingRecipes);
            mapViewButton.interactable = showingForest;
            forestViewButton.interactable = forestDiscovered && !showingForest;
            exploreButton.interactable = isIdle && !forestDiscovered;
            gatherButton.interactable = isIdle && forestDiscovered;
            brewButton.interactable = isIdle
                && session.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId) >= 2;

            inventorySummaryText.text =
                $"背包 {session.PlayerInventory.Cards.Count}/{session.PlayerInventory.SlotCapacity}\n"
                + $"重量 {session.PlayerInventory.CurrentWeight:0.##}/{session.PlayerInventory.MaxWeight:0.##}";
            inventorySummaryText.enableWordWrapping = true;
            heldCardText.text = backpackInteraction.HeldCard == null
                ? "手中：空"
                : $"手中：{backpackInteraction.HeldCard.ItemId} × {backpackInteraction.HeldCard.Quantity}";
            putBackButton.interactable = backpackInteraction.HeldCard != null;

            UpdateActionPresentation(session, forestDiscovered);

            string fingerprint = CreateInventoryFingerprint(session.PlayerInventory.Cards);
            if (inventoryFingerprint != fingerprint)
            {
                inventoryFingerprint = fingerprint;
                RebuildInventory(session.PlayerInventory.Cards);
            }
        }

        private void SetForestView(bool showForest)
        {
            if (showForest && !driver.Session.World
                    .GetOrCreateLocation(PlayableLoopSession.ForestLocationId)
                    .IsDiscovered)
                return;

            showingForest = showForest;
            Refresh();
        }

        private void SetRecipeView(bool showRecipes)
        {
            showingRecipes = showRecipes;
            Refresh();
        }

        private void UpdateActionPresentation(PlayableLoopSession session, bool forestDiscovered)
        {
            float progress = session.ActiveAction == null
                ? 0f
                : (float)session.ActiveAction.Progress01;
            actionProgressFill.fillAmount = progress;
            actionProgressFill.gameObject.SetActive(session.ActiveAction != null);

            Vector2 villagePosition = new Vector2(-260f, -155f);
            Vector2 forestPosition = new Vector2(70f, 40f);
            if (session.ActiveAction != null
                && session.ActiveAction.Type == LoopActionType.ExploreWhisperingForest)
            {
                playerCard.anchoredPosition = Vector2.Lerp(
                    villagePosition,
                    forestPosition,
                    progress);
            }
            else
            {
                playerCard.anchoredPosition = forestDiscovered
                    ? forestPosition
                    : villagePosition;
            }
        }

        private void RebuildInventory(IReadOnlyList<ItemCardStack> cards)
        {
            for (int index = inventoryContent.childCount - 1; index >= 0; index--)
            {
                GameObject child = inventoryContent.GetChild(index).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            foreach (ItemCardStack card in cards)
            {
                ItemCardStack boundCard = card;
                ItemCardView view = Instantiate(itemCardPrefab, inventoryContent);
                view.Bind(
                    boundCard,
                    () => TakeCard(boundCard.InstanceId, boundCard.Quantity),
                    () => SplitCard(boundCard.InstanceId));
            }
        }

        private void TakeCard(string instanceId, int quantity)
        {
            backpackInteraction.TryTake(instanceId, quantity);
            inventoryFingerprint = null;
            Refresh();
        }

        private void SplitCard(string instanceId)
        {
            backpackInteraction.TrySplitOne(instanceId);
            inventoryFingerprint = null;
            Refresh();
        }

        private void PutHeldCardBack()
        {
            backpackInteraction.TryPutBack();
            inventoryFingerprint = null;
            Refresh();
        }

        private void Save()
        {
            driver.SaveToDisk();
            inventoryFingerprint = null;
            Refresh();
        }

        private void Load()
        {
            if (!driver.LoadFromDisk())
                return;

            backpackInteraction = new BackpackInteraction(driver.Session);
            inventoryFingerprint = null;
            ResetViewStateFromSession();
            Refresh();
        }

        private void ResetViewStateFromSession()
        {
            forestWasDiscovered = driver.Session.World
                .GetOrCreateLocation(PlayableLoopSession.ForestLocationId)
                .IsDiscovered;
            showingForest = forestWasDiscovered;
            showingRecipes = false;
        }

        private static string CreateInventoryFingerprint(IReadOnlyList<ItemCardStack> cards)
        {
            var builder = new StringBuilder();
            foreach (ItemCardStack card in cards)
            {
                builder.Append(card.InstanceId)
                    .Append(':')
                    .Append(card.Quantity)
                    .Append('|');
            }

            return builder.ToString();
        }

        private static string GetActionName(LoopActionType actionType)
        {
            switch (actionType)
            {
                case LoopActionType.ExploreWhisperingForest:
                    return "探索低语森林";
                case LoopActionType.GatherHerbs:
                    return "采集草药";
                case LoopActionType.BrewPotion:
                    return "制作治疗药水";
                default:
                    return actionType.ToString();
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = label;
        }
    }
}
