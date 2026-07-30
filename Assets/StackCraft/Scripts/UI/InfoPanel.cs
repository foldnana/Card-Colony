using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.StackCraft
{
    public enum InfoPriority
    {
        Hover,      // Lowest priority, for mouse hover info
        Sequence,   // For non-critical sequences like vendor unlocks
        Modal       // For critical, game-pausing events like end-of-day
    }

    public readonly struct InfoPanelAction
    {
        public string Label { get; }
        public System.Action Callback { get; }

        public InfoPanelAction(string label, System.Action callback)
        {
            Label = label;
            Callback = callback;
        }
    }

    public class InfoPanel : MonoBehaviour
    {
        public static InfoPanel Instance { get; private set; }

        [SerializeField, Tooltip("The TextMeshProUGUI component where all combined header and body information is displayed.")]
        private TextMeshProUGUI infoText;

        [SerializeField, Tooltip("The font size used for the Header portion of the info text (e.g., the title of a zone or item).")]
        private int headerSize = 32;

        [SerializeField, Tooltip("The font size used for the Body portion of the info text (e.g., the description or details).")]
        private int bodySize = 26;

        [SerializeField, Tooltip("The TextButton component that is displayed when the highest priority info request includes a mandatory action.")]
        private TextButton actionButton;

        private const float PanelWidth = 420f;
        private const float ActionButtonHeight = 54f;
        private const float PanelBottomOffset = 102f;

        private RectTransform panelRect;
        private RectTransform textRect;
        private CanvasGroup visibilityGroup;
        private readonly List<TextButton> actionButtons = new();

        public bool IsWorldMapSuppressed { get; private set; }

        private static int s_requestCounter = 0;

        private class InfoRequest
        {
            public int RequestID;
            public InfoPriority Priority;
            public (string header, string body) Info;
            public List<InfoPanelAction> Actions = new();
        }

        private readonly object hoverRequester = "HoverRequester";
        private readonly Dictionary<object, InfoRequest> activeRequests = new();
        private (string header, string body) lastDisplayedInfo;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            visibilityGroup = GetComponent<CanvasGroup>();
            if (visibilityGroup == null)
                visibilityGroup = gameObject.AddComponent<CanvasGroup>();
            ConfigureLayout();
            RefreshInfo();
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetWorldMapSuppressed(bool suppressed)
        {
            IsWorldMapSuppressed = suppressed;
            ApplyVisibility();
        }

        /// <summary>
        /// Submits a request to display information and an optional action button on the UI panel.
        /// </summary>
        /// <param name="requester">The object responsible for this request. Used for identification when clearing the request.</param>
        /// <param name="priority">The <see cref="InfoPriority"/> of the message. Higher priority requests override lower ones.</param>
        /// <param name="info">A tuple containing the header (title) and body text to be displayed.</param>
        /// <param name="buttonLabel">The text to appear on the action button. If null or empty, the button is hidden.</param>
        /// <param name="buttonAction">The callback to execute when the action button is clicked.</param>
        /// <remarks>
        /// The system uses a priority-based queue. If multiple requests exist, the panel displays the one with the highest 
        /// <see cref="InfoPriority"/>. If priorities are equal, the most recent request (highest RequestID) takes precedence.
        /// </remarks>
        public void RequestInfoDisplay(object requester, InfoPriority priority, (string header, string body) info, string buttonLabel = null, System.Action buttonAction = null)
        {
            if (requester == null) return;

            var request = new InfoRequest
            {
                RequestID = s_requestCounter++,
                Priority = priority,
                Info = info
            };
            if (!string.IsNullOrEmpty(buttonLabel) && buttonAction != null)
                request.Actions.Add(
                    new InfoPanelAction(buttonLabel, buttonAction));
            activeRequests[requester] = request;

            RefreshInfo();
        }

        public void RequestInfoDisplayWithActions(
            object requester,
            InfoPriority priority,
            (string header, string body) info,
            params InfoPanelAction[] actions)
        {
            if (requester == null)
                return;

            activeRequests[requester] = new InfoRequest
            {
                RequestID = s_requestCounter++,
                Priority = priority,
                Info = info,
                Actions = actions?
                    .Where(action =>
                        !string.IsNullOrWhiteSpace(action.Label) &&
                        action.Callback != null)
                    .ToList() ??
                    new List<InfoPanelAction>()
            };

            RefreshInfo();
        }

        public void ClearInfoRequest(object requester)
        {
            if (requester == null || !activeRequests.ContainsKey(requester)) return;

            activeRequests.Remove(requester);
            RefreshInfo();
        }

        public void RegisterHover((string header, string body) info)
        {
            RequestInfoDisplay(hoverRequester, InfoPriority.Hover, info);
        }

        public void UnregisterHover()
        {
            ClearInfoRequest(hoverRequester);
        }

        private void RefreshInfo()
        {
            if (activeRequests.Count > 0)
            {
                var highestPriorityRequest = activeRequests.Values
                    .OrderByDescending(req => req.Priority)
                    .ThenByDescending(req => req.RequestID)
                    .First();

                UpdateInfo(highestPriorityRequest.Info);

                SetActionButtons(highestPriorityRequest.Actions);
            }
            else
            {
                ClearInfo();
                SetActionButtons(null);
            }

            RebuildLayout();
        }

        private void UpdateInfo((string header, string body) newInfo)
        {
            if (newInfo == lastDisplayedInfo) return;

            string headerText = "", bodyText = "";

            if (!string.IsNullOrEmpty(newInfo.header))
            {
                headerText = $"<size={headerSize}><color=#F2C94C>【{newInfo.header}】</color></size>\n";
            }
            if (!string.IsNullOrEmpty(newInfo.body))
            {
                bodyText = $"<size={bodySize}>{newInfo.body}</size>";
            }

            infoText.text = headerText + bodyText;
            lastDisplayedInfo = newInfo;
        }

        private void ConfigureLayout()
        {
            panelRect = (RectTransform)transform;
            textRect = infoText.rectTransform;
            actionButtons.Clear();
            if (actionButton != null)
                actionButtons.Add(actionButton);

            // These two components both try to drive the same RectTransforms. Their
            // result depends on initialization order and can collapse Chinese text
            // to a one-character column, so this panel uses deterministic sizing.
            if (TryGetComponent(out VerticalLayoutGroup layoutGroup))
                layoutGroup.enabled = false;
            if (TryGetComponent(out ContentSizeFitter sizeFitter))
                sizeFitter.enabled = false;

            panelRect.anchoredPosition = new Vector2(
                panelRect.anchoredPosition.x,
                PanelBottomOffset);
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PanelWidth);

            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;

            infoText.margin = new Vector4(24f, 18f, 24f, 20f);
            infoText.lineSpacing = 6f;
            infoText.paragraphSpacing = 12f;
            infoText.alignment = TextAlignmentOptions.TopLeft;
            infoText.enableWordWrapping = true;

            RebuildLayout();
        }

        private void RebuildLayout()
        {
            if (panelRect == null || textRect == null)
                return;

            float textHeight = 0f;
            if (!string.IsNullOrEmpty(infoText.text))
            {
                float textWidth = PanelWidth - infoText.margin.x - infoText.margin.z;
                Vector2 preferred = infoText.GetPreferredValues(infoText.text, textWidth, 0f);
                textHeight = Mathf.Ceil(preferred.y + infoText.margin.y + infoText.margin.w);
            }

            textRect.sizeDelta = new Vector2(0f, textHeight);

            List<TextButton> visibleButtons = actionButtons
                .Where(button =>
                    button != null && button.gameObject.activeSelf)
                .ToList();
            float buttonHeight =
                visibleButtons.Count > 0 ? ActionButtonHeight : 0f;
            float buttonWidth = visibleButtons.Count > 0
                ? PanelWidth / visibleButtons.Count
                : PanelWidth;
            for (int index = 0; index < visibleButtons.Count; index++)
            {
                RectTransform buttonRect =
                    (RectTransform)visibleButtons[index].transform;
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(0f, 1f);
                buttonRect.pivot = new Vector2(0f, 1f);
                buttonRect.anchoredPosition = new Vector2(
                    buttonWidth * index,
                    -textHeight);
                buttonRect.sizeDelta = new Vector2(
                    buttonWidth,
                    buttonHeight);
            }

            panelRect.sizeDelta = new Vector2(PanelWidth, textHeight + buttonHeight);
            infoText.ForceMeshUpdate();
        }

        private void ClearInfo()
        {
            if (string.IsNullOrEmpty(infoText.text) &&
                string.IsNullOrEmpty(lastDisplayedInfo.header) &&
                string.IsNullOrEmpty(lastDisplayedInfo.body))
            {
                return;
            }

            infoText.text = "";
            lastDisplayedInfo = ("", "");
        }

        private void SetActionButtons(
            IReadOnlyList<InfoPanelAction> actions)
        {
            int required = actions?.Count ?? 0;
            while (actionButtons.Count < required)
            {
                TextButton clone = Instantiate(
                    actionButton,
                    actionButton.transform.parent);
                clone.name = $"ActionButton{actionButtons.Count + 1}";
                clone.Deactivate();
                actionButtons.Add(clone);
            }

            for (int index = 0; index < actionButtons.Count; index++)
            {
                TextButton button = actionButtons[index];
                if (index >= required)
                {
                    button.Deactivate();
                    continue;
                }

                InfoPanelAction action = actions[index];
                button.Setup(
                    action.Label,
                    bodySize,
                    onClick: () => action.Callback?.Invoke());
            }
        }

        private void ApplyVisibility()
        {
            if (visibilityGroup == null)
                visibilityGroup = GetComponent<CanvasGroup>();
            if (visibilityGroup == null)
                return;

            visibilityGroup.alpha = IsWorldMapSuppressed ? 0f : 1f;
            visibilityGroup.interactable = !IsWorldMapSuppressed;
            visibilityGroup.blocksRaycasts = !IsWorldMapSuppressed;
        }
    }
}
