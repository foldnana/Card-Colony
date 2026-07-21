using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Displays the party occupying a world-map location using the same interaction
    /// language as the character equipment panel: click to expand, click elsewhere
    /// to collapse, and a subtle floating motion while expanded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapPersonSlot : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("Animation")]
        [SerializeField, Min(0f)] private float animationRadius = 0.1f;
        [SerializeField, Min(0f)] private float animationSpeed = 1f;

        private CardInstance ownerCard;
        private Transform occupantOriginalParent;
        private Vector3 occupantOriginalScale = Vector3.one;
        private Quaternion occupantOriginalRotation = Quaternion.identity;
        private Vector3 baseLocalPosition;
        private float noiseOffsetX;
        private float noiseOffsetZ;
        private bool isReturningToSlot;

        public CardInstance Occupant { get; private set; }
        public bool IsExpanded { get; private set; }

        public void Initialize(CardInstance card)
        {
            ownerCard = card;
        }

        private void Start()
        {
            HideCards();
        }

        private void Update()
        {
            if (isReturningToSlot && Occupant != null)
            {
                Vector3 destination = GetWorldAttachPosition(baseLocalPosition);
                if ((Occupant.transform.position - destination).sqrMagnitude <= 0.0001f)
                    HideCards();
            }
            else if (IsExpanded && Occupant != null)
            {
                AnimateCards();
            }

        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Occupant == null)
                return;

            string displayName = Occupant.Definition != null
                ? Occupant.Definition.DisplayName
                : Occupant.gameObject.name;
            InfoPanel.Instance?.RegisterHover(("人物槽", $"• {displayName}"));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            InfoPanel.Instance?.UnregisterHover();
        }

        private void OnDisable()
        {
            InfoPanel.Instance?.UnregisterHover();
            HideCards();
        }

        private void OnDestroy()
        {
            if (Occupant != null)
                Detach(Occupant);
        }

        public Vector3 GetWorldAttachPosition(Vector3 localAttachPosition)
        {
            Transform anchor = ownerCard != null ? ownerCard.transform : transform;
            return anchor.TransformPoint(localAttachPosition);
        }

        public void Attach(
            CardInstance personCard,
            Vector3 localAttachPosition,
            float attachedScale,
            bool instant)
        {
            if (personCard == null || ownerCard == null)
                return;

            if (Occupant != null)
                Detach(Occupant);

            Occupant = personCard;
            occupantOriginalParent = personCard.transform.parent;
            occupantOriginalScale = personCard.transform.localScale;
            occupantOriginalRotation = personCard.transform.localRotation;
            baseLocalPosition = localAttachPosition;
            noiseOffsetX = Random.Range(0f, 1000f);
            noiseOffsetZ = Random.Range(0f, 1000f);

            personCard.KillTweens();
            personCard.transform.SetParent(ownerCard.transform, true);
            personCard.transform.localRotation = Quaternion.identity;
            personCard.transform.localScale =
                occupantOriginalScale * Mathf.Clamp(attachedScale, 0.1f, 1f);

            if (personCard.Stack != null)
            {
                personCard.Stack.SetTargetPosition(
                    GetWorldAttachPosition(localAttachPosition),
                    instant);
                if (instant)
                    personCard.transform.localPosition = localAttachPosition;
            }
            else
            {
                personCard.transform.localPosition = localAttachPosition;
            }

            if (instant)
            {
                HideCards();
            }
            else
            {
                IsExpanded = false;
                isReturningToSlot = true;
                Occupant.SetVisible(true);
            }
        }

        public void Detach(CardInstance personCard)
        {
            if (personCard == null || personCard != Occupant)
                return;

            Vector3 worldPosition = personCard.transform.position;
            personCard.KillTweens();
            personCard.transform.SetParent(occupantOriginalParent, true);
            personCard.transform.localRotation = occupantOriginalRotation;
            personCard.transform.localScale = occupantOriginalScale;
            personCard.SetVisible(true);

            Occupant = null;
            IsExpanded = false;
            isReturningToSlot = false;
            occupantOriginalParent = null;
            occupantOriginalScale = Vector3.one;
            occupantOriginalRotation = Quaternion.identity;
            baseLocalPosition = Vector3.zero;

            personCard.Stack?.SetTargetPosition(worldPosition, instant: true);
        }

        public void ToggleVisibility()
        {
            if (Occupant == null)
                return;

            if (IsExpanded)
                HideCards();
            else
                ShowCards();
        }

        public void ShowCards()
        {
            if (Occupant == null)
            {
                IsExpanded = false;
                return;
            }

            IsExpanded = true;
            isReturningToSlot = false;
            Occupant.SetVisible(true);
        }

        public void HideCards()
        {
            IsExpanded = false;
            isReturningToSlot = false;
            if (Occupant == null)
                return;

            Occupant.transform.localPosition = baseLocalPosition;
            Occupant.SetVisible(false);
        }

        private void AnimateCards()
        {
            float time = Time.time * animationSpeed;
            float x = (Mathf.PerlinNoise(time + noiseOffsetX, 0f) * 2f - 1f) * animationRadius;
            float z = (Mathf.PerlinNoise(0f, time + noiseOffsetZ) * 2f - 1f) * animationRadius;
            Occupant.transform.localPosition = baseLocalPosition + new Vector3(x, 0f, z);
        }

    }
}
