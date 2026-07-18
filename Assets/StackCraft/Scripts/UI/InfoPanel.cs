using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

namespace CryingSnow.StackCraft
{
    public enum InfoPriority
    {
        Hover,      // Lowest priority, for mouse hover info
        Sequence,   // For non-critical sequences like vendor unlocks
        Modal       // For critical, game-pausing events like end-of-day
    }

    public class InfoPanel : MonoBehaviour
    {
        public static InfoPanel Instance { get; private set; }

        [SerializeField, Tooltip("The TextMeshProUGUI component where all combined header and body information is displayed.")]
        private TextMeshProUGUI infoText;

        [SerializeField, Tooltip("The font size used for the Header portion of the info text (e.g., the title of a zone or item).")]
        private int headerSize = 34;

        [SerializeField, Tooltip("The font size used for the Body portion of the info text (e.g., the description or details).")]
        private int bodySize = 30;

        [SerializeField, Tooltip("The TextButton component that is displayed when the highest priority info request includes a mandatory action.")]
        private TextButton actionButton;

        private static int s_requestCounter = 0;

        private class InfoRequest
        {
            public int RequestID;
            public InfoPriority Priority;
            public (string header, string body) Info;
            public string ButtonLabel;
            public System.Action ButtonAction;
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

            RefreshInfo();
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

            activeRequests[requester] = new InfoRequest
            {
                RequestID = s_requestCounter++,
                Priority = priority,
                Info = info,
                ButtonLabel = buttonLabel,
                ButtonAction = buttonAction
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

                if (!string.IsNullOrEmpty(highestPriorityRequest.ButtonLabel) && highestPriorityRequest.ButtonAction != null)
                {
                    SetActionButton(highestPriorityRequest.ButtonLabel, highestPriorityRequest.ButtonAction);
                }
                else
                {
                    actionButton.Deactivate();
                }
            }
            else
            {
                ClearInfo();
                actionButton.Deactivate();
            }
        }

        private void UpdateInfo((string header, string body) newInfo)
        {
            if (newInfo == lastDisplayedInfo) return;

            string headerText = "", bodyText = "";

            if (!string.IsNullOrEmpty(newInfo.header))
            {
                headerText = $"<size={headerSize}>[{newInfo.header}]\n";
            }
            if (!string.IsNullOrEmpty(newInfo.body))
            {
                bodyText = $"<size={bodySize}>{newInfo.body}";
            }

            infoText.text = headerText + bodyText;
            lastDisplayedInfo = newInfo;
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

        private void SetActionButton(string label, System.Action action)
        {
            actionButton.Setup(
                label,
                bodySize,
                onClick: () => action?.Invoke()
            );
        }
    }
}
