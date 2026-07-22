using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class BackpackView : MonoBehaviour
    {
        public static BackpackView Instance { get; private set; }

        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text openButtonLabel;
        [SerializeField] private RectTransform tablePanel;
        [SerializeField] private TMP_Text capacityLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button arrangeButton;
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private RectTransform dragLayer;

        public bool IsOpen => tablePanel != null && tablePanel.gameObject.activeSelf;
        public RectTransform SlotsRoot => slotsRoot;
        public RectTransform DragLayer => dragLayer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            openButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
            arrangeButton?.onClick.AddListener(BackpackService.Arrange);
            BackpackService.Changed += Refresh;
            Close();
            Refresh();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            openButton?.onClick.RemoveListener(Open);
            closeButton?.onClick.RemoveListener(Close);
            arrangeButton?.onClick.RemoveListener(BackpackService.Arrange);
            BackpackService.Changed -= Refresh;
        }

        public void Open()
        {
            if (tablePanel != null)
                tablePanel.gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (tablePanel != null)
                tablePanel.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            Rebuild(BackpackService.Current);
        }

        public void Rebuild(BackpackData backpack)
        {
            int count = backpack?.Count ?? 0;
            int capacity = backpack?.Capacity ?? 8;
            if (openButtonLabel != null)
                openButtonLabel.text = $"背包  {count}/{capacity}";
            if (capacityLabel != null)
                capacityLabel.text = $"背包  {count}/{capacity}";

            if (slotsRoot == null)
                return;

            EnsureVisualSlots(capacity);

            foreach (BackpackItemView item in
                     slotsRoot.GetComponentsInChildren<BackpackItemView>(true))
            {
                if (Application.isPlaying)
                {
                    item.gameObject.SetActive(false);
                    Destroy(item.gameObject);
                }
                else
                    DestroyImmediate(item.gameObject);
            }

            if (backpack?.Entries == null)
                return;

            TMP_FontAsset font = capacityLabel != null ? capacityLabel.font : null;
            foreach (BackpackEntryData entry in backpack.Entries
                         .Where(entry => entry?.Card != null)
                         .OrderBy(entry => entry.SlotIndex))
            {
                if (entry.SlotIndex < 0 || entry.SlotIndex >= slotsRoot.childCount)
                    continue;

                CardDefinition definition = ResolveDefinition(entry.Card.Id);
                GameObject itemObject = new GameObject(
                    $"BackpackItem_{(definition != null ? definition.DisplayName : "未知物品")}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup),
                    typeof(BackpackItemView));
                itemObject.transform.SetParent(slotsRoot.GetChild(entry.SlotIndex), false);
                itemObject.GetComponent<BackpackItemView>()
                    .Bind(entry, definition, this, font);
            }
        }

        public bool IsPointerOverStorageArea(Vector2 screenPosition)
        {
            RectTransform buttonRect = openButton?.transform as RectTransform;
            return ContainsScreenPoint(buttonRect, screenPosition) ||
                (IsOpen && ContainsScreenPoint(tablePanel, screenPosition));
        }

        internal void BeginItemDrag(BackpackItemView item, Vector2 screenPosition)
        {
            if (item == null || dragLayer == null)
                return;

            item.transform.SetParent(dragLayer, true);
            item.transform.SetAsLastSibling();
            UpdateItemDrag(item, screenPosition);
        }

        internal void UpdateItemDrag(BackpackItemView item, Vector2 screenPosition)
        {
            if (item == null || dragLayer == null)
                return;

            Canvas canvas = dragLayer.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPoint))
            {
                item.RectTransform.anchoredPosition = localPoint;
            }
        }

        internal void EndItemDrag(BackpackItemView item, Vector2 screenPosition)
        {
            if (item == null)
                return;

            bool canPlaceInWorld = !string.IsNullOrWhiteSpace(
                GameDirector.Instance?.GameData?.ActiveLocationId) &&
                SceneManager.GetActiveScene().name == "Location";

            bool taken = canPlaceInWorld &&
                !IsPointerOverStorageArea(screenPosition) &&
                TryGetWorldDropPosition(screenPosition, out Vector3 worldPosition) &&
                BackpackService.TryTake(item.EntryId, worldPosition, out _);

            if (!taken)
                Refresh();
        }

        private static CardDefinition ResolveDefinition(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            CardDefinition definition = CardManager.Instance?.GetDefinitionById(id);
            if (definition != null)
                return definition;

            return Resources.LoadAll<CardDefinition>("Cards")
                .FirstOrDefault(candidate => candidate != null && candidate.Id == id);
        }

        private void EnsureVisualSlots(int capacity)
        {
            while (slotsRoot.childCount < capacity)
            {
                GameObject slot = new GameObject(
                    $"BackpackSlot{slotsRoot.childCount + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                slot.transform.SetParent(slotsRoot, false);
                Image image = slot.GetComponent<Image>();
                image.color = new Color(0.12f, 0.14f, 0.15f, 0.72f);
                image.raycastTarget = false;
            }

            GridLayoutGroup grid = slotsRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
                return;

            int columns = Mathf.Max(1, grid.constraintCount);
            int rows = Mathf.Max(2, Mathf.CeilToInt(capacity / (float)columns));
            float height = rows * grid.cellSize.y + (rows - 1) * grid.spacing.y;
            slotsRoot.sizeDelta = new Vector2(slotsRoot.sizeDelta.x, Mathf.Max(326f, height));
        }

        private static bool TryGetWorldDropPosition(
            Vector2 screenPosition,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            Camera camera = Camera.main;
            if (camera == null || Board.Instance == null)
                return false;

            Ray ray = camera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance))
                return false;

            worldPosition = ray.GetPoint(distance).Flatten();
            return Board.Instance.IsPointValid(worldPosition);
        }

        private static bool ContainsScreenPoint(RectTransform rect, Vector2 screenPosition)
        {
            if (rect == null)
                return false;

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                rect,
                screenPosition,
                uiCamera);
        }
    }
}
