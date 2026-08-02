using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class BackpackItemView : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private BackpackView owner;
        private CanvasGroup canvasGroup;
        private BackpackEntryData entry;
        private CardDefinition definition;
        private Image background;
        private Outline selectionOutline;
        private RawImage art;
        private Image dragHeader;
        private TMP_Text dragTitle;
        private readonly List<string> entryIds = new();

        public string EntryId { get; private set; }
        public IReadOnlyList<string> EntryIds => entryIds;
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(
            BackpackEntryData entry,
            CardDefinition definition,
            BackpackView backpackView,
            TMP_FontAsset font)
        {
            Bind(entry, definition, backpackView, font, 1);
        }

        public void Bind(
            BackpackEntryData entry,
            CardDefinition definition,
            BackpackView backpackView,
            TMP_FontAsset font,
            int quantity)
        {
            Bind(
                entry,
                definition,
                backpackView,
                font,
                quantity,
                entry != null ? new[] { entry.InstanceId } : null);
        }

        public void Bind(
            BackpackEntryData entry,
            CardDefinition definition,
            BackpackView backpackView,
            TMP_FontAsset font,
            int quantity,
            IReadOnlyList<string> groupedEntryIds)
        {
            EntryId = entry.InstanceId;
            entryIds.Clear();
            if (groupedEntryIds != null)
            {
                foreach (string entryId in groupedEntryIds)
                {
                    if (!string.IsNullOrWhiteSpace(entryId) &&
                        !entryIds.Contains(entryId))
                    {
                        entryIds.Add(entryId);
                    }
                }
            }
            if (entryIds.Count == 0 && !string.IsNullOrWhiteSpace(EntryId))
                entryIds.Add(EntryId);
            owner = backpackView;
            this.entry = entry;
            this.definition = definition;
            canvasGroup = GetComponent<CanvasGroup>();

            RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            RectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform.anchoredPosition = Vector2.zero;
            RectTransform.sizeDelta = new Vector2(128f, 128f);
            RectTransform.localScale = Vector3.one;

            background = GetComponent<Image>();
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = new Color(0.035f, 0.095f, 0.145f, 1f);
            background.raycastTarget = true;
            selectionOutline = GetComponent<Outline>();
            if (selectionOutline == null)
                selectionOutline = gameObject.AddComponent<Outline>();
            selectionOutline.effectColor =
                new Color(0.18f, 0.86f, 1f, 0.94f);
            selectionOutline.effectDistance = new Vector2(2f, -2f);
            selectionOutline.useGraphicAlpha = true;
            selectionOutline.enabled = false;

            GameObject artObject = new GameObject(
                "Art",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            artObject.transform.SetParent(transform, false);
            art = artObject.GetComponent<RawImage>();
            art.texture = definition != null ? definition.ArtTexture : null;
            art.color = definition != null && definition.ArtTexture != null
                ? Color.white
                : new Color(0f, 0f, 0f, 0f);
            art.raycastTarget = false;
            SetRect(
                art.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(92f, 92f));

            if (quantity > 1)
            {
                Image quantityBadge = CreateImage(
                    "QuantityBadge",
                    transform,
                    new Color(0.015f, 0.025f, 0.035f, 0.94f));
                SetRect(
                    quantityBadge.rectTransform,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(-4f, 4f),
                    new Vector2(36f, 26f));
                TMP_Text quantityText = CreateText(
                    "Quantity",
                    quantityBadge.transform,
                    font,
                    $"×{quantity}",
                    15f,
                    Color.white,
                    TextAlignmentOptions.Center);
                Stretch(quantityText.rectTransform);
            }

            dragHeader = CreateImage(
                "DragHeader",
                transform,
                DragHeaderColor(definition));
            SetRect(
                dragHeader.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 30f));
            dragTitle = CreateText(
                "DragTitle",
                dragHeader.transform,
                font,
                definition != null ? definition.DisplayName : "未知物品",
                17f,
                Color.white,
                TextAlignmentOptions.Center);
            Stretch(dragTitle.rectTransform);
            dragHeader.gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Select();
        }

        public void Select()
        {
            owner?.SelectItem(this, entry, definition);
        }

        internal void SetSelected(bool selected)
        {
            if (selectionOutline != null)
                selectionOutline.enabled = selected;
            if (background != null)
            {
                background.color = selected
                    ? new Color(0.055f, 0.18f, 0.26f, 1f)
                    : new Color(0.035f, 0.095f, 0.145f, 1f);
            }
        }

        internal void SetWorldDragPresentation(bool useWorldCardStyle)
        {
            if (useWorldCardStyle)
            {
                RectTransform.sizeDelta = new Vector2(120f, 156f);
                if (background != null)
                    background.color = new Color(0.91f, 0.90f, 0.86f, 1f);
                if (art != null)
                {
                    SetRect(
                        art.rectTransform,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -10f),
                        new Vector2(98f, 98f));
                }
                if (dragHeader != null)
                    dragHeader.gameObject.SetActive(true);
                return;
            }

            RectTransform.sizeDelta = new Vector2(128f, 128f);
            if (background != null)
            {
                background.color = selectionOutline != null &&
                    selectionOutline.enabled
                        ? new Color(0.055f, 0.18f, 0.26f, 1f)
                        : new Color(0.035f, 0.095f, 0.145f, 1f);
            }
            if (art != null)
            {
                SetRect(
                    art.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(92f, 92f));
            }
            if (dragHeader != null)
                dragHeader.gameObject.SetActive(false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Select();
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;
            owner?.BeginItemDrag(this, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            owner?.UpdateItemDrag(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = true;
            owner?.EndItemDrag(this, eventData.position);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static Color DragHeaderColor(CardDefinition definition)
        {
            if (definition == null)
                return new Color(0.38f, 0.40f, 0.42f, 1f);
            if (definition.Faction == CardFaction.Mob ||
                definition.Category == CardCategory.Mob)
            {
                return new Color(0.92f, 0.24f, 0.23f, 1f);
            }

            return definition.Category switch
            {
                CardCategory.Character => new Color(0.28f, 0.55f, 0.91f, 1f),
                CardCategory.Consumable => new Color(0.96f, 0.40f, 0.16f, 1f),
                CardCategory.Material => new Color(0.48f, 0.38f, 0.30f, 1f),
                CardCategory.Equipment => new Color(0.24f, 0.48f, 0.74f, 1f),
                CardCategory.Currency => new Color(0.82f, 0.62f, 0.17f, 1f),
                CardCategory.Valuable => new Color(0.53f, 0.31f, 0.66f, 1f),
                _ => new Color(0.42f, 0.40f, 0.38f, 1f)
            };
        }
    }
}
