using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class Stat
    {
        public float BaseValue { get; private set; }

        private readonly List<IStatModifier> _modifiers = new();

        public int Value
        {
            get
            {
                float finalValue = BaseValue;
                _modifiers.ForEach(mod => finalValue += mod.Value);
                return Mathf.RoundToInt(finalValue);
            }
        }

        public Stat(float baseValue)
        {
            BaseValue = baseValue;
        }

        public void AddModifier(IStatModifier modifier)
        {
            _modifiers.Add(modifier);
        }

        public void RemoveModifier(IStatModifier modifier)
        {
            _modifiers.Remove(modifier);
        }
    }
}
