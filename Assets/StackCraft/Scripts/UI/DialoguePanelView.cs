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

        private TMP_Text goodbyeButtonLabel;
        private string defaultGoodbyeLabel;

        public TMP_Text SpeakerNameLabel => speakerNameLabel;
        public TMP_Text DialogueTextLabel => dialogueTextLabel;
        public Button ReplyButton => replyButton;
        public Button GoodbyeButton => goodbyeButton;
        public RawImage PortraitBackground => portraitBackground;
        public RawImage Portrait => portrait;

        private void Awake()
        {
            if (goodbyeButton != null)
            {
                goodbyeButtonLabel =
                    goodbyeButton.GetComponentInChildren<TMP_Text>(true);
                defaultGoodbyeLabel = goodbyeButtonLabel?.text;
            }
        }

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
            if (goodbyeButtonLabel != null &&
                !string.IsNullOrWhiteSpace(defaultGoodbyeLabel))
            {
                goodbyeButtonLabel.text = defaultGoodbyeLabel;
            }

            bool hasReply = !string.IsNullOrWhiteSpace(speaker.DialogueReplyText);
            if (replyButton != null)
                replyButton.gameObject.SetActive(hasReply);
            if (replyButtonLabel != null)
                replyButtonLabel.text = speaker.DialogueReplyText;
        }

        public void ShowQuest(
            CardDefinition speaker,
            string dialogue,
            string primaryLabel,
            Action onPrimary,
            string secondaryLabel,
            Action onSecondary)
        {
            if (speaker == null)
                return;

            Show(speaker, onPrimary, onSecondary);
            if (dialogueTextLabel != null)
                dialogueTextLabel.text = dialogue;
            bool hasPrimary = !string.IsNullOrWhiteSpace(primaryLabel) &&
                              onPrimary != null;
            if (replyButton != null)
                replyButton.gameObject.SetActive(hasPrimary);
            if (replyButtonLabel != null)
                replyButtonLabel.text = primaryLabel;
            if (goodbyeButton != null)
            {
                goodbyeButton.gameObject.SetActive(onSecondary != null);
                if (goodbyeButtonLabel != null &&
                    !string.IsNullOrWhiteSpace(secondaryLabel))
                {
                    goodbyeButtonLabel.text = secondaryLabel;
                }
            }
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
