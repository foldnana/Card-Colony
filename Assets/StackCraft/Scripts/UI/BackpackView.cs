using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class BackpackView : MonoBehaviour
    {
        private const string BackgroundResourcePath =
            "UI/BackpackTableBackground_Brown";
        private const float CloseButtonPadding = 14f;
        private const int OverlayLayer = 30;
        private const float OverlayCameraDistance = 15.5f;
        private static readonly Vector3 OverlayOrigin =
            new(10000f, 0f, 10000f);

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
        public BackpackBoardView Board3D => board3D;
        public Camera OverlayCamera => overlayCamera;
        public RectTransform SlotsRoot => slotsRoot;
        public RectTransform DragLayer => dragLayer;

        private BackpackBoardView board3D;
        private Camera overlayCamera;
        private Camera worldCamera;
        private PhysicsRaycaster worldRaycaster;
        private int originalWorldCullingMask;
        private int originalWorldEventMask;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(Close);
            arrangeButton?.onClick.AddListener(BackpackService.Arrange);
            BackpackService.Changed += Refresh;
            EnsureBoard3D();
            Close();
            Refresh();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            openButton?.onClick.RemoveListener(Toggle);
            closeButton?.onClick.RemoveListener(Close);
            arrangeButton?.onClick.RemoveListener(BackpackService.Arrange);
            BackpackService.Changed -= Refresh;

            if (board3D != null)
            {
                if (Application.isPlaying)
                    Destroy(board3D.gameObject);
                else
                    DestroyImmediate(board3D.gameObject);
            }

            RestoreWorldCameraMasks();
            if (overlayCamera != null)
            {
                if (Application.isPlaying)
                    Destroy(overlayCamera.gameObject);
                else
                    DestroyImmediate(overlayCamera.gameObject);
            }
        }

        public void Open()
        {
            if (tablePanel != null)
                tablePanel.gameObject.SetActive(true);
            board3D?.SetVisible(true);
            if (closeButton != null)
                closeButton.gameObject.SetActive(true);
            PositionCloseButton();
            Refresh();
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void Close()
        {
            if (tablePanel != null)
                tablePanel.gameObject.SetActive(false);
            if (closeButton != null)
                closeButton.gameObject.SetActive(false);
            board3D?.SetVisible(false);
        }

        private void LateUpdate()
        {
            if (IsOpen)
                PositionCloseButton();
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

            if (board3D != null)
            {
                board3D.Rebuild(backpack);
                return;
            }

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
                (IsOpen && board3D != null &&
                    board3D.ContainsScreenPoint(screenPosition));
        }

        internal bool TryGetStorageDragHeight(
            Vector2 screenPosition,
            out float dragHeight)
        {
            dragHeight = 0f;
            if (!IsOpen || board3D == null ||
                !board3D.ContainsScreenPoint(screenPosition))
                return false;

            dragHeight = board3D.SurfaceHeight + 0.18f;
            return true;
        }

        internal bool TryGetTableLocalPosition(
            Vector2 screenPosition,
            out Vector2 localPosition)
        {
            localPosition = Vector2.zero;
            return IsOpen &&
                board3D != null &&
                board3D.TryGetLocalTablePosition(
                    screenPosition,
                    out localPosition);
        }

        internal bool TryResolveTableDropPlacement(
            CardInstance card,
            Vector2 pointerScreenPosition,
            out Vector2 localPosition,
            out string tableStackId,
            out int tableStackOrder)
        {
            localPosition = Vector2.zero;
            tableStackId = null;
            tableStackOrder = 0;
            return IsOpen &&
                board3D != null &&
                card?.Stack != null &&
                board3D.ContainsScreenPoint(pointerScreenPosition) &&
                board3D.TryResolveStoragePlacement(
                    card,
                    card.Stack.TargetPosition,
                    out localPosition,
                    out tableStackId,
                    out tableStackOrder);
        }

        internal bool ShouldUseRigidStackDrag(CardInstance card)
        {
            return board3D != null &&
                card?.Stack != null &&
                board3D.IsStackInOverlay(card.Stack);
        }

        internal bool TryResolveBridgedDragPosition(
            CardInstance card,
            Vector2 pointerScreenPosition,
            Vector2 cardScreenOffset,
            bool bridgeAlreadyActive,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (board3D == null || card?.Stack == null)
                return false;

            Vector2 cardCenterScreen =
                pointerScreenPosition + cardScreenOffset;
            if (!IsOpen)
            {
                return TryResolveClosedBackpackDrag(
                    card,
                    cardCenterScreen,
                    bridgeAlreadyActive,
                    out worldPosition);
            }

            bool pointerOverBackpack =
                board3D.ContainsScreenPoint(pointerScreenPosition);
            bool backpackCard =
                card.GetComponent<BackpackCardProxy>() != null;
            bool currentlyInOverlay =
                board3D.IsStackInOverlay(card.Stack);

            if (pointerOverBackpack)
            {
                board3D.SetStackRenderSpace(card.Stack, useOverlay: true);
                if (!board3D.TryGetOverlayDragPosition(
                        cardCenterScreen,
                        card.Stack,
                        out worldPosition))
                {
                    board3D.SetStackRenderSpace(
                        card.Stack,
                        useOverlay: currentlyInOverlay);
                    return false;
                }

                if (!currentlyInOverlay)
                {
                    SnapStackAfterRenderSpaceChange(
                        card.Stack,
                        worldPosition);
                }
                return true;
            }

            if (!backpackCard && !currentlyInOverlay &&
                !bridgeAlreadyActive)
                return false;

            board3D.SetStackRenderSpace(card.Stack, useOverlay: false);
            if (!TryGetWorldDragPosition(
                    cardCenterScreen,
                    card.Stack,
                    out worldPosition))
            {
                board3D.SetStackRenderSpace(
                    card.Stack,
                    useOverlay: currentlyInOverlay);
                return false;
            }

            if (currentlyInOverlay)
                SnapStackAfterRenderSpaceChange(card.Stack, worldPosition);
            return true;
        }

        private bool TryResolveClosedBackpackDrag(
            CardInstance card,
            Vector2 cardCenterScreen,
            bool bridgeAlreadyActive,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            bool currentlyInOverlay =
                board3D.IsStackInOverlay(card.Stack);
            if (!bridgeAlreadyActive && !currentlyInOverlay)
                return false;

            board3D.SetStackRenderSpace(card.Stack, useOverlay: false);
            if (!TryGetWorldDragPosition(
                    cardCenterScreen,
                    card.Stack,
                    out worldPosition))
            {
                board3D.SetStackRenderSpace(
                    card.Stack,
                    useOverlay: currentlyInOverlay);
                return false;
            }

            if (currentlyInOverlay)
                SnapStackAfterRenderSpaceChange(card.Stack, worldPosition);
            return true;
        }

        private static void SnapStackAfterRenderSpaceChange(
            CardStack stack,
            Vector3 worldPosition)
        {
            stack?.SetTargetPosition(worldPosition, instant: true);
        }

        internal void RestoreDraggedStackToWorld(
            CardInstance card,
            Vector2 pointerScreenPosition,
            Vector2 cardScreenOffset)
        {
            if (card?.Stack == null || board3D == null)
                return;

            board3D.SetStackRenderSpace(card.Stack, useOverlay: false);
            if (TryGetWorldDragPosition(
                    pointerScreenPosition + cardScreenOffset,
                    card.Stack,
                    out Vector3 worldPosition))
            {
                card.Stack.SetTargetPosition(worldPosition, instant: true);
            }
        }

        internal bool TryGetTableLocalPosition(
            Vector3 worldPosition,
            CardStack stack,
            out Vector2 localPosition)
        {
            localPosition = Vector2.zero;
            return IsOpen &&
                board3D != null &&
                board3D.TryGetLocalTablePosition(
                    worldPosition,
                    stack,
                    out localPosition);
        }

        internal bool TryTakeProxyToWorld(
            BackpackCardProxy proxy,
            Vector3 dropPosition)
        {
            if (proxy?.Card?.Stack == null || BackpackService.Current == null ||
                string.IsNullOrWhiteSpace(GameDirector.Instance?.GameData?.ActiveLocationId) ||
                SceneManager.GetActiveScene().name != "Location" ||
                Board.Instance == null ||
                CardManager.Instance == null)
                return false;

            Vector3 worldPosition = dropPosition.Flatten();
            if (!Board.Instance.IsPointValid(worldPosition, proxy.Card.Stack))
                return false;

            CardStack stack = proxy.Card.Stack;
            board3D.SetStackRenderSpace(stack, useOverlay: false);
            List<BackpackCardProxy> stackProxies = stack.Cards
                .Select(card => card?.GetComponent<BackpackCardProxy>())
                .Where(candidate => candidate != null)
                .ToList();
            List<string> entryIds = stackProxies
                .Select(candidate => candidate.EntryId)
                .ToList();
            if (entryIds.Count != stack.Cards.Count)
                return false;

            return BackpackService.TryTakeExistingStack(
                BackpackService.Current,
                entryIds,
                () =>
                {
                    foreach (BackpackCardProxy stackProxy in stackProxies)
                        board3D.Detach(stackProxy);
                    CardManager.Instance.RegisterStack(stack);
                    Vector3 finalPosition = Board.Instance.EnforcePlacementRules(
                        worldPosition,
                        stack);
                    stack.SetTargetPosition(finalPosition);
                    CardManager.Instance.ResolveOverlaps();
                    return true;
                },
                () => RollbackFailedWorldTransfer(stack));
        }

        private void RollbackFailedWorldTransfer(CardStack stack)
        {
            CardManager.Instance?.UnregisterStack(stack);
            if (stack?.Cards != null)
            {
                foreach (CardInstance card in stack.Cards.ToList())
                {
                    stack.RemoveCard(card);
                    if (card == null)
                        continue;

                    card.gameObject.SetActive(false);
                    if (Application.isPlaying)
                        Destroy(card.gameObject);
                    else
                        DestroyImmediate(card.gameObject);
                }
            }

            board3D?.Rebuild(BackpackService.Current);
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

        private void EnsureBoard3D()
        {
            if (board3D != null)
                return;

            EnsureOverlayCamera();
            var boardObject = new GameObject(
                "Backpack3DBoard",
                typeof(BackpackBoardView));
            boardObject.transform.position = OverlayOrigin;
            board3D = boardObject.GetComponent<BackpackBoardView>();

            Texture2D background = Resources.Load<Texture2D>(
                BackgroundResourcePath);
            board3D.Initialize(this, background);
            board3D.ConfigureOverlay(overlayCamera, OverlayLayer);
            ConfigureLegacyPanelFor3D();
        }

        private void EnsureOverlayCamera()
        {
            if (overlayCamera != null)
                return;

            worldCamera = Camera.main;
            var cameraObject = new GameObject(
                "BackpackOverlayCamera",
                typeof(Camera),
                typeof(PhysicsRaycaster));
            overlayCamera = cameraObject.GetComponent<Camera>();
            if (worldCamera != null)
                overlayCamera.CopyFrom(worldCamera);

            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.cullingMask = 1 << OverlayLayer;
            overlayCamera.depth =
                (worldCamera != null ? worldCamera.depth : -1f) + 10f;
            overlayCamera.nearClipPlane = 0.1f;
            overlayCamera.farClipPlane = 50f;
            overlayCamera.useOcclusionCulling = false;

            Quaternion rotation = worldCamera != null
                ? worldCamera.transform.rotation
                : Quaternion.Euler(85f, 0f, 0f);
            overlayCamera.transform.rotation = rotation;
            overlayCamera.transform.position =
                OverlayOrigin - overlayCamera.transform.forward *
                OverlayCameraDistance;

            PhysicsRaycaster overlayRaycaster =
                cameraObject.GetComponent<PhysicsRaycaster>();
            overlayRaycaster.eventMask = 1 << OverlayLayer;

            if (worldCamera == null)
                return;

            originalWorldCullingMask = worldCamera.cullingMask;
            worldCamera.cullingMask &= ~(1 << OverlayLayer);
            worldRaycaster = worldCamera.GetComponent<PhysicsRaycaster>();
            if (worldRaycaster != null)
            {
                originalWorldEventMask = worldRaycaster.eventMask;
                worldRaycaster.eventMask &= ~(1 << OverlayLayer);
            }
        }

        private void RestoreWorldCameraMasks()
        {
            if (worldCamera != null)
                worldCamera.cullingMask = originalWorldCullingMask;
            if (worldRaycaster != null)
                worldRaycaster.eventMask = originalWorldEventMask;
        }

        private static bool TryGetWorldDragPosition(
            Vector2 screenPosition,
            CardStack stack,
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

            Vector3 point = ray.GetPoint(distance).Flatten();
            point.y = stack?.TopCard?.Settings?.DragHeight ?? 0.1f;
            worldPosition = Board.Instance.ClampToBounds(point, stack);
            return true;
        }

        private void ConfigureLegacyPanelFor3D()
        {
            if (tablePanel == null)
                return;

            if (closeButton != null)
            {
                closeButton.transform.SetParent(transform, false);
                closeButton.transform.SetAsLastSibling();
            }

            foreach (Graphic graphic in
                     tablePanel.GetComponentsInChildren<Graphic>(true))
            {
                graphic.enabled = false;
                graphic.raycastTarget = false;
            }

            foreach (Selectable selectable in
                     tablePanel.GetComponentsInChildren<Selectable>(true))
                selectable.gameObject.SetActive(false);

            capacityLabel?.gameObject.SetActive(false);
            slotsRoot?.gameObject.SetActive(false);
            if (arrangeButton != null)
                arrangeButton.gameObject.SetActive(false);
        }

        private void PositionCloseButton()
        {
            RectTransform closeRect = closeButton?.transform as RectTransform;
            RectTransform parentRect = closeRect?.parent as RectTransform;
            if (closeRect == null ||
                parentRect == null ||
                board3D == null ||
                !board3D.TryGetTopRightScreenPoint(out Vector2 screenPoint))
                return;

            Canvas canvas = parentRect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null &&
                canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            float scaleFactor = canvas != null
                ? Mathf.Max(0.01f, canvas.scaleFactor)
                : 1f;
            screenPoint.x -=
                (closeRect.rect.width * 0.5f + CloseButtonPadding) * scaleFactor;
            screenPoint.y -=
                (closeRect.rect.height * 0.5f + CloseButtonPadding) * scaleFactor;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPoint,
                    uiCamera,
                    out Vector2 localPoint))
                return;

            closeRect.anchorMin = new Vector2(0.5f, 0.5f);
            closeRect.anchorMax = new Vector2(0.5f, 0.5f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = localPoint;
        }
    }
}
