using UnityEngine;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CardInstance))]
    public class CardCombatant : MonoBehaviour
    {
        public bool IsInCombat { get; private set; }
        public bool IsAttacking { get; private set; }
        public float ActionProgress { get; private set; }
        public CombatTask CurrentCombatTask { get; private set; }

        private CardInstance _card;
        private CardAI _aiComponent;
        private readonly Dictionary<string, float> skillCooldowns = new();
        public float ReaggroProtectionRemaining { get; private set; }

        private void Awake()
        {
            _card = GetComponent<CardInstance>();
            _aiComponent = GetComponent<CardAI>();
        }

        private void Update()
        {
            if (!IsInCombat && ReaggroProtectionRemaining > 0f)
            {
                ReaggroProtectionRemaining = Mathf.Max(
                    0f,
                    ReaggroProtectionRemaining - Time.deltaTime);
            }
        }

        /// <summary>
        /// Puts the card into a combat state.
        /// </summary>
        public void EnterCombat(CombatTask task)
        {
            CurrentCombatTask = task;
            IsInCombat = true;

            // If this card was just dragged from a crafting stack, that task must be stopped.
            if (_card.OriginalCraftingStack != null)
            {
                CraftingManager.Instance?.StopCraftingTask(_card.OriginalCraftingStack);
                _card.OriginalCraftingStack = null;
            }

            if (_card.Stack != null)
            {
                // If the stack this card is leaving was crafting, stop the craft.
                if (_card.Stack.IsCrafting)
                {
                    CraftingManager.Instance?.StopCraftingTask(_card.Stack);
                }

                _card.Stack.RemoveCard(_card); // A card in combat doesn't belong to a world stack.
            }
            _card.Stack = null;

            CardManager.Instance.TurnOffHighlightedCards();

            // Stop AI if it exists
            if (_aiComponent != null)
            {
                _aiComponent.StopAI();
            }
        }

        /// <summary>
        /// Removes the card from its combat state.
        /// </summary>
        public void LeaveCombat()
        {
            CurrentCombatTask = null;
            IsInCombat = false;

            // Restart AI if it exists
            if (_aiComponent != null)
            {
                _aiComponent.StartAI();
            }
        }

        /// <summary>
        /// Sets the card's attacking flag (used for animations/logic).
        /// </summary>
        public void SetAttackingState(bool value)
        {
            IsAttacking = value;
        }

        /// <summary>
        /// Initializes the action progress, often with a random start.
        /// </summary>
        public void InitializeCombatActionProgress()
        {
            ActionProgress = 0f;
        }

        /// <summary>
        /// Adds to the action progress, checking if the card is alive.
        /// </summary>
        public void AddActionProgress(float amount)
        {
            if (_card.CurrentHealth > 0)
            {
                ActionProgress = Mathf.Clamp(
                    ActionProgress + amount,
                    0f,
                    CombatActionScheduler.ProgressCap);
            }
        }

        /// <summary>
        /// Resets the action progress to zero.
        /// </summary>
        public void ResetActionProgress()
        {
            ActionProgress = 0f;
        }

        public void SetActionProgress(float value)
        {
            ActionProgress = Mathf.Clamp(
                value,
                0f,
                CombatActionScheduler.ProgressCap);
        }

        public void ConsumeActionProgress()
        {
            ActionProgress = Mathf.Max(
                0f,
                ActionProgress - CombatActionScheduler.ActionThreshold);
        }

        public float GetSkillCooldown(string skillId)
        {
            return !string.IsNullOrWhiteSpace(skillId) &&
                skillCooldowns.TryGetValue(skillId, out float remaining)
                ? remaining
                : 0f;
        }

        public void SetSkillCooldown(string skillId, float seconds)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return;
            skillCooldowns[skillId] = Mathf.Max(0f, seconds);
        }

        public void TickCombatRuntime(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            foreach (string skillId in new List<string>(skillCooldowns.Keys))
            {
                float remaining = Mathf.Max(0f, skillCooldowns[skillId] - deltaTime);
                if (remaining <= 0f)
                    skillCooldowns.Remove(skillId);
                else
                    skillCooldowns[skillId] = remaining;
            }
            ReaggroProtectionRemaining = Mathf.Max(
                0f,
                ReaggroProtectionRemaining - deltaTime);
        }

        public void GrantReaggroProtection(float seconds)
        {
            ReaggroProtectionRemaining = Mathf.Max(
                ReaggroProtectionRemaining,
                seconds);
        }

        public IReadOnlyDictionary<string, float> SkillCooldowns => skillCooldowns;
    }
}
