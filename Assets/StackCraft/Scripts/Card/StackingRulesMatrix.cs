using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum StackingRule
    {
        None,           // No stacking allowed
        CategoryWide,   // Allowed across all definitions in category
        SameDefinition  // Allowed only if bottom.Definition == top.Definition
    }

    [CreateAssetMenu(menuName = "StackCraft/Stacking Rules Matrix")]
    public class StackingRulesMatrix : ScriptableObject
    {
        [SerializeField] private StackingRule[] rules;

        private void OnValidate()
        {
            Initialize();
        }

        public StackingRule GetRule(CardCategory bottom, CardCategory top)
        {
            EnsureInitialized();
            int size = System.Enum.GetValues(typeof(CardCategory)).Length;
            return rules[(int)bottom * size + (int)top];
        }

        public void SetRule(CardCategory bottom, CardCategory top, StackingRule value)
        {
            EnsureInitialized();
            int size = System.Enum.GetValues(typeof(CardCategory)).Length;
            rules[(int)bottom * size + (int)top] = value;
        }

        private void EnsureInitialized()
        {
            if (rules == null || rules.Length == 0)
                Initialize();
        }

        public void Initialize()
        {
            int newSize = System.Enum.GetValues(typeof(CardCategory)).Length;
            int newArrayLength = newSize * newSize;

            if (rules == null || rules.Length != newArrayLength)
            {
                StackingRule[] newRules = new StackingRule[newArrayLength];

                if (rules != null)
                {
                    int oldSize = (int)Mathf.Sqrt(rules.Length);

                    for (int y = 0; y < Mathf.Min(oldSize, newSize); y++)
                    {
                        for (int x = 0; x < Mathf.Min(oldSize, newSize); x++)
                        {
                            newRules[y * newSize + x] = rules[y * oldSize + x];
                        }
                    }
                }

                rules = newRules;
            }
        }
    }
}
