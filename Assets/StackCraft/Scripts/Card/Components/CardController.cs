using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CardInstance))]
    public class CardController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        // Component References
        private CardInstance _card;
        private CardCombatant _combatant;
        private CardEquipment _equipmentComponent;
        private Camera _mainCam;

        // Drag State
        private Vector3 _dragOffset;
        private Vector3 _dragStartPosition;

        // Helper Properties
        private bool isEquipped => _equipmentComponent != null && _equipmentComponent.IsEquipped;
        private bool inCombat => _combatant != null && _combatant.IsInCombat;
        private CardInstance equipperCard => _equipmentComponent?.Equipper;

        private void Awake()
        {
            _card = GetComponent<CardInstance>();
            _combatant = GetComponent<CardCombatant>();
            _equipmentComponent = GetComponent<CardEquipment>();
            _mainCam = Camera.main;
        }

        private void Update()
        {
            if (_card.IsBeingDragged)
            {
                UpdateDragPosition();
            }
        }

        #region Player Input
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!InputManager.Instance.IsInputEnabled) return;

            // 1. Handle Equipped Cards
            if (isEquipped)
            {
                if (equipperCard == null || equipperCard.EquipperComponent == null) return;

                equipperCard.EquipperComponent.Unequip(_card.Definition.EquipmentSlot);
                _card.IsBeingDragged = true;
                _card.Stack.KillAllTweens();

                _dragOffset = transform.position - GetMouseWorldPosition();

                CardManager.Instance?.HighlightStackableStacks(_card);
                TradeManager.Instance?.HighlightTradeableZones(_card.Stack);
                return;
            }

            // 2. Handle Combat Cards
            if (inCombat)
            {
                if (_combatant.CurrentCombatTask != null &&
                    _combatant.CurrentCombatTask.PlayerIsAttacker &&
                    _combatant.CurrentCombatTask.Attackers.Contains(_card) &&
                    !_combatant.IsAttacking)
                {
                    _card.Stack = new CardStack(_card, transform.position);
                    _card.IsBeingDragged = true;
                    _dragOffset = transform.position - GetMouseWorldPosition();
                }
                return;
            }

            // 3. Handle Standard Drag (Stack Splitting)
            _card.IsBeingDragged = true;
            _dragStartPosition = transform.position;

            var oldStack = _card.Stack;
            var newStack = oldStack.SplitAt(_card);

            if (newStack != null)
            {
                _card.Stack = newStack;
                CardManager.Instance.RegisterStack(newStack);
                // Keep the old stack where it is visually
                oldStack.SetTargetPosition(oldStack.TargetPosition);

                if (oldStack.IsCrafting)
                {
                    _card.OriginalCraftingStack = oldStack;
                    CraftingManager.Instance.PauseCraftingTask(oldStack);
                }
                else
                {
                    CraftingManager.Instance.CheckForRecipe(oldStack);
                }
            }

            _card.Stack.KillAllTweens();
            _dragOffset = transform.position - GetMouseWorldPosition();

            CardManager.Instance?.HighlightStackableStacks(_card);
            TradeManager.Instance?.HighlightTradeableZones(_card.Stack);
            AudioManager.Instance?.PlaySFX(AudioId.CardPick);

            UpdateDragPosition();
        }

        private void UpdateDragPosition()
        {
            Vector3 mousePos = GetMouseWorldPosition() + _dragOffset;
            mousePos.y = _card.Settings.DragHeight;
            Vector3 finalPos = Board.Instance.ClampToBounds(mousePos, _card.Stack);

            _card.Stack?.SetDragTargetPosition(finalPos);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!_card.IsBeingDragged) return;

            _card.IsBeingDragged = false;

            CardManager.Instance?.TurnOffHighlightedCards();
            TradeManager.Instance?.TurnOffHighlightedZones();
            AudioManager.Instance?.PlaySFX(AudioId.CardDrop);

            Vector3 dropPosition = transform.position.Flatten();
            float dragDistance = Vector3.Distance(dropPosition, _dragStartPosition);

            if (dragDistance < _card.Settings.ClickThreshold)
            {
                if (HandleClick()) return;
            }

            if (inCombat)
            {
                HandleCombatDrop();
                return;
            }

            HandleStandardDrop(dropPosition);
        }

        #endregion

        #region Drop Logic Helpers
        private void HandleCombatDrop()
        {
            if (_card.Stack == null)
            {
                _combatant.CurrentCombatTask.Rect.RepositionCard(_card);
                return;
            }

            bool wantsToFlee = !_combatant.CurrentCombatTask.Rect.IsPositionInside(transform.position);

            if (wantsToFlee)
            {
                bool fleeSuccessful = _combatant.CurrentCombatTask.Flee(_card);
                if (fleeSuccessful)
                {
                    CardManager.Instance.RegisterStack(_card.Stack);
                    var fleePos = Board.Instance.EnforcePlacementRules(transform.position, _card.Stack);
                    _card.Stack.SetTargetPosition(fleePos);
                    CardManager.Instance.ResolveOverlaps();
                }
                else
                {
                    _combatant.CurrentCombatTask.Rect.RepositionCard(_card);
                    _card.Stack.RemoveCard(_card);
                    _card.Stack = null;
                }
            }
            else
            {
                _combatant.CurrentCombatTask.Rect.RepositionCard(_card);
                _card.Stack.RemoveCard(_card);
                _card.Stack = null;
            }
        }

        private void HandleStandardDrop(Vector3 dropPosition)
        {
            var terminalActions = new System.Func<bool>[]
            {
                TryTradeWithNearbyZone,
                TryEquipOnNearbyCharacter,
                TryJoinCombatWithExistingTask,
                TryInitiateCombatWithNearbyEnemy
            };

            foreach (var action in terminalActions)
            {
                if (action.Invoke())
                {
                    if (_card.OriginalCraftingStack != null)
                        CraftingManager.Instance.StopCraftingTask(_card.OriginalCraftingStack);
                    _card.OriginalCraftingStack = null;
                    return;
                }
            }

            if (_card.Stack == null || _card.Stack.Cards.Count == 0)
            {
                _card.OriginalCraftingStack = null;
                return;
            }

            var finalPos = Board.Instance.EnforcePlacementRules(dropPosition, _card.Stack);
            _card.Stack.SetTargetPosition(finalPos);

            var attachedToStack = _card.TryAttachToNearbyStack(_card.Settings.AttachRadius, stackToIgnore: null);

            if (_card.OriginalCraftingStack != null)
            {
                if (attachedToStack == _card.OriginalCraftingStack)
                    CraftingManager.Instance.ResumeCraftingTask(_card.OriginalCraftingStack);
                else
                    CraftingManager.Instance.ValidateAndResumeTask(_card.OriginalCraftingStack);
            }

            if (attachedToStack == null && !_card.Stack.IsCrafting)
            {
                CraftingManager.Instance.CheckForRecipe(_card.Stack);
            }

            CardManager.Instance?.ResolveOverlaps();
            _card.OriginalCraftingStack = null;
        }
        #endregion

        #region World Interactions
        private bool HandleClick()
        {
            var clickable = _card.GetComponent<IClickable>();
            if (clickable != null)
            {
                return clickable.OnClick(_dragStartPosition);
            }
            return false;
        }

        private Vector3 GetMouseWorldPosition()
        {
            Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float dist))
                return ray.GetPoint(dist);
            return Vector3.zero;
        }

        private bool TryTradeWithNearbyZone()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _card.Settings.AttachRadius);
            TradeZone bestCandidate = null;
            float bestSqrDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var tradeZone = hit.GetComponent<TradeZone>();
                if (tradeZone == null) continue;

                float sqrDist = (tradeZone.transform.position - transform.position).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestCandidate = tradeZone;
                }
            }

            if (bestCandidate != null)
            {
                return bestCandidate.TryTradeAndConsumeStack(_card.Stack);
            }

            return false;
        }

        private bool TryEquipOnNearbyCharacter()
        {
            if (_card.EquipmentComponent == null) return false;

            Collider[] hits = Physics.OverlapSphere(transform.position, _card.Settings.AttachRadius);
            CardInstance targetCharacter = null;
            float bestSqrDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var otherCard = hit.GetComponent<CardInstance>();
                if (otherCard == null || otherCard.Stack == _card.Stack) continue;
                if (otherCard.Definition.Category != CardCategory.Character) continue;

                float sqrDist = (otherCard.transform.position - transform.position).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    targetCharacter = otherCard;
                }
            }

            if (targetCharacter != null)
            {
                if (targetCharacter.EquipperComponent != null)
                {
                    return targetCharacter.EquipperComponent.Equip(_card);
                }
            }

            return false;
        }

        private bool TryInitiateCombatWithNearbyEnemy()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _card.Settings.AttachRadius);
            CardStack targetStack = null;
            float bestSqrDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var otherCard = hit.GetComponent<CardInstance>();
                if (otherCard == null || otherCard.Stack == null || otherCard.Stack == _card.Stack) continue;

                float sqrDist = (otherCard.transform.position - transform.position).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    targetStack = otherCard.Stack;
                }
            }

            if (targetStack != null)
            {
                var myFaction = _card.Stack.TopCard.Definition.Faction;
                var theirFaction = targetStack.TopCard.Definition.Faction;

                if (myFaction == CardFaction.Player && theirFaction == CardFaction.Mob)
                {
                    var attackerCards = _card.Stack.Cards.Where(_card.IsCombatant).ToList();
                    var defenderCards = targetStack.Cards.Where(_card.IsCombatant).ToList();

                    if (attackerCards.Any() && defenderCards.Any())
                    {
                        CombatManager.Instance.StartCombat(attackerCards, defenderCards, true);
                        return true;
                    }
                }
                else if (myFaction == CardFaction.Mob && theirFaction == CardFaction.Player)
                {
                    var attackerCards = _card.Stack.Cards.Where(_card.IsCombatant).ToList();
                    var defenderCards = targetStack.Cards.Where(_card.IsCombatant).ToList();

                    if (attackerCards.Any() && defenderCards.Any())
                    {
                        CombatManager.Instance.StartCombat(attackerCards, defenderCards, false);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryJoinCombatWithExistingTask()
        {
            if (_card.Definition.Faction != CardFaction.Player && _card.Definition.Faction != CardFaction.Mob)
            {
                return false;
            }

            if (_card.Stack == null) return false;

            var targetTask = CombatManager.Instance.GetCombatTaskAtPosition(transform.position);

            if (targetTask != null)
            {
                var cardsInStack = new List<CardInstance>(_card.Stack.Cards);
                CardManager.Instance?.UnregisterStack(_card.Stack);
                bool success = targetTask.AddCombatants(cardsInStack);
                return success;
            }

            return false;
        }
        #endregion
    }
}
