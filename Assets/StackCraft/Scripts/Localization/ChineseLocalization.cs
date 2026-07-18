using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Simplified Chinese localization used by the included StackCraft demo content.
    /// The original English ScriptableObject data is intentionally preserved so asset
    /// updates can still be merged without rewriting every content asset.
    /// </summary>
    public static partial class ChineseLocalization
    {
        private const string ProjectFontResourcePath = "Fonts/SIMYOU SDF";
        private static TMP_FontAsset projectFont;

        private static readonly Dictionary<string, string> UiTexts = new()
        {
            ["Quit Game"] = "退出游戏",
            ["Friendly Mode: OFF"] = "友好模式：关闭",
            ["Friendly Mode: OFF\n<size=23>(Enemies may appear)"] = "友好模式：关闭\n<size=23>（可能出现敌人）",
            ["Friendly Mode: OFF\n\n<size=23>(Enemies may appear)"] = "友好模式：关闭\n<size=23>（可能出现敌人）",
            ["Day Duration: 120 sec"] = "每日时长：120 秒",
            ["[Clear All Saves]"] = "[清空所有存档]",
            ["Load Saved Games"] = "读取存档",
            ["[Cancel]"] = "[取消]",
            ["Gameplay Preferences"] = "玩法偏好",
            ["Game Options"] = "游戏设置",
            ["[Confirm]"] = "[确认]",
            ["[Close Window]"] = "[关闭窗口]",
            ["Load Game"] = "读取游戏",
            ["New Game"] = "新游戏",
            ["Pack Name"] = "卡包名称",
            ["Card Name"] = "卡牌名称",
            ["Sell"] = "出售",
            ["BGM 50%"] = "音乐 50%",
            ["Shadows High"] = "阴影 高",
            ["[Reset]"] = "[重置]",
            ["Fullscreen (Enabled)"] = "全屏（开启）",
            ["FPS 60"] = "帧率 60",
            ["vSync On"] = "垂直同步 开启",
            ["Resolution 1920x1080"] = "分辨率 1920x1080",
            ["[Close]"] = "[关闭]",
            ["SFX 50%"] = "音效 50%",
            ["[No]"] = "[否]",
            ["Are you sure you want to proceed?"] = "确定要继续吗？",
            ["Are you sure you want to proceed?\nThis change cannot be undone."] = "确定要继续吗？\n此更改无法撤销。",
            ["Are you sure you want to proceed?\n\nThis change cannot be undone."] = "确定要继续吗？\n此更改无法撤销。",
            ["Action Name"] = "操作名称",
            ["[Yes]"] = "[是]",
            ["[Delete]"] = "[删除]",
            ["[Load]"] = "[读取]",
            ["Action Button"] = "操作按钮",
            ["GAME PAUSED"] = "游戏已暂停",
            ["Day N"] = "第 N 天",
            ["Continue to Play"] = "继续游戏",
            ["Quests"] = "任务",
            ["Recipes"] = "配方",
            ["<size=34>[Info Text]<size=30>"] = "<size=34>[信息]<size=30>",
            ["<size=34>[Info Text]<size=30>\nInformation will be displayed here.\nThe panel automatically adjusts its size based on the text's length."] = "<size=34>[信息]<size=30>\n信息会显示在这里。\n面板将根据文本长度自动调整大小。",
            ["<size=34>[Info Text]<size=30>\n\nInformation will be displayed here.\nThe panel automatically adjusts its size based on the text's length."] = "<size=34>[信息]<size=30>\n信息会显示在这里。\n面板将根据文本长度自动调整大小。",
            ["Quit to Title & Save"] = "保存并返回标题界面",
            ["StackCraft Gameplay Demo by Crying Snow"] = "StackCraft 玩法演示 - Crying Snow",
            ["Main"] = "主世界",
            ["Island"] = "海岛",
            ["Introduction"] = "入门",
            ["Ascension"] = "飞升之路",
            ["Training"] = "战斗训练",
            ["Advancement"] = "进阶",
            ["Cooking"] = "烹饪",
            ["Exploration"] = "探索",
            ["Hoarder"] = "囤积者",
            ["Construction"] = "建设",
            ["Survive"] = "生存",
            ["The Basics"] = "基础知识"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureProjectFont();
            LocalizeSerializedTextComponents();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LocalizeSerializedTextComponents();
        }

        private static void EnsureProjectFont()
        {
            if (projectFont != null)
                return;

            projectFont = Resources.Load<TMP_FontAsset>(ProjectFontResourcePath);
            if (projectFont == null)
            {
                Debug.LogWarning($"StackCraft 中文化：无法从 Resources/{ProjectFontResourcePath} 加载项目字体。");
            }
        }

        private static void LocalizeSerializedTextComponents()
        {
            foreach (TMP_Text label in Object.FindObjectsOfType<TMP_Text>(true))
            {
                if (label == null)
                    continue;

                if (projectFont != null && label.font != projectFont)
                    label.font = projectFont;

                if (string.IsNullOrEmpty(label.text))
                    continue;

                string localized = Text(label.text);
                if (!ReferenceEquals(localized, label.text) && localized != label.text)
                    label.text = localized;
            }
        }

        public static string Text(string english)
        {
            if (string.IsNullOrEmpty(english))
                return english;

            return UiTexts.TryGetValue(english, out string localized) ? localized : english;
        }

        public static string CardName(string english)
        {
            if (string.IsNullOrEmpty(english))
                return english;

            return CardNames.TryGetValue(english, out string localized) ? localized : english;
        }

        public static string CardDescription(string englishName, string englishDescription)
        {
            if (string.IsNullOrEmpty(englishName))
                return englishDescription;

            return CardDescriptions.TryGetValue(englishName, out string localized)
                ? localized
                : englishDescription;
        }

        public static string RecipeName(string english)
        {
            if (string.IsNullOrEmpty(english))
                return english;

            return RecipeNames.TryGetValue(english, out string localized) ? localized : english;
        }

        public static string QuestTitle(string english)
        {
            if (string.IsNullOrEmpty(english))
                return english;

            return QuestTexts.TryGetValue(english, out LocalizedQuest localized)
                ? localized.Title
                : english;
        }

        public static string QuestDescription(string englishTitle, string englishDescription)
        {
            if (string.IsNullOrEmpty(englishTitle))
                return englishDescription;

            return QuestTexts.TryGetValue(englishTitle, out LocalizedQuest localized)
                ? localized.Description
                : englishDescription;
        }

        public static string EncounterMessage(string english)
        {
            if (string.IsNullOrEmpty(english))
                return english;

            return EncounterMessages.TryGetValue(english, out string localized) ? localized : english;
        }

        public static string RecipeCategoryName(RecipeCategory category)
        {
            return category switch
            {
                RecipeCategory.Misc => "杂项",
                RecipeCategory.Gathering => "采集",
                RecipeCategory.Construction => "建设",
                RecipeCategory.Cooking => "烹饪",
                RecipeCategory.Forging => "锻造",
                RecipeCategory.Refining => "加工",
                RecipeCategory.Husbandry => "培育",
                _ => category.ToString()
            };
        }

        public static string CombatTypeName(CombatType type)
        {
            return type switch
            {
                CombatType.Melee => "近战",
                CombatType.Ranged => "远程",
                CombatType.Magic => "魔法",
                _ => "无"
            };
        }

        public static string ShadowPresetName(string preset)
        {
            return preset switch
            {
                "Off" => "关闭",
                "Low" => "低",
                "Medium" => "中",
                "High" => "高",
                "Ultra" => "极高",
                _ => preset
            };
        }

        private readonly struct LocalizedQuest
        {
            public readonly string Title;
            public readonly string Description;

            public LocalizedQuest(string title, string description)
            {
                Title = title;
                Description = description;
            }
        }
    }
}
