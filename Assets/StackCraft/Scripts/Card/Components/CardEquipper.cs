using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CardInstance))]
    public class CardEquipper : MonoBehaviour
    {
        [SerializeField, Tooltip("Manages the visual layout and display of all equipped items for this card.")]
        private EquipmentPanel equipmentPanel;

        private CardInstance _card;
        private readonly Dictionary<EquipmentSlot, CardInstance> _equippedItems = new();

        public IEnumerable<CardInstance> EquippedCards => _equippedItems.Values;

        // Class Change State
        public CardDefinition OriginalDefinition { get; private set; }
        private CardInstance _classChangeItem;

        private void Awake()
        {
            _card = GetComponent<CardInstance>();
        }

        /// <summary>
        /// Explicitly records the base <see cref="CardDefinition"/> of this card.
        /// </summary>
        /// <param name="def">The original definition to revert to if a class-changing item is unequipped.</param>
        public void SetOriginalDefinition(CardDefinition def)
        {
            OriginalDefinition = def;
        }

        /// <summary>
        /// Attaches an equipment card to this card, applying stat modifiers and handling potential class changes.
        /// </summary>
        /// <param name="equipmentCard">The <see cref="CardInstance"/> to be equipped.</param>
        /// <returns>True if the equipment was successfully attached; false if the card lacks an equipment component or a valid panel.</returns>
        /// <remarks>
        /// This method manages several complex state changes:
        /// <list type="bullet">
        /// <item><description>Automatically unequips existing items in the same <see cref="EquipmentSlot"/>.</description></item>
        /// <item><description>Triggers class transformation if the item has a <see cref="CardDefinition.ClassChangeResult"/>.</description></item>
        /// <item><description>Recalculates all active stat modifiers to ensure the new base stats are modified correctly.</description></item>
        /// </list>
        /// </remarks>
        public bool Equip(CardInstance equipmentCard)
        {
            if (equipmentCard.EquipmentComponent == null) return false;
            if (equipmentPanel == null) return false;

            var slot = equipmentCard.Definition.EquipmentSlot;

            if (_equippedItems.ContainsKey(slot))
            {
                Unequip(slot);
            }

            _equippedItems[slot] = equipmentCard;

            equipmentPanel.AttachEquipment(equipmentCard);

            equipmentCard.EquipmentComponent.OnEquipped(this._card);

            // --- HANDLE CLASS CHANGE ---
            if (equipmentCard.Definition.ClassChangeResult != null)
            {
                if (OriginalDefinition == null)
                {
                    OriginalDefinition = this._card.Definition;
                }
                _classChangeItem = equipmentCard;

                var allEquipped = _equippedItems.Values.ToList();
                foreach (var item in allEquipped)
                {
                    foreach (var modifier in item.Definition.StatModifiers)
                    {
                        GetStatFromType(modifier.Stat)?.RemoveModifier(modifier);
                    }
                }

                _card.SetDefinition(equipmentCard.Definition.ClassChangeResult);

                foreach (var item in allEquipped)
                {
                    foreach (var modifier in item.Definition.StatModifiers)
                    {
                        GetStatFromType(modifier.Stat)?.AddModifier(modifier);
                    }
                }
            }
            else // --- STANDARD STAT MODIFICATION ---
            {
                foreach (var modifier in equipmentCard.Definition.StatModifiers)
                {
                    GetStatFromType(modifier.Stat)?.AddModifier(modifier);
                }
            }

            _card.UpdateStatDisplays();
            CardManager.Instance?.NotifyCardEquipped(equipmentCard.Definition);
            return true;
        }

        /// <summary>
        /// Removes the item from the specified slot and returns it to the game board.
        /// </summary>
        /// <param name="slot">The <see cref="EquipmentSlot"/> to clear.</param>
        /// <remarks>
        /// If the item being removed was the cause of a class change, this method triggers a "Class Revert" logic 
        /// that restores the <see cref="OriginalDefinition"/> while preserving modifiers from other equipped items.
        /// </remarks>
        public void Unequip(EquipmentSlot slot)
        {
            if (!_equippedItems.TryGetValue(slot, out var equipmentToDrop)) return;
            if (equipmentPanel == null) return;

            foreach (var modifier in equipmentToDrop.Definition.StatModifiers)
            {
                GetStatFromType(modifier.Stat)?.RemoveModifier(modifier);
            }

            // --- START: Class Revert Logic ---
            if (OriginalDefinition != null && equipmentToDrop == _classChangeItem)
            {
                var otherItems = _equippedItems.Values
                    .Where(item => item != equipmentToDrop)
                    .ToList();

                foreach (var item in otherItems)
                {
                    foreach (var modifier in item.Definition.StatModifiers)
                    {
                        GetStatFromType(modifier.Stat)?.RemoveModifier(modifier);
                    }
                }

                _card.SetDefinition(OriginalDefinition);

                foreach (var item in otherItems)
                {
                    foreach (var modifier in item.Definition.StatModifiers)
                    {
                        GetStatFromType(modifier.Stat)?.AddModifier(modifier);
                    }
                }
                OriginalDefinition = null;
                _classChangeItem = null;
            }
            // --- END: Class Revert Logic ---

            _equippedItems.Remove(slot);

            equipmentPanel.DetachEquipment(equipmentToDrop);

            equipmentToDrop.EquipmentComponent.OnUnequipped();

            CardManager.Instance?.ReturnCardToBoard(equipmentToDrop);

            _card.UpdateStatDisplays();
        }

        /// <summary>
        /// Iteratively removes all currently equipped items from the card.
        /// </summary>
        /// <remarks>
        /// This effectively resets the card to its base state by calling <see cref="Unequip"/> for every occupied slot.
        /// </remarks>
        public void UnequipAll()
        {
            foreach (var slot in _equippedItems.Keys.ToList())
            {
                Unequip(slot);
            }
        }

        private Stat GetStatFromType(StatType type)
        {
            return type switch
            {
                StatType.MaxHealth => _card.Stats.MaxHealth,
                StatType.Attack => _card.Stats.Attack,
                StatType.Defense => _card.Stats.Defense,
                StatType.AttackSpeed => _card.Stats.AttackSpeed,
                StatType.Accuracy => _card.Stats.Accuracy,
                StatType.Dodge => _card.Stats.Dodge,
                StatType.CriticalChance => _card.Stats.CriticalChance,
                StatType.CriticalMultiplier => _card.Stats.CriticalMultiplier,
                _ => null,
            };
        }
    }
}
