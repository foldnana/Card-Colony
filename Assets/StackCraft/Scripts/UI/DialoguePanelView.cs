using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    public readonly struct DialogueChoiceOption
    {
        public string Label { get; }
        public Action Callback { get; }

        public DialogueChoiceOption(string label, Action callback)
        {
            Label = label;
            Callback = callback;
        }
    }

    [DisallowMultipleComponent]
    public sealed class DialoguePanelView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RawImage portraitBackground;
        [SerializeField] private RawImage portrait;
        [SerializeField] private TMP_Text speakerNameLabel;
        [SerializeField] private TMP_Text dialogueTextLabel;
        [SerializeField] private Button replyButton;
        [SerializeField] private TMP_Text replyButtonLabel;
        [SerializeField] private Button goodbyeButton;
        [Header("Choice tray")]
        [SerializeField] private GameObject inlineActions;
        [SerializeField] private GameObject choiceTray;
        [SerializeField] private TMP_Text choiceTitleLabel;
        [SerializeField] private RectTransform choiceContent;
        [SerializeField] private Button choiceButtonTemplate;

        private TMP_Text goodbyeButtonLabel;
        private string defaultGoodbyeLabel;
        private Action responseGoodbyeCallback;
        private Action narrativeAdvanceCallback;

        public TMP_Text SpeakerNameLabel => speakerNameLabel;
        public TMP_Text DialogueTextLabel => dialogueTextLabel;
        public Button ReplyButton => replyButton;
        public Button GoodbyeButton => goodbyeButton;
        public RawImage PortraitBackground => portraitBackground;
        public RawImage Portrait => portrait;
        public GameObject ChoiceTray => choiceTray;

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

            narrativeAdvanceCallback = null;
            ShowSpeaker(speaker, speaker.DialogueOpeningText);
            responseGoodbyeCallback = onGoodbye;
            if (inlineActions != null)
                inlineActions.SetActive(false);

            bool hasReply = !string.IsNullOrWhiteSpace(speaker.DialogueReplyText);
            var options = new List<DialogueChoiceOption>(2);
            if (hasReply && onReply != null)
            {
                options.Add(new DialogueChoiceOption(
                    speaker.DialogueReplyText,
                    onReply));
            }
            if (onGoodbye != null)
            {
                options.Add(new DialogueChoiceOption(
                    GetGoodbyeLabel(),
                    onGoodbye));
            }
            BuildChoiceTray("你的选择", options);
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

            narrativeAdvanceCallback = null;
            bool hasPrimary = !string.IsNullOrWhiteSpace(primaryLabel) &&
                              onPrimary != null;
            bool hasSecondary = !string.IsNullOrWhiteSpace(secondaryLabel) &&
                                onSecondary != null;
            responseGoodbyeCallback = hasSecondary ? onSecondary : null;
            var options = new List<DialogueChoiceOption>(2);
            if (hasPrimary)
                options.Add(new DialogueChoiceOption(primaryLabel, onPrimary));
            if (hasSecondary)
                options.Add(new DialogueChoiceOption(secondaryLabel, onSecondary));
            ShowChoices(speaker, dialogue, "你的选择", options);
        }

        public void ShowChoices(
            CardDefinition speaker,
            string dialogue,
            string title,
            IReadOnlyList<DialogueChoiceOption> options)
        {
            if (speaker == null)
                return;

            narrativeAdvanceCallback = null;
            ShowSpeaker(speaker, dialogue);
            if (inlineActions != null)
                inlineActions.SetActive(false);
            BuildChoiceTray(title, options);
        }

        public void ShowNarrative(
            string speakerName,
            Texture speakerPortrait,
            string dialogue,
            string title,
            IReadOnlyList<DialogueChoiceOption> options)
        {
            gameObject.SetActive(true);
            narrativeAdvanceCallback = null;
            responseGoodbyeCallback = null;
            if (portraitBackground != null)
                portraitBackground.gameObject.SetActive(false);
            if (portrait != null)
            {
                portrait.texture = speakerPortrait;
                portrait.gameObject.SetActive(speakerPortrait != null);
            }
            if (speakerNameLabel != null)
                speakerNameLabel.text = speakerName ?? string.Empty;
            if (dialogueTextLabel != null)
                dialogueTextLabel.text = dialogue ?? string.Empty;
            if (inlineActions != null)
                inlineActions.SetActive(false);
            BuildChoiceTray(title, options);
        }

        /// <summary>
        /// Shows a linear narrative line. The player advances by clicking the
        /// dialogue panel itself instead of choosing a fake "continue" option.
        /// </summary>
        public void ShowNarrativeLine(
            string speakerName,
            Texture speakerPortrait,
            string dialogue,
            Action onAdvance)
        {
            gameObject.SetActive(true);
            responseGoodbyeCallback = null;
            narrativeAdvanceCallback = onAdvance;
            if (portraitBackground != null)
                portraitBackground.gameObject.SetActive(false);
            if (portrait != null)
            {
                portrait.texture = speakerPortrait;
                portrait.gameObject.SetActive(speakerPortrait != null);
            }
            if (speakerNameLabel != null)
                speakerNameLabel.text = speakerName ?? string.Empty;
            if (dialogueTextLabel != null)
                dialogueTextLabel.text = dialogue ?? string.Empty;
            if (inlineActions != null)
                inlineActions.SetActive(false);
            HideChoiceTray();
        }

        public void AdvanceNarrative()
        {
            Action callback = narrativeAdvanceCallback;
            if (callback == null)
                return;
            narrativeAdvanceCallback = null;
            callback.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button == PointerEventData.InputButton.Left)
            {
                AdvanceNarrative();
            }
        }

        public void ShowResponse(string response)
        {
            narrativeAdvanceCallback = null;
            HideChoiceTray();
            if (inlineActions != null)
                inlineActions.SetActive(false);
            if (dialogueTextLabel != null)
                dialogueTextLabel.text = response;
            if (responseGoodbyeCallback != null)
            {
                BuildChoiceTray(
                    "结束交谈",
                    new[]
                    {
                        new DialogueChoiceOption(
                            GetGoodbyeLabel(),
                            responseGoodbyeCallback)
                    });
            }
        }

        public void Hide()
        {
            replyButton?.onClick.RemoveAllListeners();
            goodbyeButton?.onClick.RemoveAllListeners();
            responseGoodbyeCallback = null;
            narrativeAdvanceCallback = null;
            HideChoiceTray();
            gameObject.SetActive(false);
        }

        private void ShowSpeaker(CardDefinition speaker, string dialogue)
        {
            gameObject.SetActive(true);

            if (portraitBackground != null)
            {
                portraitBackground.texture = speaker.BaseTextureOverride;
                portraitBackground.gameObject.SetActive(
                    speaker.BaseTextureOverride != null);
            }
            if (portrait != null)
            {
                portrait.texture = speaker.ArtTexture;
                portrait.gameObject.SetActive(speaker.ArtTexture != null);
            }
            if (speakerNameLabel != null)
                speakerNameLabel.text = speaker.DisplayName;
            if (dialogueTextLabel != null)
                dialogueTextLabel.text = dialogue;
        }

        private string GetGoodbyeLabel()
        {
            return string.IsNullOrWhiteSpace(defaultGoodbyeLabel)
                ? "告辞"
                : defaultGoodbyeLabel;
        }

        private void BuildChoiceTray(
            string title,
            IReadOnlyList<DialogueChoiceOption> options)
        {
            ClearChoiceButtons();
            if (choiceTray == null || choiceContent == null ||
                choiceButtonTemplate == null || options == null)
            {
                choiceTray?.SetActive(false);
                return;
            }

            if (choiceTitleLabel != null)
                choiceTitleLabel.text = string.IsNullOrWhiteSpace(title)
                    ? "请选择"
                    : title;

            int visibleCount = 0;
            for (int i = 0; i < options.Count; i++)
            {
                DialogueChoiceOption option = options[i];
                if (string.IsNullOrWhiteSpace(option.Label) ||
                    option.Callback == null)
                {
                    continue;
                }

                DialogueChoiceOption captured = option;
                Button button = Instantiate(
                    choiceButtonTemplate,
                    choiceContent,
                    false);
                button.name = $"ChoiceButton_{visibleCount + 1}";
                button.gameObject.SetActive(true);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = captured.Label;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    HideChoiceTray();
                    captured.Callback.Invoke();
                });
                visibleCount++;
            }

            choiceTray.SetActive(visibleCount > 0);
        }

        private void HideChoiceTray()
        {
            ClearChoiceButtons();
            choiceTray?.SetActive(false);
        }

        private void ClearChoiceButtons()
        {
            if (choiceContent == null)
                return;

            for (int i = choiceContent.childCount - 1; i >= 0; i--)
            {
                Transform child = choiceContent.GetChild(i);
                if (choiceButtonTemplate != null &&
                    child == choiceButtonTemplate.transform)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

    }
}
