using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class CardStack
    {
        /// <summary>
        /// A sentinel instance representing a directive to refuse all stacking attempts.
        /// </summary>
        public static readonly CardStack RefuseAll = new CardStack();

        public List<CardInstance> Cards { get; private set; }
        public Vector3 TargetPosition { get; private set; }
        public long PresentationOrder { get; private set; }
        public int OverlapLayer { get; private set; }
        public float PresentationBaseY { get; private set; }
        public bool IsLocked { get; set; }
        public bool IsAnchored => Cards != null && Cards.Exists(card =>
            card?.Definition != null &&
            !card.Definition.PlayerDraggable);
        public bool IsBeingDragged => Cards != null && Cards.Exists(card =>
            card != null && card.IsBeingDragged);
        public bool InheritsPresentationFromParentCard =>
            Cards != null && Cards.Exists(card =>
            {
                Transform parent = card != null ? card.transform.parent : null;
                return parent != null &&
                    parent.GetComponentInParent<CardInstance>() != null;
            });

        public CardInstance TopCard => Cards.Count > 0 ? Cards[0] : null;
        public CardInstance BottomCard => Cards.Count > 0 ? Cards[Cards.Count - 1] : null;

        public bool IsCrafting { get; private set; }

        /// <summary>
        /// Sets the internal flag indicating whether this stack is currently engaged in a crafting process.
        /// </summary>
        /// <param name="state">True if the stack is crafting; false otherwise.</param>
        public void SetCraftingState(bool state) => IsCrafting = state;

        public float Width
        {
            get
            {
                if (TopCard == null) return 0f;
                return TopCard.Size.x;
            }
        }

        public float FullDepth
        {
            get
            {
                if (Cards.Count == 0 || TopCard == null) return 0f;

                float cardDepth = TopCard.Size.y;
                float stackOffsetDepth = (Cards.Count - 1) * Mathf.Abs(TopCard.Settings.StackStep.z);

                return cardDepth + stackOffsetDepth;
            }
        }

        public float PresentationTopY
        {
            get
            {
                if (Cards.Count == 0 || TopCard == null)
                    return PresentationBaseY;

                return TargetPosition.y + PresentationBaseY +
                    (Cards.Count - 1) * Mathf.Abs(TopCard.Settings.StackStep.y);
            }
        }

        private CardStack() { }

        /// <summary>
        /// Initializes a new <see cref="CardStack"/> containing a single initial card.
        /// </summary>
        /// <param name="initialCard">The card that will be the first member of this stack.</param>
        /// <param name="position">The initial world position of the stack's base.</param>
        public CardStack(CardInstance initialCard, Vector3 position)
        {
            Cards = new List<CardInstance> { initialCard };
            initialCard.Stack = this;
            SetTargetPosition(position, instant: true);
        }

        public void SetPresentationOrder(long order)
        {
            PresentationOrder = order;
        }

        public void SetPresentationLayer(
            int layer,
            float baseY,
            bool instant = false)
        {
            int safeLayer = Mathf.Max(0, layer);
            float safeBaseY = Mathf.Max(0f, baseY);
            if (OverlapLayer == safeLayer &&
                Mathf.Approximately(PresentationBaseY, safeBaseY))
                return;

            OverlapLayer = safeLayer;
            PresentationBaseY = safeBaseY;
            ApplyCardLayout(
                instant,
                instant || TopCard?.Settings == null
                    ? null
                    : TopCard.Settings.LayerSettleDuration);
        }

        /// <summary>
        /// Adds a card to the top of the stack and updates the card's reference to this stack.
        /// </summary>
        /// <param name="card">The card instance to be added.</param>
        public void AddCard(CardInstance card)
        {
            Cards.Add(card);
            card.Stack = this;
        }

        /// <summary>
        /// Removes a card from the stack. If the stack becomes empty, it is automatically
        /// unregistered from the <see cref="CardManager"/>.
        /// </summary>
        /// <param name="card">The card instance to be removed.</param>
        public void RemoveCard(CardInstance card)
        {
            if (Cards.Remove(card))
            {
                card.Stack = null;

                if (Cards.Count == 0)
                {
                    CardManager.Instance?.UnregisterStack(this);
                }
                else
                {
                    SetTargetPosition(TargetPosition);
                    CardManager.Instance?.ResolvePresentationLayers();
                }
            }
        }

        /// <summary>
        /// Merges all cards from another stack into this stack.
        /// </summary>
        /// <remarks>
        /// Before a regular merge, this checks if the bottom card of the current stack
        /// has a component that can handle the stacking interaction (<see cref="IOnStackable"/>).
        /// If the interaction is handled, the merge stops; otherwise, the cards are transferred.
        /// </remarks>
        /// <param name="stackToMerge">The stack whose cards will be moved into the current stack.</param>
        public void MergeWith(CardStack stackToMerge)
        {
            // Check if the bottom card has ANY component that can handle this.
            var stackable = this.BottomCard?.GetComponent<IOnStackable>();

            if (stackable != null)
            {
                bool handled = stackable.OnStack(stackToMerge);
                if (handled)
                {
                    // The interaction was handled (e.g., coins deposited).
                    // If the stack is now empty, it will be unregistered.
                    // Stop the merge.
                    return;
                }
            }

            // --- Regular Merge Logic ---
            // (If stackable was null, or it returned false)
            foreach (var card in stackToMerge.Cards)
            {
                AddCard(card);
            }
            if (stackToMerge.PresentationOrder > PresentationOrder)
                PresentationOrder = stackToMerge.PresentationOrder;
            stackToMerge.Cards.Clear();
        }

        /// <summary>
        /// Splits the current stack into two, creating a new stack starting from the specified card
        /// and including all cards above it.
        /// </summary>
        /// <param name="card">The card instance where the split should occur. This card will become the base of the new stack.</param>
        /// <returns>A new <see cref="CardStack"/> instance containing the split-off cards, or null if the card was the top card or not found.</returns>
        public CardStack SplitAt(CardInstance card)
        {
            int splitIndex = Cards.IndexOf(card);
            if (splitIndex < 0 || splitIndex == 0) return null;

            Vector3 logicalPosition = new Vector3(
                card.transform.position.x,
                TargetPosition.y,
                card.transform.position.z);
            var newStack = new CardStack(card, logicalPosition);

            int originalCount = Cards.Count;
            for (int i = splitIndex + 1; i < originalCount; i++)
            {
                newStack.AddCard(Cards[i]);
            }

            Cards.RemoveRange(splitIndex, originalCount - splitIndex);

            return newStack;
        }

        /// <summary>
        /// Removes a card from the stack, destroys its GameObject, and cleans up the stack's state.
        /// </summary>
        /// <remarks>
        /// If the stack was crafting, the task is stopped. If the stack becomes empty, it is unregistered.
        /// </remarks>
        /// <param name="card">The card instance to be destroyed.</param>
        public void DestroyCard(
            CardInstance card,
            bool allowProtagonistRepresentationRemoval = false)
        {
            if (!allowProtagonistRepresentationRemoval &&
                ProtagonistRules.IsProtagonist(card))
            {
                return;
            }

            if (Cards.Remove(card))
            {
                if (IsCrafting) CraftingManager.Instance.StopCraftingTask(this);

                card.GetComponent<WorldMapLocation>()?.ReleaseDockedParty();

                card.Stack = null;
                GameObject.Destroy(card.gameObject);

                if (Cards.Count == 0)
                {
                    CardManager.Instance?.UnregisterStack(this);
                }
                else
                {
                    SetTargetPosition(TargetPosition);
                    CardManager.Instance?.ResolvePresentationLayers();
                }
            }
        }

        /// <summary>
        /// Finalizes a card removal after the owning trade transaction has
        /// already committed its reversible data changes. Cleanup callbacks
        /// are isolated so one faulty component cannot leave a half-consumed
        /// currency stack.
        /// </summary>
        public void DestroyCardForCommittedTrade(CardInstance card)
        {
            if (ProtagonistRules.IsProtagonist(card))
                return;

            if (card == null || !Cards.Remove(card))
                return;

            try
            {
                if (IsCrafting)
                    CraftingManager.Instance?.StopCraftingTask(this);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            try
            {
                card.KillTweens();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            try
            {
                card.GetComponent<WorldMapLocation>()?
                    .ReleaseDockedParty();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }

            card.Stack = null;
            try
            {
                Object.Destroy(card.gameObject);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            if (Cards.Count == 0)
            {
                CardManager.Instance?.UnregisterStack(this);
                return;
            }
            try
            {
                SetTargetPosition(TargetPosition);
                CardManager.Instance?.ResolvePresentationLayers();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Destroys all card instances within the stack and unregisters
        /// the entire stack from the <see cref="CardManager"/>.
        /// </summary>
        public void DestroyAllCards()
        {
            if (Cards.Exists(ProtagonistRules.IsProtagonist))
                return;

            if (IsCrafting) CraftingManager.Instance.StopCraftingTask(this);

            foreach (var card in Cards)
            {
                card.KillTweens();
                card.GetComponent<WorldMapLocation>()?.ReleaseDockedParty();
                card.Stack = null;
                GameObject.Destroy(card.gameObject);
            }

            Cards.Clear();
            CardManager.Instance?.UnregisterStack(this);
        }

        /// <summary>
        /// Updates the target position for the entire stack and propagates the new position to all individual cards.
        /// </summary>
        /// <remarks>
        /// Each card's final position is calculated by offsetting the base position based on the card's index and the StackStep setting.
        /// </remarks>
        /// <param name="newPosition">The new world position for the base (bottom) of the stack.</param>
        /// <param name="instant">If true, positions are set immediately; otherwise, they are moved via animation.</param>
        public void SetTargetPosition(Vector3 newPosition, bool instant = false)
        {
            TargetPosition = newPosition;
            ApplyCardLayout(instant);
        }

        private void ApplyCardLayout(
            bool instant,
            float? animationDuration = null)
        {
            Vector3 displayedBasePosition =
                TargetPosition + Vector3.up * PresentationBaseY;

            for (int i = 0; i < Cards.Count; i++)
            {
                var card = Cards[i];
                var cardTargetPos =
                    displayedBasePosition + card.Settings.StackStep * i;

                if (instant)
                {
                    card.SetTargetInstant(cardTargetPos);
                }
                else if (animationDuration.HasValue)
                {
                    card.SetPresentationTargetAnimated(
                        cardTargetPos,
                        animationDuration.Value);
                }
                else
                {
                    card.SetTargetAnimated(cardTargetPos);
                }
            }
        }

        /// <summary>
        /// Synchronizes the stack's logical anchor after a parent transform moves.
        /// The card transforms are intentionally left untouched because they have
        /// already inherited the same movement from their parent.
        /// </summary>
        public void SynchronizeTargetWithParentMotion(Vector3 newPosition)
        {
            TargetPosition = newPosition;
        }

        /// <summary>
        /// Settles card-local positional motion before the transform that owns
        /// the whole stack starts moving.
        /// </summary>
        public void StopMovementForParentDrag()
        {
            foreach (CardInstance card in Cards)
            {
                card?.StopMovementAtCurrentPosition();
            }
        }

        /// <summary>
        /// Moves the stack by a given world vector, typically used by the physics solver to resolve overlaps.
        /// </summary>
        /// <remarks>
        /// The final target position is checked against board placement rules before being applied.
        /// </remarks>
        /// <param name="worldTranslation">The X/Z vector to move the stack by.</param>
        public void ApplyTranslation(Vector3 worldTranslation)
        {
            var newTargetPosition = TargetPosition + worldTranslation;
            var finalPosition = Board.Instance.EnforcePlacementRules(newTargetPosition, this);

            SetTargetPosition(finalPosition);
        }

        /// <summary>
        /// Updates the stack positions specifically during a drag interaction.
        /// The Top card snaps instantly (responsiveness), while the trailing cards sway.
        /// </summary>
        public void SetDragTargetPosition(Vector3 newPosition)
        {
            TargetPosition = newPosition;

            Vector3 currentLeadingCardPos = TargetPosition;

            for (int i = 0; i < Cards.Count; i++)
            {
                var card = Cards[i];
                Vector3 cardTargetPos;

                if (i == 0)
                {
                    cardTargetPos = TargetPosition + card.Settings.StackStep * i;
                    card.SetTargetInstant(cardTargetPos);

                    currentLeadingCardPos = cardTargetPos;
                }
                else
                {
                    var precedingCard = Cards[i - 1];
                    cardTargetPos = precedingCard.transform.position + card.Settings.StackStep;
                    card.SetTargetDamped(cardTargetPos);
                }
            }
        }

        /// <summary>
        /// Immediately stops all active movement and DOTween animations on every card within the stack.
        /// </summary>
        public void KillAllTweens()
        {
            foreach (var card in Cards)
            {
                card.KillTweens();
            }
        }
    }
}
