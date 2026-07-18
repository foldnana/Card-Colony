using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(RectTransform))]
    public class CombatRect : MonoBehaviour
    {
        public static readonly Vector2 Margin = new Vector2(0.1f, 0.1f);

        public RectTransform Rect { get; private set; }

        private Vector2 cellSize;

        private List<CardInstance> _attackers;
        private List<CardInstance> _defenders;
        private readonly Dictionary<CardInstance, Vector3> _cardPositions = new();

        /// <summary>
        /// Sets up the combat area by defining the participating units, calculating the required size,
        /// clamping the area to the board limits, resolving overlaps with other stacks, and arranging the units.
        /// </summary>
        /// <param name="attackers">The list of CardInstances that will occupy the attacking row.</param>
        /// <param name="defenders">The list of CardInstances that will occupy the defending row.</param>
        public void Initialize(List<CardInstance> attackers, List<CardInstance> defenders)
        {
            _attackers = attackers;
            _defenders = defenders;

            var firstCard = attackers[0];
            cellSize = firstCard.Size + Margin;

            Rect = GetComponent<RectTransform>();
            AdjustRectSize();

            var allUnits = _attackers
                .Select(a => a.transform.position)
                .Concat(_defenders.Select(d => d.transform.position));

            Vector3 averagePosition = allUnits.Aggregate((a, b) => a + b) / allUnits.Count();
            transform.position = averagePosition.Flatten();
            transform.position = Board.Instance.ClampRectTransformToBoard(Rect);
            CardManager.Instance.ResolveOverlaps(this);

            ArrangeCards();
        }

        /// <summary>
        /// Calculates the necessary RectTransform size to accommodate the given number of attackers and defenders
        /// side-by-side in two rows, including margins.
        /// </summary>
        /// <param name="attackerCount">The number of units in the attacker row.</param>
        /// <param name="defenderCount">The number of units in the defender row.</param>
        /// <param name="cardSize">The base size (width/depth) of a single card unit.</param>
        /// <param name="margin">The extra spacing to add around each card.</param>
        /// <returns>A Vector2 representing the required total width and height of the combat area.</returns>
        public static Vector2 CalculateRequiredSize(int attackerCount, int defenderCount, Vector2 cardSize, Vector2 margin)
        {
            Vector2 cellSize = cardSize + margin;
            float width = cellSize.x * Mathf.Max(attackerCount, defenderCount);
            float height = cellSize.y * 2;
            return new Vector2(width, height) + (margin * 2);
        }

        private void AdjustRectSize()
        {
            var firstCard = _attackers.FirstOrDefault() ?? _defenders.FirstOrDefault();
            if (firstCard == null) return;

            Rect.sizeDelta = CalculateRequiredSize(
                _attackers.Count, _defenders.Count, firstCard.Size, Margin
            );
        }

        /// <summary>
        /// Recalculates the required size of the <see cref="CombatRect"/> and repositions all units within it.
        /// </summary>
        /// <remarks>
        /// This is typically called after a unit is added or removed from the combat.
        /// </remarks>
        public void UpdateLayout()
        {
            AdjustRectSize();
            ArrangeCards();
        }

        private void ArrangeCards()
        {
            ArrangeRow(_attackers, -cellSize.y * 0.5f);  // Top Row
            ArrangeRow(_defenders, +cellSize.y * 0.5f);  // Bottom Row

            _cardPositions.Clear();

            foreach (var card in _attackers.Concat(_defenders))
            {
                _cardPositions.TryAdd(card, card.transform.position);
            }
        }

        private void ArrangeRow(List<CardInstance> rowCards, float rowY)
        {
            if (rowCards.Count == 0) return;

            float rowWidth = (rowCards.Count - 1) * cellSize.x;
            float offset = -rowWidth / 2f;

            for (int i = 0; i < rowCards.Count; i++)
            {
                Vector3 localSlot = new Vector3(offset + i * cellSize.x, rowY, 0);
                Vector3 worldSlot = Rect.TransformPoint(localSlot);
                rowCards[i].SetTargetInstant(worldSlot, forceGround: true);
            }
        }

        /// <summary>
        /// Checks if a given world position falls within the horizontal bounds of this combat area.
        /// </summary>
        /// <param name="worldPosition">The world coordinate to check.</param>
        /// <returns>True if the position is inside the RectTransform's bounds; otherwise, false.</returns>
        public bool IsPositionInside(Vector3 worldPosition)
        {
            if (Rect == null) return false;

            Vector2 localPoint = Rect.InverseTransformPoint(worldPosition);
            return Rect.rect.Contains(localPoint);
        }

        /// <summary>
        /// Animates a card back to its designated layout position within the combat area.
        /// </summary>
        /// <remarks>
        /// This is used to snap a card back into place if it was temporarily lifted or moved.
        /// </remarks>
        /// <param name="card">The card instance to reposition.</param>
        public void RepositionCard(CardInstance card)
        {
            if (_cardPositions.TryGetValue(card, out Vector3 targetPosition))
            {
                card.SetTargetAnimated(targetPosition);
            }
        }

        /// <summary>
        /// Retrieves the target world position assigned to a specific card within the combat layout.
        /// </summary>
        /// <param name="card">The card instance whose layout position is requested.</param>
        /// <returns>The pre-calculated layout world position, or the card's current world position if not found.</returns>
        public Vector3 GetLayoutPosition(CardInstance card)
        {
            if (_cardPositions.TryGetValue(card, out Vector3 layoutPosition))
            {
                return layoutPosition;
            }

            return card.transform.position;
        }

        /// <summary>
        /// Cleans up the combat area by destroying the entire <see cref="CombatRect"/> GameObject.
        /// </summary>
        public void Close()
        {
            Destroy(gameObject);
        }
    }
}
