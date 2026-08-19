using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class NarrativePresentationView : MonoBehaviour
    {
        [SerializeField] private GameObject mapMask;
        [SerializeField] private GameObject visualNovelRoot;
        [SerializeField] private RawImage fullscreenImage;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text modeLabel;

        private Action skipRequested;

        public static NarrativePresentationView Instance { get; private set; }
        public bool IsVisualNovelMode =>
            visualNovelRoot != null && visualNovelRoot.activeSelf;
        public bool IsFullscreenImageVisible =>
            fullscreenImage != null && fullscreenImage.gameObject.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (skipButton != null)
                skipButton.onClick.AddListener(HandleSkipClicked);
            Restore();
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.onClick.RemoveListener(HandleSkipClicked);
            if (Instance == this)
                Instance = null;
        }

        public void BindSkip(Action callback)
        {
            skipRequested = callback;
            if (skipButton != null)
                skipButton.gameObject.SetActive(callback != null);
        }

        public void SetVisualNovelMode(bool visible)
        {
            visualNovelRoot?.SetActive(visible);
            if (mapMask != null)
                mapMask.SetActive(visible || IsFullscreenImageVisible);
            if (modeLabel != null)
                modeLabel.text = visible ? "剧情" : string.Empty;
        }

        public void ShowFullscreenImage(Texture texture)
        {
            if (fullscreenImage == null)
                return;
            fullscreenImage.texture = texture;
            fullscreenImage.gameObject.SetActive(texture != null);
            mapMask?.SetActive(texture != null || IsVisualNovelMode);
        }

        public void HideFullscreenImage()
        {
            if (fullscreenImage != null)
            {
                fullscreenImage.texture = null;
                fullscreenImage.gameObject.SetActive(false);
            }
            if (mapMask != null)
                mapMask.SetActive(IsVisualNovelMode);
        }

        public GameObject ShowActorCallout(
            NarrativeActorHandle actor,
            string text,
            Color color)
        {
            if (actor?.Card == null || string.IsNullOrWhiteSpace(text))
                return null;
            bool usesWorldCanvas = WorldCanvas.Instance != null;
            Transform parent = usesWorldCanvas
                ? WorldCanvas.Instance.transform
                : actor.Card.transform;
            var callout = new GameObject(
                $"NarrativeCallout_{actor.RoleId}",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            callout.transform.SetParent(parent, false);
            RectTransform rect = callout.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2.5f, 0.65f);
            if (usesWorldCanvas)
            {
                rect.position = actor.Card.transform.position +
                    new Vector3(0f, 0.35f, 0.7f);
            }
            else
            {
                rect.localPosition = new Vector3(0f, 0.35f, 0.7f);
            }
            rect.rotation = Quaternion.Euler(90f, 0f, 0f);
            Image background = callout.GetComponent<Image>();
            background.color = new Color(0.035f, 0.085f, 0.13f, 0.94f);
            background.raycastTarget = false;

            var labelObject = new GameObject(
                "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(callout.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0.12f, 0.06f);
            labelRect.offsetMax = new Vector2(-0.12f, -0.06f);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = color;
            label.fontSize = 0.26f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return callout;
        }

        public void Restore()
        {
            skipRequested = null;
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
            if (visualNovelRoot != null)
                visualNovelRoot.SetActive(false);
            HideFullscreenImage();
            mapMask?.SetActive(false);
            if (modeLabel != null)
                modeLabel.text = string.Empty;
        }

        private void HandleSkipClicked()
        {
            skipRequested?.Invoke();
        }
    }
}
