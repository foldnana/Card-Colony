using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public static class CombatSkillService
    {
        public static IReadOnlyList<CombatSkillDefinition> Resolve(CardInstance actor)
        {
            if (actor == null)
                return System.Array.Empty<CombatSkillDefinition>();

            var ordered = new List<CombatSkillDefinition>();
            Add(ordered, actor.BaseDefinition?.InnateCombatSkills);

            IEnumerable<CardInstance> equipment = actor.EquipperComponent?.EquippedCards ??
                Enumerable.Empty<CardInstance>();
            foreach (CardInstance card in equipment
                         .Where(card => card?.Definition?.EquipmentSlot == EquipmentSlot.Weapon))
                Add(ordered, card.Definition.GrantedCombatSkills);
            foreach (CardInstance card in equipment
                         .Where(card => card?.Definition?.EquipmentSlot != EquipmentSlot.Weapon))
                Add(ordered, card.Definition.GrantedCombatSkills);

            return ordered
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                .GroupBy(skill => skill.Id)
                .Select(group => group.First())
                .ToList();
        }

        private static void Add(
            ICollection<CombatSkillDefinition> destination,
            IEnumerable<CombatSkillDefinition> source)
        {
            if (source == null)
                return;
            foreach (CombatSkillDefinition skill in source)
                destination.Add(skill);
        }
    }
}
