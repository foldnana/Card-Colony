using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class BackpackItemView : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private BackpackView owner;
        private CanvasGroup canvasGroup;

        public string EntryId { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(
            BackpackEntryData entry,
            CardDefinition definition,
            BackpackView backpackView,
            TMP_FontAsset font)
        {
            EntryId = entry.InstanceId;
            owner = backpackView;
            canvasGroup = GetComponent<CanvasGroup>();

            RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            RectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform.anchoredPosition = Vector2.zero;
            RectTransform.sizeDelta = new Vector2(112f, 132f);
            RectTransform.localScale = Vector3.one;

            Image background = GetComponent<Image>();
            background.color = new Color(0.88f, 0.86f, 0.80f, 1f);
            background.raycastTarget = true;

            CardCategory category = definition != null
                ? definition.Category
                : CardCategory.None;
            string displayName = definition != null
                ? definition.DisplayName
                : "未知物品";

            Image header = CreateImage(
                "Header",
                transform,
                CategoryColor(category));
            SetRect(
                header.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -14f),
                new Vector2(0f, 28f));

            TMP_Text title = CreateText(
                "Title",
                transform,
                font,
                displayName,
                16f,
                Color.white,
                TextAlignmentOptions.Center);
            SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -14f),
                new Vector2(-8f, 28f));

            GameObject artObject = new GameObject(
                "Art",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            artObject.transform.SetParent(transform, false);
            RawImage art = artObject.GetComponent<RawImage>();
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
                new Vector2(0f, -9f),
                new Vector2(78f, 78f));

            string detail = category == CardCategory.Currency
                ? "金币"
                : category == CardCategory.Consumable
                    ? $"食物  {entry.Card.CurrentNutrition}"
                    : definition == null
                        ? "旧存档物品"
                        : string.Empty;
            TMP_Text detailText = CreateText(
                "Detail",
                transform,
                font,
                detail,
                13f,
                new Color(0.18f, 0.19f, 0.20f),
                TextAlignmentOptions.Center);
            SetRect(
                detailText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 9f),
                new Vector2(-8f, 20f));
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
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

        private static Color CategoryColor(CardCategory category)
        {
            return category switch
            {
                CardCategory.Consumable => new Color(0.30f, 0.55f, 0.33f),
                CardCategory.Currency => new Color(0.73f, 0.52f, 0.16f),
                CardCategory.Material => new Color(0.48f, 0.36f, 0.27f),
                CardCategory.Equipment => new Color(0.24f, 0.45f, 0.67f),
                CardCategory.Valuable => new Color(0.49f, 0.31f, 0.58f),
                _ => new Color(0.33f, 0.38f, 0.40f)
            };
        }
    }
}
