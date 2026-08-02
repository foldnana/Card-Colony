using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class CombatLogPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text logText;
        [SerializeField] private CombatLogView view;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private HudMessageAreaCoordinator coordinator;

        private readonly Dictionary<string, CombatEventBuffer> buffers = new();
        private CombatManager subscribedManager;
        private string visibleSessionId;
        private string pendingClearSessionId;
        private float hideAt = -1f;

        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;

        private void Awake()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            view ??= GetComponent<CombatLogView>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            CombatFocusService.FocusChanged += HandleFocusChanged;
            TrySubscribe();
        }

        private void OnDisable()
        {
            CombatFocusService.FocusChanged -= HandleFocusChanged;
            Unsubscribe();
        }

        private void Update()
        {
            TrySubscribe();
            if (hideAt >= 0f && Time.unscaledTime >= hideAt)
            {
                hideAt = -1f;
                SetVisible(false);
                if (!string.IsNullOrWhiteSpace(pendingClearSessionId))
                    buffers.Remove(pendingClearSessionId);
                pendingClearSessionId = null;
                visibleSessionId = null;
            }
        }

        private void TrySubscribe()
        {
            CombatManager manager = CombatManager.Instance;
            if (manager == null || manager == subscribedManager)
                return;
            Unsubscribe();
            subscribedManager = manager;
            subscribedManager.EventPublished += HandleEvent;
        }

        private void Unsubscribe()
        {
            if (subscribedManager != null)
                subscribedManager.EventPublished -= HandleEvent;
            subscribedManager = null;
        }

        private void HandleEvent(CombatEvent combatEvent)
        {
            if (combatEvent == null || string.IsNullOrWhiteSpace(combatEvent.SessionId))
                return;
            if (!buffers.TryGetValue(combatEvent.SessionId, out CombatEventBuffer buffer))
            {
                buffer = new CombatEventBuffer();
                buffers.Add(combatEvent.SessionId, buffer);
            }
            buffer.Add(combatEvent);
            string focusedSession = CombatFocusService.FocusedCombat?.SessionId;
            if (string.IsNullOrWhiteSpace(focusedSession))
                focusedSession = visibleSessionId ?? combatEvent.SessionId;
            if (combatEvent.SessionId != focusedSession)
            {
                if (combatEvent.Type == CombatEventType.CombatEnded)
                    buffers.Remove(combatEvent.SessionId);
                return;
            }
            visibleSessionId = focusedSession;
            Refresh(buffer);
            SetVisible(true);
            hideAt = combatEvent.Type == CombatEventType.CombatEnded
                ? Time.unscaledTime + 4f
                : -1f;
            if (combatEvent.Type == CombatEventType.CombatEnded)
                pendingClearSessionId = combatEvent.SessionId;
        }

        private void HandleFocusChanged()
        {
            string sessionId = CombatFocusService.FocusedCombat?.SessionId;
            if (string.IsNullOrWhiteSpace(sessionId) ||
                !buffers.TryGetValue(sessionId, out CombatEventBuffer buffer))
                return;
            visibleSessionId = sessionId;
            hideAt = -1f;
            pendingClearSessionId = null;
            Refresh(buffer);
            SetVisible(true);
        }

        private void Refresh(CombatEventBuffer buffer)
        {
            if (view != null)
            {
                view.Render(buffer.Events);
                return;
            }
            if (logText == null)
                return;
            var readable = buffer.Events
                .Select(value => new
                {
                    Event = value,
                    Text = CombatEventFormatter.Format(value)
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                .ToList();
            logText.text = string.Join("\n", readable
                .Skip(Mathf.Max(0, readable.Count - 6))
                .Select(row => $"<color={ColorFor(row.Event.Type)}>" +
                    row.Text + "</color>"));
        }

        private static string ColorFor(CombatEventType type)
        {
            return type switch
            {
                CombatEventType.CriticalHit => "#FFD166",
                CombatEventType.DamageApplied => "#FF8A80",
                CombatEventType.HealingApplied => "#83E28E",
                CombatEventType.RetreatFailed => "#FFB74D",
                CombatEventType.RetreatSucceeded => "#70D6FF",
                CombatEventType.ExperienceGranted => "#D6B8FF",
                _ => "#F1E5CF"
            };
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            coordinator?.SetCombatLogActive(visible);
        }
    }

}
