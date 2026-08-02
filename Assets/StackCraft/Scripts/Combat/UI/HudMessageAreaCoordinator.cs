using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class HudMessageAreaCoordinator : MonoBehaviour
    {
        [SerializeField] private InfoPanel infoPanel;
        [SerializeField] private CanvasGroup combatLog;
        private bool combatLogActive;

        private void OnEnable()
        {
            if (infoPanel != null)
                infoPanel.HighestActivePriorityChanged += HandlePriorityChanged;
            Apply();
        }

        private void OnDisable()
        {
            if (infoPanel != null)
                infoPanel.HighestActivePriorityChanged -= HandlePriorityChanged;
        }

        public void SetCombatLogActive(bool active)
        {
            combatLogActive = active;
            Apply();
        }

        private void HandlePriorityChanged(InfoPriority? _) => Apply();

        private void Apply()
        {
            bool modal = infoPanel?.HighestActivePriority == InfoPriority.Modal;
            if (combatLog != null)
                combatLog.alpha = combatLogActive && !modal ? 1f : 0f;
            CanvasGroup infoGroup = infoPanel != null
                ? infoPanel.GetComponent<CanvasGroup>()
                : null;
            if (infoGroup != null)
            {
                bool showInfo = !combatLogActive || modal;
                showInfo &= infoPanel.HighestActivePriority.HasValue &&
                    !infoPanel.IsWorldMapSuppressed;
                infoGroup.alpha = showInfo ? 1f : 0f;
                infoGroup.blocksRaycasts = showInfo;
            }
        }
    }
}
