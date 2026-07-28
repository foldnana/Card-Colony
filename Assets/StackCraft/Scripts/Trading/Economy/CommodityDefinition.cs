using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum CommodityCategory
    {
        Food,
        Material,
        Manufactured,
        Medicine,
        Other
    }

    [CreateAssetMenu(
        menuName = "StackCraft/Trading/Commodity",
        fileName = "Commodity_")]
    public sealed class CommodityDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CardDefinition cardDefinition;
        [SerializeField, Min(1)] private int basePrice = 1;
        [SerializeField, Min(0f)] private float unitWeight;
        [SerializeField, Min(1)] private int stackLimit = 20;
        [SerializeField] private CommodityCategory category;
        [SerializeField, Range(0f, 1f)] private float volatility = 0.5f;
        [SerializeField, Min(0)] private int perishableHours;
        [SerializeField] private List<string> tradeTags = new();
        [SerializeField] private bool isTradeCommodity = true;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? cardDefinition?.DisplayName ?? id
            : displayName;
        public CardDefinition CardDefinition => cardDefinition;
        public int BasePrice => Mathf.Max(1, basePrice);
        public float UnitWeight => Mathf.Max(0f, unitWeight);
        public int StackLimit => Mathf.Max(1, stackLimit);
        public CommodityCategory Category => category;
        public float Volatility => Mathf.Clamp01(volatility);
        public int PerishableHours => Mathf.Max(0, perishableHours);
        public IReadOnlyList<string> TradeTags => tradeTags;
        public bool IsTradeCommodity => isTradeCommodity;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tradeTags == null)
                return false;

            return tradeTags.Exists(item =>
                string.Equals(
                    item,
                    tag,
                    System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
