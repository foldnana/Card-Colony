using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class WorldMapPartyStatusView : MonoBehaviour
    {
        private const float ExpandedWidth = 270f;
        private const float CollapsedWidth = 82f;

        public static WorldMapPartyStatusView Instance { get; private set; }

        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text memberCountLabel;
        [SerializeField] private Button collapseButton;
        [SerializeField] private TMP_Text collapseGlyph;
        [SerializeField] private PartyRosterSlotView[] memberSlots;

        private readonly List<CardData> displayedMembers = new();
        private CanvasGroup canvasGroup;
        private string displayedLocation;
        private string displayedState;
        private bool isCollapsed;

        public bool IsCollapsed => isCollapsed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            canvasGroup = GetComponent<CanvasGroup>();
            if (panelRect == null)
                panelRect = (RectTransform)transform;
            collapseButton.onClick.AddListener(ToggleCollapsed);
            PartySelectionService.SelectionChanged += OnSelectionChanged;
            Hide();
        }

        private void OnDestroy()
        {
            PartySelectionService.SelectionChanged -= OnSelectionChanged;
            if (collapseButton != null)
                collapseButton.onClick.RemoveListener(ToggleCollapsed);
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            foreach (PartyRosterSlotView slot in memberSlots)
                slot?.RefreshVitals();
        }

        public void ShowParty(
            CardInstance partyCard,
            string locationName,
            string state,
            int memberCount,
            CardData protagonistData = null)
        {
            if (partyCard == null)
            {
                Hide();
                return;
            }

            IEnumerable<CardData> savedMembers =
                GameDirector.Instance?.GameData?.PartyMembers;
            List<CardData> members = savedMembers?
                .Where(member => member != null)
                .Take(GameData.MaximumPartySize)
                .ToList() ?? new List<CardData>();
            if (members.Count == 0 && protagonistData != null)
                members.Add(protagonistData);
            if (members.Count == 0 && partyCard.Definition != null)
                members.Add(new CardData(partyCard));

            ShowMembers(
                members,
                locationName,
                state,
                protagonistData?.PersistentId,
                memberCount);
        }

        public void ShowMembers(
            IEnumerable<CardData> members,
            string locationName,
            string state,
            string preferredPersistentId = null,
            int reportedMemberCount = -1)
        {
            displayedMembers.Clear();
            displayedMembers.AddRange(members?
                .Where(member => member != null)
                .Take(GameData.MaximumPartySize) ??
                Enumerable.Empty<CardData>());
            displayedLocation = string.IsNullOrWhiteSpace(locationName)
                ? "未知地点"
                : locationName;
            displayedState = string.IsNullOrWhiteSpace(state)
                ? "待命"
                : state;

            PartySelectionService.EnsureValidSelection(
                displayedMembers,
                preferredPersistentId);
            titleLabel.text = "当前小队";
            int count = reportedMemberCount >= 0
                ? Mathf.Clamp(reportedMemberCount, 0, GameData.MaximumPartySize)
                : displayedMembers.Count;
            memberCountLabel.text = $"{count}/{GameData.MaximumPartySize}";
            RefreshSlots();
            SetVisible(true);
        }

        public void Hide()
        {
            displayedMembers.Clear();
            SetVisible(false);
        }

        public void ToggleCollapsed()
        {
            SetCollapsed(!isCollapsed);
        }

        public void SetCollapsed(bool collapsed)
        {
            isCollapsed = collapsed;
            panelRect.sizeDelta = new Vector2(
                collapsed ? CollapsedWidth : ExpandedWidth,
                panelRect.sizeDelta.y);
            titleLabel.gameObject.SetActive(!collapsed);
            memberCountLabel.gameObject.SetActive(!collapsed);
            collapseGlyph.text = collapsed ? ">" : "<";
            foreach (PartyRosterSlotView slot in memberSlots)
                slot?.SetCollapsed(collapsed);
        }

        private void RefreshSlots()
        {
            string selectedId = PartySelectionService.SelectedPersistentId;
            for (int index = 0; index < memberSlots.Length; index++)
            {
                PartyRosterSlotView slot = memberSlots[index];
                if (slot == null)
                    continue;

                CardData member = index < displayedMembers.Count
                    ? displayedMembers[index]
                    : null;
                CardDefinition definition = member == null
                    ? null
                    : ResolveDefinition(member.Id);
                CardInstance liveCard = member == null
                    ? null
                    : ResolveLiveCard(member.PersistentId);
                slot.Bind(
                    member,
                    definition,
                    liveCard,
                    displayedLocation,
                    displayedState,
                    member?.PersistentId == selectedId);
                slot.SetCollapsed(isCollapsed);
            }
        }

        private void OnSelectionChanged(string persistentId)
        {
            foreach (PartyRosterSlotView slot in memberSlots)
                slot?.SetSelected(slot.PersistentId == persistentId);
        }

        private static CardDefinition ResolveDefinition(string cardId)
        {
            CardDefinition definition =
                CardManager.Instance?.GetDefinitionById(cardId);
            if (definition != null)
                return definition;

            return Resources.LoadAll<CardDefinition>(string.Empty)
                .FirstOrDefault(candidate => candidate != null &&
                    candidate.Id == cardId);
        }

        private static CardInstance ResolveLiveCard(string persistentId)
        {
            if (string.IsNullOrWhiteSpace(persistentId))
                return null;

            return CardManager.Instance?.AllCards.FirstOrDefault(card =>
                card != null && card.PersistentId == persistentId);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
