#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public sealed class WorldQuestProjectValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [MenuItem("Tools/Card Colony/World Quests/Validate All")]
        public static void ValidateAllFromMenu()
        {
            WorldQuestValidationReport report = ValidateAll();
            foreach (string warning in report.Warnings.Cast<string>())
                Debug.LogWarning("[WorldQuest] " + warning);
            foreach (string error in report.Errors.Cast<string>())
                Debug.LogError("[WorldQuest] " + error);
            if (report.IsValid)
            {
                Debug.Log($"[WorldQuest] 验证通过：Error=0，" +
                    $"Warning={report.Warnings.Count}，任务数=" +
                    Resources.LoadAll<WorldQuestDefinition>("WorldQuests")
                        .Length);
            }
            else
            {
                Debug.LogError($"[WorldQuest] 验证失败：" +
                    $"Error={report.Errors.Count}，" +
                    $"Warning={report.Warnings.Count}");
            }
        }

        [MenuItem("Tools/Card Colony/World Quests/Create Quest")]
        public static void CreateQuest()
        {
            var definition = ScriptableObject.CreateInstance<
                WorldQuestDefinition>();
            ConfigureTemplate(definition);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/StackCraft/Resources/WorldQuests/" +
                "WorldQuest_New.asset");
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }

        private static void ConfigureTemplate(
            WorldQuestDefinition definition)
        {
            var serialized = new SerializedObject(definition);
            string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            serialized.FindProperty("id").stringValue =
                "side_new_quest_" + suffix;
            serialized.FindProperty("category").enumValueIndex =
                (int)WorldQuestCategory.Side;
            serialized.FindProperty("title").stringValue = "新支线任务";
            serialized.FindProperty("description").stringValue =
                "请填写任务背景、目标与玩家动机。";
            serialized.FindProperty("giverNpcId").stringValue = "npc_id";
            serialized.FindProperty("entryStageId").stringValue = "start";

            SerializedProperty turnInNpcIds = serialized.FindProperty(
                "turnInNpcIds");
            turnInNpcIds.arraySize = 1;
            turnInNpcIds.GetArrayElementAtIndex(0).stringValue = "npc_id";

            SerializedProperty stages = serialized.FindProperty("stages");
            stages.arraySize = 1;
            SerializedProperty stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative("stageId").stringValue = "start";
            stage.FindPropertyRelative("title").stringValue = "开始";
            stage.FindPropertyRelative("objectiveSummary").stringValue =
                "与任务发布者交谈";
            stage.FindPropertyRelative("activeReminderText").stringValue =
                "返回任务发布者处继续任务。";
            SerializedProperty objectives = stage.FindPropertyRelative(
                "objectives");
            objectives.arraySize = 1;
            SerializedProperty objective =
                objectives.GetArrayElementAtIndex(0);
            objective.FindPropertyRelative("objectiveId").stringValue =
                "talk_to_giver";
            objective.FindPropertyRelative("type").enumValueIndex =
                (int)WorldQuestObjectiveType.TalkToNpc;
            objective.FindPropertyRelative("targetId").stringValue =
                "npc_id";
            objective.FindPropertyRelative("requiredAmount").intValue = 1;
            objective.FindPropertyRelative("showProgress").boolValue = true;
            objective.FindPropertyRelative("displayText").stringValue =
                "与任务发布者交谈";

            SerializedProperty outcomes = serialized.FindProperty(
                "outcomes");
            outcomes.arraySize = 1;
            SerializedProperty outcome = outcomes.GetArrayElementAtIndex(0);
            outcome.FindPropertyRelative("outcomeId").stringValue =
                "completed";
            outcome.FindPropertyRelative("isDefault").boolValue = true;
            outcome.FindPropertyRelative("choiceLabel").stringValue =
                "完成任务";
            outcome.FindPropertyRelative("resolutionText").stringValue =
                "任务已经完成。";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Card Colony/World Quests/Open Dependency Report")]
        public static void OpenDependencyReport()
        {
            foreach (WorldQuestDefinition definition in
                     Resources.LoadAll<WorldQuestDefinition>("WorldQuests")
                         .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                string targets = string.Join(
                    ", ",
                    EnumerateEffects(definition)
                        .Where(effect => effect.Type is
                            WorldQuestEffectType.MakeQuestAvailable or
                            WorldQuestEffectType.ActivateQuest or
                            WorldQuestEffectType.SuspendQuest or
                            WorldQuestEffectType.ResumeQuest or
                            WorldQuestEffectType.FailQuest or
                            WorldQuestEffectType.CancelQuest)
                        .Select(effect => effect.TargetId)
                        .Distinct());
                string conditions = string.Join(
                    ", ",
                    EnumerateConditionSets(definition)
                        .SelectMany(set => set.Conditions)
                        .Where(condition => condition.Type is
                            WorldQuestConditionType.QuestStatus or
                            WorldQuestConditionType.QuestOutcome)
                        .Select(condition => condition.TargetId)
                        .Distinct());
                string facts = string.Join(
                    ", ",
                    EnumerateEffects(definition)
                        .Where(effect => effect.Type is
                            WorldQuestEffectType.SetWorldFactBool or
                            WorldQuestEffectType.SetWorldFactInt or
                            WorldQuestEffectType.SetWorldFactString or
                            WorldQuestEffectType.IncrementWorldFactInt)
                        .Select(effect => effect.TargetId)
                        .Distinct());
                Debug.Log($"[WorldQuest] {definition.Id}" +
                    $"\n  前置任务：{EmptyLabel(conditions)}" +
                    $"\n  状态效果：{EmptyLabel(targets)}" +
                    $"\n  写入事实：{EmptyLabel(facts)}",
                    definition);
            }
        }

        private static System.Collections.Generic.IEnumerable<
            WorldQuestConditionSet> EnumerateConditionSets(
                WorldQuestDefinition definition)
        {
            return new[]
                {
                    definition.HardStartConditions,
                    definition.AvailabilityConditions,
                    definition.AcceptConditions
                }
                .Concat(definition.Stages.SelectMany(stage =>
                    stage.Transitions.Select(transition =>
                        transition.Conditions)))
                .Concat(definition.Outcomes.SelectMany(outcome => new[]
                {
                    outcome.SelectionConditions,
                    outcome.TurnInConditions
                }))
                .Where(value => value != null);
        }

        private static System.Collections.Generic.IEnumerable<
            WorldQuestEffectDefinition> EnumerateEffects(
                WorldQuestDefinition definition)
        {
            return definition.OnAcceptEffects
                .Concat(definition.Stages.SelectMany(stage =>
                    stage.OnEnterEffects.Concat(stage.OnCompleteEffects)))
                .Concat(definition.Outcomes.SelectMany(outcome =>
                    outcome.Effects))
                .Where(value => value != null);
        }

        private static string EmptyLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "（无）" : value;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            WorldQuestValidationReport validation = ValidateAll();
            if (!validation.IsValid)
            {
                throw new BuildFailedException(string.Join(
                    Environment.NewLine,
                    validation.Errors.Cast<string>()));
            }
        }

        internal static WorldQuestValidationReport ValidateAll()
        {
            return WorldQuestDefinitionValidator.Validate(
                (System.Collections.Generic.IEnumerable<
                    WorldQuestDefinition>)
                Resources.LoadAll<WorldQuestDefinition>("WorldQuests"));
        }
    }

    [InitializeOnLoad]
    internal static class WorldQuestPlayModeValidation
    {
        static WorldQuestPlayModeValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode)
                    return;
                WorldQuestValidationReport report =
                    WorldQuestProjectValidator.ValidateAll();
                if (report.IsValid)
                    return;
                EditorApplication.isPlaying = false;
                Debug.LogError(
                    "[WorldQuest] 任务定义验证失败，已阻止进入 PlayMode。\n" +
                    string.Join(Environment.NewLine,
                        report.Errors.Cast<string>()));
            };
        }
    }

    internal sealed class WorldQuestAssetSaveValidation :
        AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (paths.Any(path => path.Replace('\\', '/').StartsWith(
                    "Assets/StackCraft/Resources/WorldQuests/",
                    StringComparison.Ordinal)))
            {
                WorldQuestProjectValidator.ValidateAllFromMenu();
            }
            return paths;
        }
    }
}
#endif
