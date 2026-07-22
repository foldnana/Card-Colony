using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class DialoguePanelView : MonoBehaviour
    {
        [SerializeField] private RawImage portraitBackground;
        [SerializeField] private RawImage portrait;
        [SerializeField] private TMP_Text speakerNameLabel;
        [SerializeField] private TMP_Text dialogueTextLabel;
        [SerializeField] private Button replyButton;
        [SerializeField] private TMP_Text replyButtonLabel;
        [SerializeField] private Button goodbyeButton;

        public TMP_Text SpeakerNameLabel => speakerNameLabel;
        public TMP_Text DialogueTextLabel => dialogueTextLabel;
        public Button ReplyButton => replyButton;
        public Button GoodbyeButton => goodbyeButton;
        public RawImage PortraitBackground => portraitBackground;
        public RawImage Portrait => portrait;

        public void Show(CardDefinition speaker, Action onReply, Action onGoodbye)
        {
            if (speaker == null)
                return;

            gameObject.SetActive(true);

            if (portraitBackground != null)
            {
                portraitBackground.texture = speaker.BaseTextureOverride;
                portraitBackground.gameObject.SetActive(speaker.BaseTextureOverride != null);
            }
            if (portrait != null)
                portrait.texture = speaker.ArtTexture;
            if (speakerNameLabel != null)
                speakerNameLabel.text = speaker.DisplayName;
            if (dialogueTextLabel != null)
                dialogueTextLabel.text = speaker.DialogueOpeningText;

            ConfigureButton(replyButton, onReply);
            ConfigureButton(goodbyeButton, onGoodbye);

            bool hasReply = !string.IsNullOrWhiteSpace(speaker.DialogueReplyText);
            if (replyButton != null)
                replyButton.gameObject.SetActive(hasReply);
            if (replyButtonLabel != null)
                replyButtonLabel.text = speaker.DialogueReplyText;
        }

        public void ShowResponse(string response)
        {
            if (dialogueTextLabel != null)
                dialogueTextLabel.text = response;
            if (replyButton != null)
                replyButton.gameObject.SetActive(false);
        }

        public void Hide()
        {
            replyButton?.onClick.RemoveAllListeners();
            goodbyeButton?.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
        }

        private static void ConfigureButton(Button button, Action callback)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback?.Invoke());
        }
    }
}
