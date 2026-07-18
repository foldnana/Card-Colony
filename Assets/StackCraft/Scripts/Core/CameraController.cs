using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    public class CameraController : MonoBehaviour
    {
        [Header("Object References")]
        [SerializeField, Tooltip("The transform of the pivot camera that moves and zooms with this controller.")]
        private Transform cameraTransform;

        [Header("Pan Settings")]
        [SerializeField, Tooltip("How fast the camera moves across the board when dragging.")]
        private float panSpeed = 0.01f;

        [SerializeField, Tooltip("Smoothing time used when interpolating camera movement toward the target position.")]
        private float smoothTime = 0.15f;

        [SerializeField, Tooltip("How far past the board edge the camera can scroll.")]
        private float panPadding = 0.5f;

        [Header("Zoom Settings (Distance)")]
        [SerializeField, Tooltip("How fast the camera zooms in and out when scrolling.")]
        private float zoomSpeed = 1f;

        [SerializeField, Tooltip("Minimum allowed zoom-in distance from the ground.")]
        private float minDistance = 5f;

        [SerializeField, Tooltip("Maximum allowed zoom-out distance from the ground.")]
        private float maxDistance = 20f;

        private bool isDragging;
        private Vector3 dragOrigin;
        private Vector3 targetPos;
        private Vector3 velocity;

        // Movement Clamps
        private Vector2 clampMin = new Vector2(-10, -5);
        private Vector2 clampMax = new Vector2(10, 5);

        private void Awake()
        {
            targetPos = transform.position;
        }

        private void Start()
        {
            if (Board.Instance != null)
            {
                Board.Instance.OnBoundsUpdated += UpdateMovementClamps;
                UpdateMovementClamps(Board.Instance.WorldBounds);
            }
        }

        private void OnDestroy()
        {
            if (Board.Instance != null)
            {
                Board.Instance.OnBoundsUpdated -= UpdateMovementClamps;
            }
        }

        private void UpdateMovementClamps(Bounds boardBounds)
        {
            // X Axis
            clampMin.x = boardBounds.min.x - panPadding;
            clampMax.x = boardBounds.max.x + panPadding;

            // Z Axis
            clampMin.y = boardBounds.min.z - panPadding;
            clampMax.y = boardBounds.max.z + panPadding;

            // Immediately clamp current target to ensure we don't get stuck outside if board shrinks.
            ClampTargetPosition();
        }

        private void Update()
        {
            if (!InputManager.Instance.IsInputEnabled) return;

            HandlePan();
            HandleZoom();

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref velocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        }

        private void HandlePan()
        {
            if ((Input.GetMouseButtonDown(0) && !IsPointerBlocked()) || Input.GetMouseButtonDown(2))
            {
                dragOrigin = Input.mousePosition;
                isDragging = true;
            }

            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(2))
                isDragging = false;

            if (isDragging)
            {
                Vector3 delta = Input.mousePosition - dragOrigin;
                Vector3 move = new Vector3(-delta.x, 0, -delta.y) * panSpeed * (transform.position.y / 10f);

                targetPos += move;
                ClampTargetPosition();

                dragOrigin = Input.mousePosition;
            }
        }

        private void ClampTargetPosition()
        {
            // Ensure clampMin/Max have been set (Board might not be ready in frame 1).
            if (Mathf.Approximately(clampMin.x, clampMax.x)) return;

            targetPos.x = Mathf.Clamp(targetPos.x, clampMin.x, clampMax.x);
            targetPos.z = Mathf.Clamp(targetPos.z, clampMin.y, clampMax.y);
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector3 forward = cameraTransform.forward;
                Vector3 newPos = targetPos + forward * (scroll * zoomSpeed);

                float distance = newPos.y / Mathf.Cos(Mathf.Deg2Rad * (90f - cameraTransform.eulerAngles.x));
                if (distance >= minDistance && distance <= maxDistance)
                {
                    targetPos = newPos;
                    // Re-clamp after zoom because zooming moves the camera position in X/Z too.
                    ClampTargetPosition();
                }
            }
        }

        private bool IsPointerBlocked()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>
        /// Smoothly moves the camera to a target world position.
        /// </summary>
        /// <param name="target">The world position to focus on.</param>
        /// <param name="duration">Tween duration in seconds.</param>
        /// <returns>Coroutine yielding until movement finishes.</returns>
        public IEnumerator MoveTo(Vector3 target, float duration = 0.5f)
        {
            isDragging = false;
            dragOrigin = Input.mousePosition;

            float desiredDistance = Mathf.Lerp(maxDistance, minDistance, 0.8f);
            Vector3 offset = -cameraTransform.forward * desiredDistance;
            Vector3 newCameraPosition = target + offset;

            yield return transform.DOMove(newCameraPosition, duration)
                .SetUpdate(true)
                .WaitForCompletion();

            targetPos = newCameraPosition;
        }

        /// <summary>
        /// Shakes the camera additively.
        /// </summary>
        /// <param name="duration">How long the shake should last.</param>
        /// <param name="strength">How intense the shake should be.</param>
        public void Shake(float duration = 0.3f, float strength = 0.1f)
        {
            cameraTransform.DOShakePosition(duration, strength)
                .SetUpdate(true);
        }
    }
}
