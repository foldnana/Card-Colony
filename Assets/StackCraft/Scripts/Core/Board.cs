using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class Board : MonoBehaviour
    {
        public static Board Instance { get; private set; }

        public event System.Action<Bounds> OnBoundsUpdated;

        [Header("Restricted Area")]
        [SerializeField, Tooltip("Space at the top (Z axis) where cards cannot be placed.")]
        private float topMargin = 1.5f;

        public float TopMargin => topMargin;
        public Bounds WorldBounds => currentBounds;

        private SkinnedMeshRenderer skinnedMesh;
        private Mesh bakedMesh;
        private Bounds currentBounds;
        private int totalBoost;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            skinnedMesh = GetComponent<SkinnedMeshRenderer>();
            bakedMesh = new Mesh();

            UpdateCurrentBounds();
        }

        private void Start()
        {
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnStatsChanged += HandleStatsChanged;
                HandleStatsChanged(CardManager.Instance.GetStatsSnapshot());
            }
        }

        private void OnDestroy()
        {
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnStatsChanged -= HandleStatsChanged;
            }

            if (bakedMesh != null)
            {
                Destroy(bakedMesh);
            }
        }

        private void HandleStatsChanged(StatsSnapshot stats)
        {
            if (stats.TotalBoost != totalBoost)
            {
                bool isBoardShrinking = stats.TotalBoost < totalBoost;

                totalBoost = Mathf.Min(stats.TotalBoost, 100);
                skinnedMesh.SetBlendShapeWeight(0, totalBoost);

                UpdateCurrentBounds();

                if (isBoardShrinking && CardManager.Instance != null)
                {
                    CardManager.Instance.EnforceBoardLimits();
                }
            }
        }

        [ContextMenu("Update Current Bounds")]
        private void UpdateCurrentBounds()
        {
            if (skinnedMesh == null)
                skinnedMesh = GetComponent<SkinnedMeshRenderer>();

            if (bakedMesh == null)
                bakedMesh = new Mesh();

            skinnedMesh.BakeMesh(bakedMesh);
            bakedMesh.RecalculateBounds();

            var localBounds = bakedMesh.bounds;
            var center = transform.TransformPoint(localBounds.center);
            var extents = Vector3.Scale(localBounds.extents, transform.lossyScale);

            currentBounds = new Bounds(center, extents * 2f);

            OnBoundsUpdated?.Invoke(currentBounds);
        }

        /// <summary>
        /// Constrains the movement of a CardStack's root position to ensure its entire physical footprint
        /// remains within the playable world boundaries of the board.
        /// </summary>
        /// <remarks>
        /// The method calculates the effective minimum and maximum coordinates by factoring in the stack's
        /// width and depth relative to the board's Bounds, and then clamps the requested position.
        /// This check enforces the X and Z boundaries of the board but does not specifically handle the
        /// restricted top margin (header area).
        /// </remarks>
        /// <param name="position">The desired world position for the stack.</param>
        /// <param name="stack">The <see cref="CardStack"/> whose dimensions are used for the clamping offset.</param>
        /// <returns>A Vector3 that is guaranteed to be within the board's physical boundaries.</returns>
        public Vector3 ClampToBounds(Vector3 position, CardStack stack)
        {
            if (stack == null) return position;

            float cardWidth = stack.Width;
            float stackDepth = stack.FullDepth;
            float topCardHalfDepth = stack.TopCard.Size.y * 0.5f;

            var min = currentBounds.min + new Vector3(cardWidth * 0.5f, 0, stackDepth - topCardHalfDepth);
            var max = currentBounds.max - new Vector3(cardWidth * 0.5f, 0, topCardHalfDepth);

            return new Vector3(
                Mathf.Clamp(position.x, min.x, max.x),
                position.y,
                Mathf.Clamp(position.z, min.z, max.z)
            );
        }

        /// <summary>
        /// Applies the complete set of placement rules to a CardStack's potential position, 
        /// ensuring it is both within the board's physical boundaries and outside the restricted top margin.
        /// </summary>
        /// <remarks>
        /// This method first uses ClampToBounds to handle the lateral and bottom bounds. 
        /// It then checks if the card's top edge overlaps the restricted margin area and snaps 
        /// the position down (negative Z) if necessary. The result is also flattened to Y=0.
        /// </remarks>
        /// <param name="position">The desired world position for the stack.</param>
        /// <param name="stack">The <see cref="CardStack"/> whose dimensions are used for the placement check.</param>
        /// <returns>A corrected, final Vector3 position that adheres to all board placement rules.</returns>
        public Vector3 EnforcePlacementRules(Vector3 position, CardStack stack)
        {
            if (stack == null) return position;

            var clamped = ClampToBounds(position, stack);

            float restrictedStart = currentBounds.max.z - topMargin;
            float cardTopEdge = clamped.z + (stack.TopCard.Size.y * 0.5f);

            if (cardTopEdge > restrictedStart)
            {
                clamped.z = restrictedStart - (stack.TopCard.Size.y * 0.5f);
            }

            return clamped.Flatten();
        }

        /// <summary>
        /// Checks if a given world position is a valid placement location for a card or stack on the board.
        /// </summary>
        /// <remarks>
        /// A position is valid if it is entirely within the physical boundaries of the board
        /// and does not overlap with the restricted top margin area (header). The stack's dimensions
        /// are accounted for when performing the boundary checks.
        /// </remarks>
        /// <param name="position">The world position to validate.</param>
        /// <param name="stack">The <see cref="CardStack"/> to check. If null, the check assumes a point (0 size).</param>
        /// <returns>True if the position is a valid placement location; otherwise, false.</returns>
        public bool IsPointValid(Vector3 position, CardStack stack = null)
        {
            if (currentBounds.extents == Vector3.zero)
                UpdateCurrentBounds();

            // If we have a stack, account for its size
            float cardHalfWidth = stack != null ? stack.Width * 0.5f : 0f;
            float cardHalfDepth = stack != null ? stack.TopCard.Size.y * 0.5f : 0f;

            // Effective min/max bounds considering card footprint
            var min = currentBounds.min + new Vector3(cardHalfWidth, 0, cardHalfDepth);
            var max = currentBounds.max - new Vector3(cardHalfWidth, 0, cardHalfDepth);

            // First check: inside overall board bounds
            if (position.x < min.x || position.x > max.x ||
                position.z < min.z || position.z > max.z)
            {
                return false;
            }

            // Second check: not in restricted margin (header)
            float restrictedStart = currentBounds.max.z - topMargin;
            float cardTopEdge = position.z + cardHalfDepth;
            if (cardTopEdge > restrictedStart)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates a corrected world position for a RectTransform to ensure its entire area
        /// stays within the board's playable zone.
        /// </summary>
        /// <remarks>
        /// This method is primarily used for positioning UI elements like combat rectangles. It converts
        /// the RectTransform's local dimensions to world space, clamps the resulting footprint
        /// within the board's physical boundaries, and enforces the restricted top margin rule.
        /// </remarks>
        /// <param name="rect">The RectTransform whose world position needs to be corrected.</param>
        /// <returns>A corrected Vector3 position that keeps the RectTransform fully within the valid board area.</returns>
        public Vector3 ClampRectTransformToBoard(RectTransform rect)
        {
            if (rect == null)
                return Vector3.zero;

            if (currentBounds.extents == Vector3.zero)
                UpdateCurrentBounds();

            // Convert rect size from local to world
            Vector3 rectSize = rect.rect.size;
            Vector3 worldSize = Vector3.Scale(rectSize, rect.lossyScale);

            float halfWidth = worldSize.x * 0.5f;
            float halfDepth = worldSize.y * 0.5f; // RectTransform height: Z axis

            Vector3 position = rect.position;

            // Effective min/max considering rect footprint
            var min = currentBounds.min + new Vector3(halfWidth, 0, halfDepth);
            var max = currentBounds.max - new Vector3(halfWidth, 0, halfDepth);

            // Clamp to board
            Vector3 clamped = new Vector3(
                Mathf.Clamp(position.x, min.x, max.x),
                0f, // keep board flat
                Mathf.Clamp(position.z, min.z, max.z)
            );

            // Handle restricted top margin
            float restrictedStart = currentBounds.max.z - topMargin;
            float rectTopEdge = clamped.z + halfDepth;

            if (rectTopEdge > restrictedStart)
            {
                clamped.z = restrictedStart - halfDepth;
            }

            return clamped;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (currentBounds.extents == Vector3.zero)
            {
                UpdateCurrentBounds();
            }

            Vector3 boardCenter = currentBounds.center;
            Vector3 boardSize = new Vector3(currentBounds.size.x, 0.01f, currentBounds.size.z);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(boardCenter, boardSize);

            Vector3 marginCenter = boardCenter + new Vector3(0, 0, (currentBounds.size.z * 0.5f) - (topMargin * 0.5f));
            Vector3 marginSize = new Vector3(currentBounds.size.x - 0.05f, 0.01f, topMargin - 0.05f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(marginCenter, marginSize);
        }
#endif
    }
}
