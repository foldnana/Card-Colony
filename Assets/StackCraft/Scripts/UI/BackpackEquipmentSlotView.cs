using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class BackpackEquipmentSlotView : MonoBehaviour,
        IPointerClickHandler
    {
        [SerializeField] private RawImage art;
        [SerializeField] private TMP_Text itemNameLabel;
        [SerializeField] private TMP_Text slotNameLabel;
        [SerializeField] private Outline selectionOutline;

        private BackpackView owner;
        private CardData equippedItem;
        private CardDefinition definition;

        public EquipmentSlot Slot { get; private set; }

        public void Bind(
            EquipmentSlot slot,
            CardData item,
            CardDefinition itemDefinition,
            BackpackView backpackView)
        {
            Slot = slot;
            equippedItem = item;
            definition = itemDefinition;
            owner = backpackView;
            if (art != null)
            {
                art.texture = itemDefinition?.ArtTexture;
                art.enabled = art.texture != null;
            }
            if (itemNameLabel != null)
            {
                itemNameLabel.text = itemDefinition != null
                    ? itemDefinition.DisplayName
                    : "+";
            }
            if (slotNameLabel != null)
                slotNameLabel.text = SlotLabel(slot);
            SetSelected(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.SelectEquipmentSlot(this, Slot, equippedItem, definition);
        }

        public void SetSelected(bool selected)
        {
            if (selectionOutline != null)
                selectionOutline.enabled = selected;
        }

        public static string SlotLabel(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "武器",
                EquipmentSlot.Armor => "护甲",
                EquipmentSlot.Accessory => "饰品",
                _ => slot.ToString()
            };
        }
    }
}
