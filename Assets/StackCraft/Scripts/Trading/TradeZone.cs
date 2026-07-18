using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(MeshRenderer), typeof(BoxCollider))]
    public abstract class TradeZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Visual Effects")]
        [SerializeField, Tooltip("A particle effect prefab (puff) that is instantiated when a successful transaction occurs.")]
        private PuffParticle puffParticle;

        [SerializeField, Tooltip("Material used by the highlight system to draw the zone outline.")]
        private Material outlineMaterial;

        private Vector3 spawnOffset;

        protected Vector3 spawnPosition => transform.position + spawnOffset;

        private Highlight highlight;

        public virtual void Initialize(CardDefinition definition, Vector3 spawnOffset)
        {
            this.spawnOffset = spawnOffset;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            InfoPanel.Instance?.RegisterHover(GetInfo());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            InfoPanel.Instance?.UnregisterHover();
        }

        private void OnDisable()
        {
            InfoPanel.Instance?.UnregisterHover();
        }

        public bool TryTradeAndConsumeStack(CardStack droppedStack)
        {
            if (CanTrade(droppedStack))
            {
                PlayPuffParticle();

                ProcessTransaction(droppedStack);
                return droppedStack.Cards.Count == 0;
            }
            return false;
        }

        public abstract bool CanTrade(CardStack droppedStack);
        protected abstract void ProcessTransaction(CardStack droppedStack);

        /// <summary>
        /// Controls the visual highlighting state of the zone.
        /// Creates the necessary <see cref="Highlight"/> component if it does not already exist.
        /// </summary>
        /// <param name="value">If true, the zone is highlighted; otherwise, the highlight is hidden.</param>
        public void SetHighlighted(bool value)
        {
            if (highlight == null)
            {
                var mesh = GetComponent<MeshFilter>().mesh;
                highlight = new Highlight(transform, mesh, outlineMaterial);
            }
            else highlight.SetActive(value);
        }

        public void PlayPuffParticle()
        {
            Instantiate(puffParticle, transform.position, Quaternion.identity);
        }

        public abstract (string, string) GetInfo();
    }
}
