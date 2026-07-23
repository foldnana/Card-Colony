using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class BackpackBoardView : MonoBehaviour
    {
        private static readonly Vector2 DefaultSize = new(6.6f, 4.2f);
        private const float BaseThickness = 0.32f;
        private const float CardSurfaceOffset = 0.08f;
        private const float HiddenOffset = 1.2f;
        private const float TransitionDuration = 0.32f;
        private const float SurfaceCameraSmoothTime = 0.045f;

        private readonly Dictionary<string, BackpackCardProxy> proxies = new();
        private readonly HashSet<CardStack> synchronizedStacks = new();
        private BoxCollider surfaceCollider;
        private Material baseMaterial;
        private Transform cardsRoot;
        private BackpackView owner;
        private Camera interactionCamera;
        private int overlayLayer;
        private Vector2 viewportAnchor = new(0.43f, 0.48f);
        private Vector2 surfaceDragScreenOffset;
        private Vector3 restingBoardPosition;
        private Vector3 surfaceCameraTargetPosition;
        private Vector3 surfaceCameraVelocity;
        private bool hasSurfaceCameraTarget;
        private bool isSurfaceDragging;
        private bool hasEverOpened;

        public float SurfaceHeight => BaseThickness;
        public Vector2 Size => DefaultSize;
        public Camera InteractionCamera => interactionCamera;
        public Vector2 ViewportAnchor => viewportAnchor;

        private void Awake()
        {
            BuildVisuals();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            ClearProxies();

            if (baseMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(baseMaterial);
            else
                DestroyImmediate(baseMaterial);
        }

        private void LateUpdate()
        {
            UpdateSurfaceCameraMotion(Time.unscaledDeltaTime);
        }

        public void Initialize(BackpackView backpackView, Texture texture)
        {
            owner = backpackView;
            BuildVisuals();
            if (baseMaterial != null && texture != null)
            {
                baseMaterial.mainTexture = texture;
                baseMaterial.mainTextureScale = new Vector2(1f, -1f);
                baseMaterial.mainTextureOffset = new Vector2(0f, 1f);
                baseMaterial.color = Color.white;
            }
        }

        public void ConfigureOverlay(Camera camera, int layer)
        {
            interactionCamera = camera;
            overlayLayer = layer;
            restingBoardPosition = transform.position.Flatten();
            transform.position = restingBoardPosition;
            SetLayerRecursively(gameObject, overlayLayer);
            PositionInCameraView();
        }

        public void SetVisible(bool visible)
        {
            transform.DOKill();
            if (visible)
            {
                hasEverOpened = true;
                transform.position = restingBoardPosition;
                PositionInCameraView();
                gameObject.SetActive(true);
                Vector3 shownPosition = transform.position;
                transform.position = shownPosition + Vector3.down * HiddenOffset;
                transform.DOMove(shownPosition, TransitionDuration)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .OnUpdate(SynchronizeStackTargetsToVisuals)
                    .OnComplete(SynchronizeStackTargetsToVisuals);
                return;
            }

            hasSurfaceCameraTarget = false;
            isSurfaceDragging = false;
            surfaceCameraVelocity = Vector3.zero;
            bool animateClose = Application.isPlaying &&
                gameObject.activeSelf &&
                hasEverOpened;
            if (!animateClose)
            {
                ClearProxies();
                gameObject.SetActive(false);
                return;
            }

            transform.DOMoveY(transform.position.y - HiddenOffset, TransitionDuration * 0.7f)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (this != null)
                    {
                        ClearProxies();
                        gameObject.SetActive(false);
                    }
                });
        }

        public void Rebuild(BackpackData backpack)
        {
            ClearProxies();
            if (!gameObject.activeSelf || backpack?.Entries == null ||
                CardManager.Instance == null)
                return;

            foreach (IGrouping<string, BackpackEntryData> group in backpack.Entries
                         .Where(entry => entry?.Card != null)
                         .GroupBy(entry => string.IsNullOrWhiteSpace(entry.TableStackId)
                             ? entry.InstanceId
                             : entry.TableStackId))
            {
                List<BackpackEntryData> stackEntries = group
                    .OrderBy(entry => entry.TableStackOrder)
                    .ThenBy(entry => entry.SlotIndex)
                    .ToList();
                Vector3 position = GetTableWorldPosition(stackEntries[0]);
                CardStack restoredStack = null;

                foreach (BackpackEntryData entry in stackEntries)
                {
                    CardInstance card = CardManager.Instance.RestoreUnmanagedCardFromData(
                        entry.Card,
                        position);
                    if (card == null)
                        continue;

                    if (restoredStack == null)
                    {
                        restoredStack = card.Stack;
                    }
                    else
                    {
                        CardStack singleCardStack = card.Stack;
                        singleCardStack.RemoveCard(card);
                        restoredStack.AddCard(card);
                    }

                    card.transform.SetParent(cardsRoot, true);
                    SetLayerRecursively(card.gameObject, overlayLayer);
                    BackpackCardProxy proxy =
                        card.gameObject.AddComponent<BackpackCardProxy>();
                    proxy.Bind(owner, this, card, entry.InstanceId, entry.SlotIndex);
                    proxies[entry.InstanceId] = proxy;
                }

                if (restoredStack != null)
                {
                    restoredStack.IsLocked = false;
                    position = GetClampedWorldPosition(
                        position,
                        restoredStack);
                    restoredStack.SetTargetPosition(position, instant: true);
                    PersistStackPlacement(
                        restoredStack,
                        position,
                        stackEntries[0].TableStackId,
                        backpack);
                }
            }
        }

        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            return TryRaycastSurface(screenPosition, out _);
        }

        internal bool TryGetLocalTablePosition(
            Vector2 screenPosition,
            out Vector2 localPosition)
        {
            localPosition = Vector2.zero;
            if (!TryRaycastSurface(screenPosition, out RaycastHit hit))
                return false;

            Vector3 local = ClampLocalPosition(
                transform.InverseTransformPoint(hit.point),
                null);
            localPosition = new Vector2(local.x, local.z);
            return true;
        }

        internal bool TryGetLocalTablePosition(
            Vector3 worldPosition,
            CardStack stack,
            out Vector2 localPosition)
        {
            Vector3 local = ClampLocalPosition(
                transform.InverseTransformPoint(worldPosition),
                stack);
            localPosition = new Vector2(local.x, local.z);
            return true;
        }

        internal bool TryGetTopRightScreenPoint(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
            Camera camera = interactionCamera;
            if (!gameObject.activeInHierarchy || camera == null)
                return false;

            float halfWidth = DefaultSize.x * 0.5f;
            float halfDepth = DefaultSize.y * 0.5f;
            Vector3[] corners =
            {
                new(-halfWidth, SurfaceHeight, -halfDepth),
                new(-halfWidth, SurfaceHeight, halfDepth),
                new(halfWidth, SurfaceHeight, -halfDepth),
                new(halfWidth, SurfaceHeight, halfDepth)
            };

            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                Vector3 projected = camera.WorldToScreenPoint(
                    transform.TransformPoint(corner));
                if (projected.z <= 0f)
                    continue;
                maxX = Mathf.Max(maxX, projected.x);
                maxY = Mathf.Max(maxY, projected.y);
            }

            if (maxX == float.MinValue || maxY == float.MinValue)
                return false;

            screenPosition = new Vector2(maxX, maxY);
            return true;
        }

        internal bool TryGetOverlayDragPosition(
            Vector2 screenPosition,
            CardStack stack,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (interactionCamera == null)
                return false;

            float surfaceY = transform.TransformPoint(new Vector3(
                0f,
                SurfaceHeight + CardSurfaceOffset + 0.1f,
                0f)).y;
            Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 local = ClampLocalPosition(
                transform.InverseTransformPoint(ray.GetPoint(distance)),
                stack);
            worldPosition = transform.TransformPoint(new Vector3(
                local.x,
                SurfaceHeight + CardSurfaceOffset + 0.1f,
                local.z));
            return true;
        }

        internal bool IsStackInOverlay(CardStack stack)
        {
            return stack?.TopCard != null &&
                stack.TopCard.gameObject.layer == overlayLayer;
        }

        internal void SetStackRenderSpace(CardStack stack, bool useOverlay)
        {
            if (stack?.Cards == null)
                return;

            int layer = useOverlay ? overlayLayer : 0;
            foreach (CardInstance card in stack.Cards)
            {
                if (card != null)
                    SetLayerRecursively(card.gameObject, layer);
            }
        }

        internal void BeginSurfaceDrag(Vector2 screenPosition)
        {
            if (interactionCamera == null)
                return;

            // Complete the opening tween before the pointer takes ownership of
            // the board. Otherwise DOTween and the drag motion write the same
            // transform while card targets still refer to the hidden position.
            transform.DOKill(complete: true);
            StopStackMotionBeforeSurfaceDrag();
            SynchronizeStackTargetsToVisuals();
            Vector3 center = interactionCamera.WorldToScreenPoint(
                transform.position);
            surfaceDragScreenOffset =
                new Vector2(center.x, center.y) - screenPosition;
            surfaceCameraTargetPosition =
                interactionCamera.transform.position;
            surfaceCameraVelocity = Vector3.zero;
            hasSurfaceCameraTarget = true;
            isSurfaceDragging = true;
        }

        internal void DragSurface(Vector2 screenPosition)
        {
            if (interactionCamera == null)
                return;

            SetSurfaceCameraTarget(
                screenPosition + surfaceDragScreenOffset);
        }

        internal void EndSurfaceDrag()
        {
            isSurfaceDragging = false;
        }

        internal bool TryResolveStoragePlacement(
            CardInstance movingCard,
            Vector3 dropPosition,
            out Vector2 localPosition,
            out string tableStackId,
            out int tableStackOrder)
        {
            localPosition = Vector2.zero;
            tableStackId = null;
            tableStackOrder = 0;
            CardStack movingStack = movingCard?.Stack;
            if (movingStack == null)
                return false;

            Vector3 localDrop = ClampLocalPosition(
                transform.InverseTransformPoint(dropPosition),
                movingStack);
            Vector3 freePosition = transform.TransformPoint(new Vector3(
                localDrop.x,
                SurfaceHeight + CardSurfaceOffset,
                localDrop.z));
            BackpackCardProxy stackTarget = FindStackTarget(
                movingCard,
                movingStack,
                freePosition);
            if (stackTarget == null)
            {
                localPosition = new Vector2(localDrop.x, localDrop.z);
                return true;
            }

            CardStack targetStack = stackTarget.Card.Stack;
            Vector3 targetPosition = GetClampedWorldPosition(
                targetStack.TargetPosition,
                targetStack);
            Vector3 targetLocal =
                transform.InverseTransformPoint(targetPosition);
            localPosition = new Vector2(targetLocal.x, targetLocal.z);

            BackpackData backpack = BackpackService.Current;
            BackpackEntryData targetEntry =
                backpack?.Find(stackTarget.EntryId);
            tableStackId = targetEntry?.TableStackId;
            if (string.IsNullOrWhiteSpace(tableStackId))
            {
                tableStackId = Guid.NewGuid().ToString("N");
                PersistStackPlacement(
                    targetStack,
                    targetPosition,
                    tableStackId,
                    backpack);
            }
            tableStackOrder = targetStack.Cards.Count;
            return true;
        }

        internal void PlaceOnTable(
            BackpackCardProxy proxy,
            Vector3 dropPosition)
        {
            if (proxy?.Card?.Stack == null)
                return;

            CardStack movingStack = proxy.Card.Stack;
            Vector3 localDrop = ClampLocalPosition(
                transform.InverseTransformPoint(dropPosition),
                movingStack);
            Vector3 freePosition = transform.TransformPoint(new Vector3(
                localDrop.x,
                SurfaceHeight + CardSurfaceOffset,
                localDrop.z));

            BackpackCardProxy stackTarget = FindStackTarget(
                proxy.Card,
                movingStack,
                freePosition);

            if (stackTarget != null)
            {
                CardStack targetStack = stackTarget.Card.Stack;
                MergeVisualStacks(targetStack, movingStack);
                Vector3 clampedTargetPosition = GetClampedWorldPosition(
                    targetStack.TargetPosition,
                    targetStack);
                targetStack.SetTargetPosition(clampedTargetPosition);
                PersistStackPlacement(targetStack, clampedTargetPosition);
                return;
            }

            movingStack.SetTargetPosition(freePosition);
            PersistStackPlacement(movingStack, freePosition);
        }

        private BackpackCardProxy FindStackTarget(
            CardInstance movingCard,
            CardStack movingStack,
            Vector3 freePosition)
        {
            if (movingCard == null || movingStack == null)
                return null;

            float attachRadius = movingCard.Settings.AttachRadius;
            BackpackCardProxy nearest = null;
            float nearestSqrDistance = float.PositiveInfinity;

            foreach (BackpackCardProxy candidate in proxies.Values)
            {
                CardStack targetStack = candidate?.Card?.Stack;
                if (targetStack == null ||
                    targetStack == movingStack ||
                    !CanStackTogether(movingCard, candidate))
                    continue;

                Vector3 targetCenter = GetStackFootprintCenter(
                    targetStack,
                    targetStack.TargetPosition);
                if (!IsWithinNormalAttachRange(
                        freePosition,
                        targetStack,
                        attachRadius))
                    continue;

                float sqrDistance =
                    (targetCenter.Flatten() - freePosition.Flatten()).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance)
                    continue;

                nearest = candidate;
                nearestSqrDistance = sqrDistance;
            }

            return nearest;
        }

        private static bool IsWithinNormalAttachRange(
            Vector3 movingAnchor,
            CardStack targetStack,
            float attachRadius)
        {
            attachRadius = Mathf.Max(0f, attachRadius);
            if (targetStack?.Cards == null)
                return false;

            float attachRadiusSqr = attachRadius * attachRadius;
            foreach (CardInstance card in targetStack.Cards)
            {
                Collider collider = card != null
                    ? card.GetComponent<Collider>()
                    : null;
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                    continue;

                Vector3 closestPoint = collider.ClosestPoint(movingAnchor);
                if ((closestPoint - movingAnchor).sqrMagnitude <=
                    attachRadiusSqr)
                    return true;
            }

            return false;
        }

        private static Vector2 GetStackFootprintSize(CardStack stack)
        {
            if (stack == null)
                return Vector2.zero;
            return new Vector2(stack.Width, stack.FullDepth);
        }

        private static Vector3 GetStackFootprintCenter(
            CardStack stack,
            Vector3 targetPosition)
        {
            if (stack?.Cards == null || stack.Cards.Count == 0 ||
                stack.TopCard == null)
                return targetPosition;

            float bottomZ = targetPosition.z +
                (stack.Cards.Count - 1) * stack.TopCard.Settings.StackStep.z;
            targetPosition.z = (targetPosition.z + bottomZ) * 0.5f;
            return targetPosition;
        }

        private bool CanStackTogether(
            CardInstance movingCard,
            BackpackCardProxy target)
        {
            CardDefinition movingDefinition = movingCard?.Definition;
            CardDefinition targetDefinition =
                target?.Card?.Stack?.BottomCard?.Definition;
            return movingDefinition != null &&
                targetDefinition != null &&
                CardManager.Instance != null &&
                CardManager.Instance.CanStack(
                    movingDefinition,
                    targetDefinition);
        }

        private static void MergeVisualStacks(
            CardStack target,
            CardStack source)
        {
            if (target == null || source?.Cards == null || target == source)
                return;

            foreach (CardInstance card in source.Cards.ToList())
                target.AddCard(card);
            source.Cards.Clear();
        }

        internal void ReturnToSlot(BackpackCardProxy proxy)
        {
            if (proxy == null)
                return;

            Rebuild(BackpackService.Current);
        }

        internal void Detach(BackpackCardProxy proxy)
        {
            if (proxy == null)
                return;

            proxies.Remove(proxy.EntryId);
            proxy.Card?.transform.SetParent(null, true);
            if (Application.isPlaying)
                Destroy(proxy);
            else
                DestroyImmediate(proxy);
        }

        private void BuildVisuals()
        {
            if (surfaceCollider != null)
                return;

            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Backpack3DSurface";
            surface.transform.SetParent(transform, false);
            surface.transform.localPosition = new Vector3(0f, BaseThickness * 0.5f, 0f);
            surface.transform.localScale = new Vector3(
                DefaultSize.x,
                BaseThickness,
                DefaultSize.y);

            surfaceCollider = surface.GetComponent<BoxCollider>();
            BackpackBoardDragSurface dragSurface =
                surface.AddComponent<BackpackBoardDragSurface>();
            dragSurface.Bind(this);
            MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            if (shader != null)
            {
                baseMaterial = new Material(shader)
                {
                    color = new Color(0.68f, 0.74f, 0.77f, 1f)
                };
                renderer.sharedMaterial = baseMaterial;
            }

            var cardsObject = new GameObject("Backpack3DCards");
            cardsObject.transform.SetParent(transform, false);
            cardsRoot = cardsObject.transform;
        }

        private bool TryRaycastSurface(
            Vector2 screenPosition,
            out RaycastHit hit)
        {
            hit = default;
            if (!gameObject.activeInHierarchy || surfaceCollider == null ||
                interactionCamera == null)
                return false;

            Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
            return surfaceCollider.Raycast(ray, out hit, 1000f);
        }

        private void PositionInCameraView()
        {
            if (interactionCamera == null)
                return;

            if (!TryCalculateSurfaceCameraTarget(
                    new Vector2(
                        viewportAnchor.x * Screen.width,
                        viewportAnchor.y * Screen.height),
                    out Vector3 cameraPosition,
                    out _))
                return;

            interactionCamera.transform.position = cameraPosition;
            surfaceCameraTargetPosition = cameraPosition;
            surfaceCameraVelocity = Vector3.zero;
            hasSurfaceCameraTarget = false;
        }

        private void SetSurfaceCameraTarget(Vector2 desiredScreenPoint)
        {
            if (!TryCalculateSurfaceCameraTarget(
                    desiredScreenPoint,
                    out Vector3 cameraPosition,
                    out Vector2 clampedScreenPoint))
                return;

            surfaceCameraTargetPosition = cameraPosition;
            hasSurfaceCameraTarget = true;
            viewportAnchor = new Vector2(
                Mathf.Clamp01(
                    clampedScreenPoint.x / Mathf.Max(1f, Screen.width)),
                Mathf.Clamp01(
                    clampedScreenPoint.y / Mathf.Max(1f, Screen.height)));
        }

        private bool TryCalculateSurfaceCameraTarget(
            Vector2 desiredScreenPoint,
            out Vector3 cameraPosition,
            out Vector2 clampedScreenPoint)
        {
            cameraPosition = Vector3.zero;
            clampedScreenPoint = desiredScreenPoint;
            if (interactionCamera == null)
                return false;

            clampedScreenPoint =
                ClampScreenPointToSafeArea(desiredScreenPoint);
            Ray ray =
                interactionCamera.ScreenPointToRay(clampedScreenPoint);
            var boardPlane = new Plane(Vector3.up, transform.position);
            if (!boardPlane.Raycast(ray, out float distance))
                return false;

            Vector3 projectedBoardPoint = ray.GetPoint(distance);
            Vector3 inversePan =
                transform.position - projectedBoardPoint;
            inversePan.y = 0f;
            cameraPosition =
                interactionCamera.transform.position + inversePan;
            return true;
        }

        private void UpdateSurfaceCameraMotion(float unscaledDeltaTime)
        {
            if (!hasSurfaceCameraTarget ||
                interactionCamera == null ||
                unscaledDeltaTime <= 0f)
                return;

            interactionCamera.transform.position = Vector3.SmoothDamp(
                interactionCamera.transform.position,
                surfaceCameraTargetPosition,
                ref surfaceCameraVelocity,
                SurfaceCameraSmoothTime,
                Mathf.Infinity,
                unscaledDeltaTime);

            if (!isSurfaceDragging &&
                (interactionCamera.transform.position -
                 surfaceCameraTargetPosition).sqrMagnitude < 0.000001f)
            {
                interactionCamera.transform.position =
                    surfaceCameraTargetPosition;
                surfaceCameraVelocity = Vector3.zero;
                hasSurfaceCameraTarget = false;
            }
        }

        private void SynchronizeStackTargetsToVisuals()
        {
            synchronizedStacks.Clear();
            foreach (BackpackCardProxy proxy in proxies.Values)
            {
                CardStack stack = proxy?.Card?.Stack;
                if (stack == null || !synchronizedStacks.Add(stack))
                    continue;

                stack.SynchronizeTargetWithParentMotion(
                    GetVisualStackAnchor(stack));
            }
        }

        private void StopStackMotionBeforeSurfaceDrag()
        {
            synchronizedStacks.Clear();
            foreach (BackpackCardProxy proxy in proxies.Values)
            {
                CardStack stack = proxy?.Card?.Stack;
                if (stack == null || !synchronizedStacks.Add(stack))
                    continue;

                stack.StopMovementForParentDrag();
            }
        }

        private static Vector3 GetVisualStackAnchor(CardStack stack)
        {
            return stack?.Cards != null &&
                stack.Cards.Count > 0 &&
                stack.Cards[0] != null
                    ? stack.Cards[0].transform.position
                    : stack?.TargetPosition ?? Vector3.zero;
        }

        private Vector2 ClampScreenPointToSafeArea(Vector2 desiredCenter)
        {
            if (interactionCamera == null || Screen.width <= 0 || Screen.height <= 0)
                return desiredCenter;

            Bounds screenBounds = GetProjectedScreenBounds();
            Vector3 currentCenter3 = interactionCamera.WorldToScreenPoint(
                transform.position);
            Vector2 currentCenter = new(currentCenter3.x, currentCenter3.y);
            float leftExtent = currentCenter.x - screenBounds.min.x;
            float rightExtent = screenBounds.max.x - currentCenter.x;
            float bottomExtent = currentCenter.y - screenBounds.min.y;
            float topExtent = screenBounds.max.y - currentCenter.y;

            Rect safeArea = Screen.safeArea;
            float safeRight = Mathf.Min(
                safeArea.xMax,
                Screen.width * 0.79f);
            desiredCenter.x = ClampCenterAxis(
                desiredCenter.x,
                safeArea.xMin + 12f + leftExtent,
                safeRight - 12f - rightExtent);
            desiredCenter.y = ClampCenterAxis(
                desiredCenter.y,
                safeArea.yMin + 12f + bottomExtent,
                safeArea.yMax - 12f - topExtent);
            return desiredCenter;
        }

        private Bounds GetProjectedScreenBounds()
        {
            float halfWidth = DefaultSize.x * 0.5f;
            float halfDepth = DefaultSize.y * 0.5f;
            Vector3[] corners =
            {
                new(-halfWidth, SurfaceHeight, -halfDepth),
                new(-halfWidth, SurfaceHeight, halfDepth),
                new(halfWidth, SurfaceHeight, -halfDepth),
                new(halfWidth, SurfaceHeight, halfDepth)
            };
            Vector3 first = interactionCamera.WorldToScreenPoint(
                transform.TransformPoint(corners[0]));
            var bounds = new Bounds(first, Vector3.zero);
            for (int index = 1; index < corners.Length; index++)
            {
                bounds.Encapsulate(interactionCamera.WorldToScreenPoint(
                    transform.TransformPoint(corners[index])));
            }
            return bounds;
        }

        private static float ClampCenterAxis(
            float value,
            float minimum,
            float maximum)
        {
            return minimum <= maximum
                ? Mathf.Clamp(value, minimum, maximum)
                : (minimum + maximum) * 0.5f;
        }

        private Vector3 GetTableWorldPosition(BackpackEntryData entry)
        {
            Vector2 local = entry.HasTablePosition
                ? new Vector2(entry.TablePositionX, entry.TablePositionZ)
                : GetDefaultTablePosition(entry.SlotIndex);
            Vector3 clamped = ClampLocalPosition(
                new Vector3(local.x, 0f, local.y),
                null);
            return transform.TransformPoint(new Vector3(
                clamped.x,
                SurfaceHeight + CardSurfaceOffset,
                clamped.z));
        }

        private static Vector2 GetDefaultTablePosition(int index)
        {
            float angle = Mathf.Abs(index) * 137.508f * Mathf.Deg2Rad;
            float radius = Mathf.Min(1.35f, 0.2f + 0.42f * Mathf.Sqrt(index + 1f));
            return new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * 0.72f);
        }

        private Vector3 ClampLocalPosition(Vector3 local, CardStack stack)
        {
            float halfCardWidth = stack != null
                ? Mathf.Max(0.45f, stack.Width * 0.5f)
                : 0.45f;
            local.x = ClampFootprintAnchor(
                local.x,
                DefaultSize.x * 0.5f,
                -halfCardWidth,
                halfCardWidth);

            float halfCardDepth = stack?.TopCard != null
                ? Mathf.Max(0.55f, stack.TopCard.Size.y * 0.5f)
                : 0.55f;
            float stackOffset = stack?.TopCard != null
                ? (stack.Cards.Count - 1) *
                    stack.TopCard.Settings.StackStep.z
                : 0f;
            float negativeExtent = Mathf.Min(
                -halfCardDepth,
                stackOffset - halfCardDepth);
            float positiveExtent = Mathf.Max(
                halfCardDepth,
                stackOffset + halfCardDepth);
            local.z = ClampFootprintAnchor(
                local.z,
                DefaultSize.y * 0.5f,
                negativeExtent,
                positiveExtent);
            return local;
        }

        private static float ClampFootprintAnchor(
            float anchor,
            float surfaceHalfExtent,
            float negativeExtent,
            float positiveExtent)
        {
            float minimum = -surfaceHalfExtent - negativeExtent;
            float maximum = surfaceHalfExtent - positiveExtent;
            if (minimum <= maximum)
                return Mathf.Clamp(anchor, minimum, maximum);

            // The stack is larger than the surface. Center its footprint so the
            // unavoidable overflow is shared equally between both edges.
            return -(negativeExtent + positiveExtent) * 0.5f;
        }

        private Vector3 GetClampedWorldPosition(
            Vector3 worldPosition,
            CardStack stack)
        {
            Vector3 local = ClampLocalPosition(
                transform.InverseTransformPoint(worldPosition),
                stack);
            return transform.TransformPoint(new Vector3(
                local.x,
                SurfaceHeight + CardSurfaceOffset,
                local.z));
        }

        private void PersistStackPlacement(
            CardStack stack,
            Vector3 worldPosition,
            string stackId = null,
            BackpackData backpack = null)
        {
            backpack ??= BackpackService.Current;
            if (stack?.Cards == null || backpack == null)
                return;

            Vector3 local = transform.InverseTransformPoint(worldPosition);
            if (string.IsNullOrWhiteSpace(stackId))
                stackId = Guid.NewGuid().ToString("N");
            for (int index = 0; index < stack.Cards.Count; index++)
            {
                BackpackCardProxy proxy =
                    stack.Cards[index]?.GetComponent<BackpackCardProxy>();
                if (proxy == null)
                    continue;

                backpack.TrySetTablePlacement(
                    proxy.EntryId,
                    local.x,
                    local.z,
                    stackId,
                    index);
            }
        }

        private void ClearProxies()
        {
            foreach (BackpackCardProxy proxy in proxies.Values.ToList())
                DestroyProxyCard(proxy);
            proxies.Clear();
        }

        private static void DestroyProxyCard(BackpackCardProxy proxy)
        {
            CardStack stack = proxy?.Card?.Stack;
            if (stack == null)
                return;

            foreach (CardInstance stackCard in stack.Cards.ToList())
            {
                stack.RemoveCard(stackCard);
                if (Application.isPlaying)
                    Destroy(stackCard.gameObject);
                else
                    DestroyImmediate(stackCard.gameObject);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }
    }
}
