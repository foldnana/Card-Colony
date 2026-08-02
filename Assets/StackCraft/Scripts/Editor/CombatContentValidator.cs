using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class CombatContentValidator
    {
        [MenuItem("Tools/StackCraft/Validate Combat Content")]
        public static void ValidateFromMenu()
        {
            bool valid = Validate(logResults: true);
            if (valid)
                Debug.Log("Combat content validation passed.");
        }

        public static bool Validate(bool logResults)
        {
            var errors = new List<string>();
            CombatSkillDefinition[] skills = LoadAll<CombatSkillDefinition>();
            CombatItemDefinition[] items = LoadAll<CombatItemDefinition>();
            CardDefinition[] cards = LoadAll<CardDefinition>();

            ValidateUniqueIds(skills, value => value.Id, "技能", errors);
            ValidateUniqueIds(items, value => value.Id, "战斗物品", errors);

            foreach (CombatSkillDefinition skill in skills.Where(value => value != null))
            {
                if (skill.EnergyCost < 0)
                    errors.Add($"技能 {skill.name} 的精力消耗不能小于0。");
                if (skill.CooldownSeconds < 0f)
                    errors.Add($"技能 {skill.name} 的冷却不能小于0。");
                if (skill.PowerMultiplier <= 0f)
                    errors.Add($"技能 {skill.name} 的伤害倍率必须大于0。");
                if (!System.Enum.IsDefined(typeof(CombatTargetRule), skill.TargetRule))
                    errors.Add($"技能 {skill.name} 的目标规则无效。");
            }

            foreach (CardDefinition card in cards.Where(value => value != null))
            {
                if (card.InnateCombatSkills?.Any(value => value == null) == true ||
                    card.GrantedCombatSkills?.Any(value => value == null) == true)
                    errors.Add($"卡牌 {card.name} 引用了空技能元素。");
                if (card.CombatItemDefinition != null &&
                    card.Category != CardCategory.Consumable)
                    errors.Add($"战斗物品卡 {card.name} 必须属于 Consumable。");
                if (card.CombatItemDefinition != null &&
                    card.CombatItemDefinition.EffectType != CombatItemEffectType.Damage &&
                    card.CombatItemDefinition.EffectType != CombatItemEffectType.Heal)
                    errors.Add($"战斗物品卡 {card.name} 的效果类型无效。");
            }

            CardDefinition protagonist = cards.FirstOrDefault(
                value => value != null && value.name == "Card_Villager");
            if (protagonist?.InnateCombatSkills?.Any(
                    value => value != null &&
                        value.Id == "skill_protagonist_power_strike") != true)
                errors.Add("主角基础人物定义必须包含“奋力一击”。");

            CardDefinition medicine = cards.FirstOrDefault(
                value => value != null && value.name == "Card_Medicine");
            if (medicine?.CombatItemDefinition?.Id != "combat_item_medicine")
                errors.Add("Card_Medicine 必须引用 combat_item_medicine。");

            if (logResults)
            {
                foreach (string error in errors)
                    Debug.LogError("[Combat Content] " + error);
            }
            return errors.Count == 0;
        }

        private static T[] LoadAll<T>() where T : Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .ToArray();
        }

        private static void ValidateUniqueIds<T>(
            IEnumerable<T> values,
            System.Func<T, string> getId,
            string label,
            ICollection<string> errors)
            where T : Object
        {
            foreach (T value in values)
            {
                if (string.IsNullOrWhiteSpace(getId(value)))
                    errors.Add($"{label} {value.name} 的 ID 不能为空。");
            }
            foreach (IGrouping<string, T> duplicate in values
                         .Where(value => !string.IsNullOrWhiteSpace(getId(value)))
                         .GroupBy(getId)
                         .Where(group => group.Count() > 1))
                errors.Add($"{label} ID {duplicate.Key} 不唯一。");
        }
    }

    public sealed class CombatContentBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!CombatContentValidator.Validate(logResults: true))
                throw new BuildFailedException(
                    "Combat content validation failed. Build cancelled.");
        }
    }
}
