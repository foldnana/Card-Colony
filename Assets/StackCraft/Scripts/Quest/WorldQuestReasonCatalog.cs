using System;
using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class WorldQuestReasonDefinition
    {
        [SerializeField] private string reasonId;
        [SerializeField] private string displayText;

        public string ReasonId => reasonId;
        public string DisplayText => displayText;
    }

    [CreateAssetMenu(
        menuName = "StackCraft/World Quest Reason Catalog",
        fileName = "WorldQuestReasons")]
    public sealed class WorldQuestReasonCatalog : ScriptableObject
    {
        [SerializeField] private List<WorldQuestReasonDefinition> reasons =
            new();

        public IReadOnlyList<WorldQuestReasonDefinition> Reasons => reasons;

        public bool TryGet(string reasonId, out string displayText)
        {
            foreach (WorldQuestReasonDefinition reason in reasons)
            {
                if (reason != null && reason.ReasonId == reasonId)
                {
                    displayText = reason.DisplayText;
                    return true;
                }
            }
            displayText = string.Empty;
            return false;
        }
    }
}
