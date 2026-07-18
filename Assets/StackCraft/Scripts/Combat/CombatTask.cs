using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    public class CombatTask
    {
        private enum CombatState
        {
            Idle,
            ExecutingTurn,
            Finished
        }

        #region Public Properties
        public List<CardInstance> Attackers { get; private set; }
        public List<CardInstance> Defenders { get; private set; }
        public bool PlayerIsAttacker { get; private set; }
        public bool IsOngoing => _currentState != CombatState.Finished;
        public CombatRect Rect { get; private set; }
        #endregion

        #region Private Fields
        private readonly List<CardInstance> _combatants = new();

        private const float ATTACK_INTERVAL = 1f;
        private float _turnTimer = ATTACK_INTERVAL;

        private CombatState _currentState = CombatState.Idle;
        private readonly MonoBehaviour _coroutineRunner;
        private Coroutine _attackCoroutine;
        private CameraController _cameraController;
        #endregion

        public CombatTask(List<CardInstance> attackers, List<CardInstance> defenders, bool playerIsAttacker, CombatRect rect)
        {
            // We need a MonoBehaviour instance to start coroutines on.
            _coroutineRunner = CombatManager.Instance;
            _cameraController = GameObject.FindAnyObjectByType<CameraController>();

            Attackers = attackers;
            Defenders = defenders;
            PlayerIsAttacker = playerIsAttacker;
            Rect = rect;
            _currentState = CombatState.Idle;

            _combatants.AddRange(attackers);
            _combatants.AddRange(defenders);
            foreach (var card in _combatants)
            {
                card.Combatant.InitializeCombatActionProgress();
            }
        }

        #region Turn Management
        /// <summary>
        /// Advances the combat simulation loop by one frame, responsible for ticking the
        /// action progress of all combatants and managing the turn timer.
        /// </summary>
        /// <remarks>
        /// All combatants continuously accumulate action progress based on their Attack Speed.
        /// When the combat is in the Idle state, the turn timer counts down, triggering
        /// the <see cref="ResolveTurn"/> method when the interval is reached.
        /// </remarks>
        /// <param name="delta">The time elapsed since the last frame (Time.deltaTime).</param>
        public void Update(float delta)
        {
            // Only proceed if combat is not finished
            if (!IsOngoing) return;

            // All combatants still build progress regardless of the state
            foreach (var card in _combatants)
            {
                card.Combatant.AddActionProgress(card.Stats.AttackSpeed.Value * delta);
            }

            // Only tick the turn timer if we are idle and waiting for the next turn.
            if (_currentState == CombatState.Idle)
            {
                _turnTimer -= delta;
                if (_turnTimer <= 0f)
                {
                    _turnTimer += ATTACK_INTERVAL;
                    ResolveTurn();
                }
            }
        }

        private void ResolveTurn()
        {
            if (Attackers.Count == 0 || Defenders.Count == 0)
            {
                EndCombat();
                return;
            }

            var actor = _combatants
                .Where(c => !c.IsBeingDragged)
                .OrderByDescending(c => c.Combatant.ActionProgress)
                .FirstOrDefault();

            if (actor == null) return;

            List<CardInstance> targetList = Attackers.Contains(actor) ? Defenders : Attackers;

            if (targetList.Count > 0)
            {
                var target = targetList[Random.Range(0, targetList.Count)];

                // Stop any previous coroutine that might be stuck (safety check)
                if (_attackCoroutine != null)
                {
                    _coroutineRunner.StopCoroutine(_attackCoroutine);
                }

                _attackCoroutine = _coroutineRunner.StartCoroutine(AttackSequenceCoroutine(actor, target));
            }
        }

        private IEnumerator AttackSequenceCoroutine(CardInstance attacker, CardInstance defender)
        {
            // Set state to prevent other attacks from starting
            _currentState = CombatState.ExecutingTurn;

            attacker.Combatant.SetAttackingState(true);
            CombatType type = attacker.Definition.CombatType;

            // --- ATTACK PHASE (ANIMATION) ---
            PlayAttackSound(type);

            if (type is CombatType.Melee or CombatType.None)
            {
                Vector3 targetPos = defender.transform.position + Vector3.up * 0.05f;
                var attackTween = attacker.transform.DOJump(targetPos, 1f, 1, 0.3f).SetUpdate(true);
                yield return attacker.StartCombatTween(attackTween).WaitForCompletion();
            }
            else if (type is CombatType.Ranged or CombatType.Magic)
            {
                Vector3 fireOrigin = attacker.transform.position + Vector3.up * 0.05f;
                Vector3 targetCenter = defender.transform.position + Vector3.up * 0.05f;

                Tween projectileTween = CombatManager.Instance.SpawnProjectile(type, fireOrigin, targetCenter);

                if (projectileTween != null)
                {
                    yield return projectileTween.WaitForCompletion();
                }
            }

            // --- COMBAT RESOLUTION ---
            HitResult result = ResolveAttack(attacker, defender);
            if (result.Type != HitType.Miss)
            {
                defender.TakeDamage(result.Damage);
                _cameraController.Shake();
                PlayHitSound(type);

                if (result.Type is HitType.Critical)
                {
                    AudioManager.Instance?.PlaySFX(AudioId.Critical);
                }
            }
            else
            {
                AudioManager.Instance?.PlaySFX(AudioId.Miss);
            }

            Vector3 uiSpawnPos = defender.transform.TransformPoint(new Vector3(0.3f, 0.1f, 0.4f));
            CombatManager.Instance.SpawnHitUI(uiSpawnPos, result);

            // --- RETURN PHASE (ANIMATION) ---
            float returnTime = 0.3f;
            if (type is CombatType.Melee or CombatType.None)
            {
                Vector3 returnPos = Rect.GetLayoutPosition(attacker);
                var returnTween = attacker.transform.DOJump(returnPos, 1f, 1, returnTime)
                    .SetUpdate(true);

                yield return attacker.StartCombatTween(returnTween).WaitForCompletion();
            }
            else if (type is CombatType.Ranged or CombatType.Magic)
            {
                yield return new WaitForSecondsRealtime(returnTime);
            }

            attacker.Combatant.SetAttackingState(false);

            // --- CLEANUP & RESOLUTION ---
            if (defender.CurrentHealth <= 0)
            {
                Attackers.Remove(defender);
                Defenders.Remove(defender);
                _combatants.Remove(defender);
                defender.Kill();
                Rect.UpdateLayout();
                yield return new WaitForSeconds(0.5f);
            }

            attacker.Combatant.ResetActionProgress();

            if (Attackers.Count == 0 || Defenders.Count == 0)
            {
                EndCombat();
            }
            else
            {
                _currentState = CombatState.Idle;
            }
        }
        #endregion

        #region Combat Resolution
        private HitResult ResolveAttack(CardInstance attacker, CardInstance defender)
        {
            // 1. Hit Chance
            float hitChance = Mathf.Clamp((attacker.Stats.Accuracy.Value - defender.Stats.Dodge.Value) / 100f, 0.05f, 0.95f);
            if (Random.value > hitChance)
                return new HitResult(HitType.Miss, 0, CombatTypeAdvantage.None);

            // 2. Critical Check
            float critChance = Mathf.Clamp(attacker.Stats.CriticalChance.Value / 100f, 0f, 1f);
            bool isCritical = Random.value <= critChance;

            // 3. Damage Calculation
            int attackPower = attacker.Stats.Attack.Value;
            int defensePower = defender.Stats.Defense.Value;
            int damage = Mathf.Max(1, attackPower - defensePower);

            // 4. RPS Logic
            CombatType attackerType = attacker.Definition.CombatType;
            CombatType defenderType = defender.Definition.CombatType;
            CombatTypeAdvantage advantage = GetAdvantage(attackerType, defenderType);

            if (advantage == CombatTypeAdvantage.Advantage)
            {
                damage = Mathf.RoundToInt(damage * CombatManager.Instance.AdvantageMultiplier);
            }
            else if (advantage == CombatTypeAdvantage.Disadvantage)
            {
                damage = Mathf.RoundToInt(damage * CombatManager.Instance.DisadvantageMultiplier);
            }

            if (isCritical)
                damage = Mathf.RoundToInt(damage * attacker.Stats.CriticalMultiplier.Value / 100f);

            return new HitResult(isCritical ? HitType.Critical : HitType.Normal, damage, advantage);
        }

        /// <summary>
        /// Determines the combat advantage between two unit types based on the Rock-Paper-Scissors rule.
        /// </summary>
        /// <remarks>
        /// The rules are: Melee > Ranged, Ranged > Magic, and Magic > Melee. 
        /// If types are the same or if either type is <see cref="CombatType.None"/>,
        /// the result is <see cref="CombatTypeAdvantage.None"/>.
        /// </remarks>
        /// <param name="attackerType">The CombatType of the attacking unit.</param>
        /// <param name="defenderType">The CombatType of the defending unit.</param>
        /// <returns>A CombatTypeAdvantage value (Advantage, Disadvantage, or None).</returns>
        private CombatTypeAdvantage GetAdvantage(CombatType attackerType, CombatType defenderType)
        {
            // If either card doesn't have a type, there is no advantage.
            if (attackerType == CombatType.None || defenderType == CombatType.None)
                return CombatTypeAdvantage.None;

            // If types are the same, no advantage.
            if (attackerType == defenderType)
                return CombatTypeAdvantage.None;

            // Melee > Ranged
            if (attackerType == CombatType.Melee && defenderType == CombatType.Ranged)
                return CombatTypeAdvantage.Advantage;

            // Ranged > Magic
            if (attackerType == CombatType.Ranged && defenderType == CombatType.Magic)
                return CombatTypeAdvantage.Advantage;

            // Magic > Melee
            if (attackerType == CombatType.Magic && defenderType == CombatType.Melee)
                return CombatTypeAdvantage.Advantage;

            // If it's not None and not an Advantage, it must be a Disadvantage.
            return CombatTypeAdvantage.Disadvantage;
        }
        #endregion

        #region Unit Management
        /// <summary>
        /// Adds a list of new cards to the combat if their faction is valid.
        /// </summary>
        /// <param name="newCombatants">The list of card instances to add.</param>
        /// <returns>True if at least one card was successfully added, otherwise false.</returns>
        public bool AddCombatants(List<CardInstance> newCombatants)
        {
            if (newCombatants == null || !newCombatants.Any()) return false;

            bool anyCardAdded = false;

            // A reference to the stack being added, for the overlap check later.
            CardStack sourceStack = newCombatants[0].Stack;

            foreach (var newCombatant in newCombatants)
            {
                var cardFaction = newCombatant.Definition.Faction;
                List<CardInstance> targetList = null;

                // Determine which side the new combatant should join.
                if (cardFaction == CardFaction.Player)
                {
                    targetList = PlayerIsAttacker ? Attackers : Defenders;
                }
                else if (cardFaction == CardFaction.Mob)
                {
                    targetList = PlayerIsAttacker ? Defenders : Attackers;
                }

                // Add the card if a valid side was found and it's not already in the combat.
                if (targetList != null && !_combatants.Contains(newCombatant))
                {
                    targetList.Add(newCombatant);
                    _combatants.Add(newCombatant);

                    newCombatant.Combatant.EnterCombat(this);
                    newCombatant.Combatant.InitializeCombatActionProgress();
                    anyCardAdded = true;
                }
            }

            // If we successfully added at least one card, perform the expensive updates once.
            if (anyCardAdded)
            {
                // 1. Trigger a visual update of the combat area.
                Rect.UpdateLayout();

                // 2. After resizing, check if this combat now overlaps with another and merge if needed.
                CombatManager.Instance?.CheckAndMergeCombats(this);

                // 3. Resolve any new overlaps with world stacks, ignoring the stack being added.
                CardManager.Instance?.ResolveOverlaps(Rect, sourceStack);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a card instance from the combat lists. This method is used when a combatant
        /// is removed or killed by an external source outside of the standard combat turn resolution
        /// (e.g., a card dying from starvation).
        /// </summary>
        /// <remarks>
        /// After removal, the combat layout is updated. If the removal results in one side having zero units,
        /// the combat task is immediately terminated via <see cref="EndCombat"/>.
        /// </remarks>
        /// <param name="cardToRemove">The card instance to be removed from the combat.</param>
        public void RemoveCombatant(CardInstance cardToRemove)
        {
            if (!IsOngoing) return;

            bool removedFromAttackers = Attackers.Remove(cardToRemove);
            bool removedFromDefenders = Defenders.Remove(cardToRemove);
            _combatants.Remove(cardToRemove);

            if (removedFromAttackers || removedFromDefenders)
            {
                Rect?.UpdateLayout();

                if (Attackers.Count == 0 || Defenders.Count == 0)
                {
                    EndCombat();
                }
            }
        }

        /// <summary>
        /// Attempts to remove a specified card from the combat, allowing it to "flee" the engagement.
        /// </summary>
        /// <remarks>
        /// This action is only permitted if the Player faction is currently the attacker and the combat is ongoing.
        /// If the card is successfully removed and the attacker side becomes empty, the combat is immediately terminated.
        /// </remarks>
        /// <param name="card">The card instance attempting to flee (must be one of the attackers).</param>
        /// <returns>True if the card was successfully removed from the combat; otherwise, false.</returns>
        public bool Flee(CardInstance card)
        {
            if (!PlayerIsAttacker || !IsOngoing) return false;

            bool wasRemoved = Attackers.Remove(card);
            if (wasRemoved)
            {
                _combatants.Remove(card);
                card.Combatant.LeaveCombat();
                Rect.UpdateLayout();

                if (Attackers.Count == 0)
                {
                    EndCombat();
                }
                return true;
            }

            return false;
        }
        #endregion

        #region Cleanup
        private void EndCombat()
        {
            if (_currentState == CombatState.Finished) return; // Prevent multiple calls

            // Stop the attack sequence if it's running to prevent errors
            if (_attackCoroutine != null)
            {
                _coroutineRunner.StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }

            _currentState = CombatState.Finished;

            if (Rect != null)
            {
                Rect.Close();
                Rect = null;
            }

            List<CardInstance> survivors = new List<CardInstance>();
            survivors.AddRange(Attackers);
            survivors.AddRange(Defenders);

            if (survivors.Count > 0)
            {
                foreach (var card in survivors)
                {
                    card.Combatant.LeaveCombat();

                    CardStack newStack = new CardStack(card, card.transform.position);
                    CardManager.Instance?.RegisterStack(newStack);

                    Vector3 finalPos = Board.Instance.EnforcePlacementRules(card.transform.position, newStack);
                    newStack.SetTargetPosition(finalPos);
                }
            }
        }

        /// <summary>
        /// Immediately ends this combat and cleans up its UI, intended for use when merging into a larger combat.
        /// This bypasses the normal survivor processing.
        /// </summary>
        public void CleanUpForMerge()
        {
            if (_currentState == CombatState.Finished) return; // Prevent multiple calls

            // Stop the attack sequence if it's running to prevent errors
            if (_attackCoroutine != null)
            {
                _coroutineRunner.StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }

            _currentState = CombatState.Finished;

            if (Rect != null)
            {
                Rect.Close();
                Rect = null;
            }
        }
        #endregion

        #region Effects
        private void PlayAttackSound(CombatType type)
        {
            switch (type)
            {
                case CombatType.Ranged:
                    AudioManager.Instance.PlaySFX(AudioId.AttackRanged);
                    break;

                case CombatType.Magic:
                    AudioManager.Instance.PlaySFX(AudioId.AttackMagic);
                    break;

                case CombatType.None:
                case CombatType.Melee:
                default:
                    AudioManager.Instance.PlaySFX(AudioId.AttackMelee);
                    break;
            }
        }

        private void PlayHitSound(CombatType type)
        {
            switch (type)
            {
                case CombatType.Ranged:
                    AudioManager.Instance.PlaySFX(AudioId.HitRanged);
                    break;

                case CombatType.Magic:
                    AudioManager.Instance.PlaySFX(AudioId.HitMagic);
                    break;

                case CombatType.None:
                case CombatType.Melee:
                default:
                    AudioManager.Instance.PlaySFX(AudioId.HitMelee);
                    break;
            }
        }
        #endregion
    }
}
