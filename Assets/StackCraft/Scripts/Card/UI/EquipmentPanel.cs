using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(MeshRenderer), typeof(BoxCollider))]
    public class EquipmentPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private class AnimatedEquipment
        {
            public CardInstance Card;
            public Vector3 BaseLocalPosition;
            public float NoiseOffsetX;
            public float NoiseOffsetZ;
        }

        [Header("Attach Points")]
        [SerializeField, Tooltip("The local position where a Weapon card is attached. Visualized by a gizmo in the editor.")]
        private Vector3 weaponAttachPoint = new Vector3(-1f, 0.2f, 0f);

        [SerializeField, Tooltip("The local position where an Armor card is attached. Visualized by a gizmo in the editor.")]
        private Vector3 armorAttachPoint = new Vector3(0f, 0.2f, -0.7f);

        [SerializeField, Tooltip("The local position where an Accessory card is attached. Visualized by a gizmo in the editor.")]
        private Vector3 accessoryAttachPoint = new Vector3(1f, 0.2f, 0f);

        [Header("Animation")]
        [SerializeField, Tooltip("How far the cards will randomly orbit from their attach point.")]
        private float animationRadius = 0.1f;

        [SerializeField, Tooltip("How fast the cards will move in their random orbit.")]
        private float animationSpeed = 1f;

        private Camera _mainCam;
        private MeshRenderer _meshRenderer;
        private BoxCollider _boxCollider;
        private Material _materialInstance;
        private Vector3 _activeSlot;
        private bool _isClickable = true;
        private bool _isCardsVisible;

        private readonly List<AnimatedEquipment> _animatedCards = new();

        private void Awake()
        {
            _mainCam = Camera.main;
            _meshRenderer = GetComponent<MeshRenderer>();
            _boxCollider = GetComponent<BoxCollider>();
            _materialInstance = _meshRenderer.material;
            _activeSlot = Vector3.zero;

            _materialInstance.SetVector("_SlotActive", _activeSlot);

            UpdatePanelVisibility();
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayEnded += HandleDayEnded;
                TimeManager.Instance.OnDayStarted += HandleDayStarted;
            }

            HideCards();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            InfoPanel.Instance?.RegisterHover(GetInfo());
        }

        private (string, string) GetInfo()
        {
            if (_animatedCards == null || _animatedCards.Count == 0) return ("", "");

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _animatedCards.Count; i++)
            {
                var animatedCard = _animatedCards[i];
                if (animatedCard.Card == null) continue;

                sb.Append($"\u2022 {animatedCard.Card.Definition.DisplayName}");

                if (i < _animatedCards.Count - 1) sb.Append("\n");
            }

            return ("装备", sb.ToString());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            InfoPanel.Instance?.UnregisterHover();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isClickable) ToggleVisibility();
        }

        private void OnDisable()
        {
            InfoPanel.Instance?.UnregisterHover();
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayEnded -= HandleDayEnded;
                TimeManager.Instance.OnDayStarted -= HandleDayStarted;
            }
        }

        private void HandleDayEnded(int _)
        {
            _isClickable = false;
            HideCards();
        }

        private void HandleDayStarted(int _) => _isClickable = true;

        private void Update()
        {
            // Only animate if the cards are supposed to be visible.
            if (_isCardsVisible && _animatedCards.Count > 0)
            {
                AnimateCards();
            }

            CheckForOutsideClick();
        }

        private void AnimateCards()
        {
            float time = Time.time * animationSpeed;

            foreach (var item in _animatedCards)
            {
                if (item.Card == null) continue;

                // 1. Get two different Perlin noise values (0 to 1 range).
                float noiseX = Mathf.PerlinNoise(time + item.NoiseOffsetX, 0f);
                float noiseZ = Mathf.PerlinNoise(0f, time + item.NoiseOffsetZ);

                // 2. Map noise from (0 to 1) to (-1 to 1) and scale by radius.
                float x = (noiseX * 2.0f - 1.0f) * animationRadius;
                float z = (noiseZ * 2.0f - 1.0f) * animationRadius;

                // 3. Apply the offset to the card's base position.
                Vector3 offset = new Vector3(x, 0f, z);
                item.Card.transform.localPosition = item.BaseLocalPosition + offset;
            }
        }

        private void CheckForOutsideClick()
        {
            // Only run this check if the panel is currently visible.
            if (!_isCardsVisible) return;

            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    if (hit.transform == transform) return;

                    if (hit.transform.TryGetComponent<CardInstance>(out var card))
                    {
                        if (card.Definition.Category is CardCategory.Equipment)
                            return;
                    }
                }

                // If we reach this point, the click was "outside."
                HideCards();
            }
        }

        public void ToggleVisibility()
        {
            if (_isCardsVisible) HideCards();
            else ShowCards();
        }

        public void ShowCards()
        {
            _isCardsVisible = true;
            UpdateCardsVisibility();
        }

        public void HideCards()
        {
            _isCardsVisible = false;
            UpdateCardsVisibility();
        }

        private void UpdatePanelVisibility()
        {
            bool visible = _animatedCards.Count > 0;
            _meshRenderer.enabled = visible;
            _boxCollider.enabled = visible;
        }

        private void UpdateSlotsVisual(EquipmentSlot slot, float value)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    _activeSlot.x = value;
                    break;
                case EquipmentSlot.Armor:
                    _activeSlot.y = value;
                    break;
                case EquipmentSlot.Accessory:
                    _activeSlot.z = value;
                    break;
            }

            if (_materialInstance != null)
            {
                _materialInstance.SetVector("_SlotActive", _activeSlot);
            }
        }

        private void UpdateCardsVisibility()
        {
            foreach (var item in _animatedCards)
            {
                if (item.Card == null) continue;

                item.Card.SetVisible(_isCardsVisible);

                // If we are hiding the cards, snap them back to their base position
                // so they are not left at a random offset.
                if (!_isCardsVisible)
                {
                    item.Card.transform.localPosition = item.BaseLocalPosition;
                }
            }
        }

        /// <summary>
        /// Parents an equipment card to this panel and initializes its visual state and animation data.
        /// </summary>
        /// <param name="equipmentCard">The <see cref="CardInstance"/> to display on the panel.</param>
        /// <remarks>
        /// This method performs several visual setup steps:
        /// <list type="bullet">
        /// <item><description>Stops any active UI/movement tweens on the card.</description></item>
        /// <item><description>Snaps the card to the specific <see cref="EquipmentSlot"/> attach point.</description></item>
        /// <item><description>Assigns random noise offsets to create a unique "floating" animation for this specific card.</description></item>
        /// <item><description>Updates the panel's material shader to highlight the active slot.</description></item>
        /// </list>
        /// </remarks>
        public void AttachEquipment(CardInstance equipmentCard)
        {
            if (equipmentCard == null) return;

            equipmentCard.KillTweens();

            var slot = equipmentCard.Definition.EquipmentSlot;
            Vector3 localAttachPos = GetAttachPoint(slot);

            // Set parent and initial position.
            equipmentCard.transform.SetParent(this.transform);
            equipmentCard.transform.localPosition = localAttachPos;
            equipmentCard.transform.localRotation = Quaternion.identity;

            // Create and store the new animated item.
            var newItem = new AnimatedEquipment
            {
                Card = equipmentCard,
                BaseLocalPosition = localAttachPos,
                NoiseOffsetX = Random.Range(0f, 1000f),
                NoiseOffsetZ = Random.Range(0f, 1000f)
            };
            _animatedCards.Add(newItem);

            if (!_isCardsVisible)
            {
                ShowCards();
            }
            else
            {
                // If panel is already open, just ensure this new card is visible.
                equipmentCard.SetVisible(true);
            }

            UpdateSlotsVisual(slot, 1f);
            UpdatePanelVisibility();
        }

        /// <summary>
        /// Removes a card from the panel's internal tracking and clears its visual relationship with the parent.
        /// </summary>
        /// <param name="equipmentCard">The <see cref="CardInstance"/> to be removed from the display.</param>
        /// <remarks>
        /// Upon detachment, the method restores the card's visibility, resets its rotation, and clears its 
        /// parent-child relationship in the hierarchy. It also updates the panel's visibility 
        /// logic. If no cards remain, the panel's renderer and collider are disabled.
        /// </remarks>
        public void DetachEquipment(CardInstance equipmentCard)
        {
            if (equipmentCard == null) return;

            // Find the corresponding animated item.
            var itemToDetach = _animatedCards.Find(item => item.Card == equipmentCard);
            if (itemToDetach == null) return;

            // Un-parent, remove from list, and make sure it's visible.
            equipmentCard.transform.SetParent(null);
            _animatedCards.Remove(itemToDetach);
            equipmentCard.SetVisible(true);

            // Reset its rotation in case it was modified (though we don't in this anim).
            equipmentCard.transform.localRotation = Quaternion.identity;

            var slot = equipmentCard.Definition.EquipmentSlot;
            UpdateSlotsVisual(slot, 0f);

            UpdatePanelVisibility();
        }

        private Vector3 GetAttachPoint(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => weaponAttachPoint,
                EquipmentSlot.Armor => armorAttachPoint,
                EquipmentSlot.Accessory => accessoryAttachPoint,
                _ => Vector3.zero,
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 cardSize = new Vector3(0.8f, 0f, 1f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.TransformPoint(weaponAttachPoint), cardSize);
            Gizmos.DrawWireCube(transform.TransformPoint(armorAttachPoint), cardSize);
            Gizmos.DrawWireCube(transform.TransformPoint(accessoryAttachPoint), cardSize);
        }
#endif
    }
}
