using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class CombatLogView : MonoBehaviour
    {
        [SerializeField] private TMP_Text logText;

        public void SetTextTarget(TMP_Text value) => logText = value;

        public void Render(IEnumerable<CombatEvent> events)
        {
            if (logText == null)
                return;
            var readable = (events ?? Enumerable.Empty<CombatEvent>())
                .Select(combatEvent => new
                {
                    Event = combatEvent,
                    Text = CombatEventFormatter.Format(combatEvent)
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                .ToList();
            logText.text = string.Join("\n", readable
                .Skip(System.Math.Max(0, readable.Count - 6))
                .Select(row => $"<color={ColorFor(row.Event.Type)}>" +
                    row.Text + "</color>"));
        }

        private static string ColorFor(CombatEventType type)
        {
            return type switch
            {
                CombatEventType.DamageApplied => "#FF8A80",
                CombatEventType.HealingApplied => "#83E28E",
                CombatEventType.RetreatFailed => "#FFB74D",
                CombatEventType.RetreatSucceeded => "#70D6FF",
                CombatEventType.ExperienceGranted => "#D6B8FF",
                _ => "#F1E5CF"
            };
        }
    }
}
