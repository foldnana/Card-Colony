using System.Collections;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CardInstance))]
    public class CardAI : MonoBehaviour
    {
        private CardInstance _card;
        private CardCombatant _combatant;
        private Coroutine _autoMoveCoroutine;
        private Coroutine _produceCoroutine;

        private void Awake()
        {
            _card = GetComponent<CardInstance>();
            _combatant = GetComponent<CardCombatant>();
        }

        private void Start()
        {
            StartAI();
        }

        /// <summary>
        /// Starts this card's autonomous behavior system.
        /// Initializes movement and, if applicable, begins the produce-spawning loop.
        /// Aggressive cards only run movement/combat behavior, while passive cards
        /// that can produce items also run a production coroutine.
        /// </summary>
        public void StartAI()
        {
            StopAI();

            _autoMoveCoroutine = StartCoroutine(AutoMove());

            if (!_card.Definition.IsAggressive && _card.Definition.ProduceCard != null)
                _produceCoroutine = StartCoroutine(ProduceLoop());
        }

        /// <summary>
        /// Stops all AI behavior running on this card.
        /// Safely terminates movement and production coroutines
        /// and clears their references so they can be restarted cleanly.
        /// </summary>
        public void StopAI()
        {
            if (_autoMoveCoroutine != null)
            {
                StopCoroutine(_autoMoveCoroutine);
                _autoMoveCoroutine = null;
            }

            if (_produceCoroutine != null)
            {
                StopCoroutine(_produceCoroutine);
                _produceCoroutine = null;
            }
        }

        #region Auto Move Logic
        private IEnumerator AutoMove()
        {
            yield return new WaitForSeconds(Random.Range(0.5f, 1.0f));

            while (true)
            {
                yield return new WaitForSeconds(_card.Settings.MoveInterval);

                if (!CanMove()) continue;
                if (ShouldStayInEnclosure()) continue;

                EnsureDetachedFromStack();

                if (_card.Definition.IsAggressive)
                {
                    ExecuteAggressiveBehavior();
                }
                else
                {
                    MoveRandomly();
                }
            }
        }

        private bool CanMove()
        {
            return !_card.IsBeingDragged
                && !(_card.Stack != null && _card.Stack.IsCrafting)
                && !_combatant.IsInCombat;
        }

        private void EnsureDetachedFromStack()
        {
            if (_card.Stack.Cards.Count > 1)
            {
                DetachFromStack();
            }
        }

        private bool ShouldStayInEnclosure()
        {
            // Aggressive units and cards not in a stack ignore enclosure rules.
            if (_card.Definition.IsAggressive || _card.Stack == null)
                return false;

            // Find the enclosure card in this stack (if any).
            var enclosureCard = _card.Stack.Cards.FirstOrDefault(
                c => c.GetComponent<EnclosureLogic>() != null
            );

            if (enclosureCard == null)
                return false;

            int capacity = enclosureCard.GetComponent<EnclosureLogic>().Capacity;

            // Position of enclosure and this card within the stack ordering.
            int enclosureIndex = _card.Stack.Cards.IndexOf(enclosureCard);
            int myIndex = _card.Stack.Cards.IndexOf(_card);

            // How many cards above the enclosure this card is.
            int distanceAboveEnclosure = myIndex - enclosureIndex;

            // Card must remain if it is within the enclosure capacity range above it.
            return distanceAboveEnclosure > 0 && distanceAboveEnclosure <= capacity;
        }

        private void ExecuteAggressiveBehavior()
        {
            // 1. Priority: Join Existing Combat
            if (TryJoinExistingCombat()) return;

            // 2. Priority: Hunt Player
            if (TryHuntPlayer()) return;

            // 3. Fallback: Patrol
            MoveRandomly();
        }

        private bool TryJoinExistingCombat()
        {
            CombatTask closestCombat = FindClosestRelevantCombat(_card.Definition.AggroRadius);

            if (closestCombat == null || closestCombat.Rect == null) return false;

            float distanceToCombat = Vector3.Distance(transform.position, closestCombat.Rect.transform.position);

            if (distanceToCombat <= _card.Definition.AttackRadius * 1.5f)
            {
                closestCombat.AddCombatants(_card.Stack.Cards.ToList());
            }
            else
            {
                MoveTowards(closestCombat.Rect.transform.position);
            }

            return true;
        }

        private bool TryHuntPlayer()
        {
            CardInstance target = FindClosestPlayerCard(_card.Definition.AggroRadius);

            if (target == null) return false;

            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);

            if (distanceToTarget <= _card.Definition.AttackRadius)
            {
                var attackerCards = _card.Stack.Cards.Where(_card.IsCombatant).ToList();
                var defenderCards = target.Stack.Cards.Where(_card.IsCombatant).ToList();

                if (attackerCards.Any() && defenderCards.Any())
                {
                    CombatManager.Instance.StartCombat(attackerCards, defenderCards, false);
                }
            }
            else
            {
                MoveTowards(target.transform.position);
            }

            return true;
        }
        #endregion

        #region Movement & Stack Helpers
        private CombatTask FindClosestRelevantCombat(float radius)
        {
            if (CombatManager.Instance == null) return null;

            CombatTask bestTask = null;
            float bestDistSq = radius * radius;
            Vector3 myPos = transform.position;

            foreach (var task in CombatManager.Instance.ActiveCombats)
            {
                if (!task.IsOngoing || task.Rect == null) continue;

                bool hasPlayerInvolvement = task.Attackers.Any(c => c.Definition.Faction == CardFaction.Player)
                                         || task.Defenders.Any(c => c.Definition.Faction == CardFaction.Player);

                if (!hasPlayerInvolvement) continue;

                float distSq = (task.Rect.transform.position - myPos).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTask = task;
                }
            }

            return bestTask;
        }

        private CardInstance FindClosestPlayerCard(float radius)
        {
            return CardManager.Instance.AllCards
                .Where(c => c != null &&
                            c.Definition.Faction == CardFaction.Player &&
                            !c.Combatant.IsInCombat)
                .OrderBy(c => Vector3.Distance(transform.position, c.transform.position))
                .FirstOrDefault();
        }

        private void MoveTowards(Vector3 targetPos)
        {
            Vector3 direction = (targetPos - transform.position).normalized;
            Vector3 movePosition = transform.position + direction * _card.Settings.MoveRadius;

            if (Board.Instance.IsPointValid(movePosition, _card.Stack))
            {
                _card.Stack.SetTargetPosition(movePosition);
                CardManager.Instance?.ResolveOverlaps();
            }
        }

        private void DetachFromStack()
        {
            var oldStack = _card.Stack;

            if (!oldStack.Cards.Remove(_card)) return;

            var newStack = new CardStack(_card, transform.position);
            CardManager.Instance.RegisterStack(newStack);

            if (oldStack.Cards.Count == 0)
            {
                CardManager.Instance?.UnregisterStack(oldStack);
            }
            else
            {
                oldStack.SetTargetPosition(oldStack.TargetPosition);
                if (oldStack.IsCrafting)
                {
                    CraftingManager.Instance.StopCraftingTask(oldStack);
                }
                else
                {
                    CraftingManager.Instance.CheckForRecipe(oldStack);
                }
            }
        }

        private void MoveRandomly()
        {
            Vector3 basePosition = _card.Stack.TargetPosition;
            Vector3 targetPosition = basePosition;

            for (int i = 0; i < _card.Settings.MaxAttemptsPerMove; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                Vector3 candidate = basePosition + new Vector3(direction.x, 0f, direction.y) * _card.Settings.MoveRadius;

                if (Board.Instance.IsPointValid(candidate, _card.Stack))
                {
                    targetPosition = candidate;
                    break;
                }
            }

            _card.Stack.SetTargetPosition(targetPosition);
            CardManager.Instance?.ResolveOverlaps();
        }
        #endregion

        #region Produce Spawning
        private IEnumerator ProduceLoop()
        {
            yield return new WaitForSeconds(Random.Range(0.5f, 1.0f));

            while (true)
            {
                yield return new WaitForSeconds(_card.Definition.ProduceInterval);

                if (_card.Definition.IsAggressive || !CanMove())
                    continue;

                SpawnProduce();
            }
        }

        private void SpawnProduce()
        {
            var produceCard = _card.Definition.ProduceCard;

            if (produceCard == null)
                return;

            CardManager.Instance.CreateCardInstance(
                produceCard,
                transform.position
            );

            _card.PlayPuffParticle();
        }
        #endregion
    }
}
