using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private TextMeshProUGUI _text;
        private TextMeshProUGUI text
        {
            get
            {
                if (_text == null) _text = GetComponent<TextMeshProUGUI>();
                return _text;
            }
        }

        private System.Action onClick;
        private System.Action<bool> onHover;

        public void Setup(string label, float fontSize, System.Action<bool> onHover = null, System.Action onClick = null)
        {
            gameObject.SetActive(true);
            text.text = label;
            text.fontSize = fontSize;
            this.onHover = onHover;
            this.onClick = onClick;
        }

        public void SetOnClick(System.Action onClick)
        {
            this.onClick = onClick;
        }

        public string GetText()
        {
            return this.text.text;
        }

        public void SetText(string text)
        {
            this.text.text = text;
        }

        public void SetColor(Color color)
        {
            text.color = color;
        }

        public void Deactivate()
        {
            text.text = "";
            text.fontStyle = FontStyles.Normal;
            onClick = null;
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (onClick != null)
            {
                onClick.Invoke();
                AudioManager.Instance.PlaySFX(AudioId.Click);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            text.fontStyle = FontStyles.Underline;
            onHover?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            text.fontStyle = FontStyles.Normal;
            onHover?.Invoke(false);
        }

        private void OnDisable()
        {
            text.fontStyle = FontStyles.Normal;
            onHover?.Invoke(false);
        }
    }
}
