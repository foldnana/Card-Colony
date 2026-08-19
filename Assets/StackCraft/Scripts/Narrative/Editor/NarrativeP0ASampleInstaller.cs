#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft.NarrativeEditor
{
    public static class NarrativeP0ASampleInstaller
    {
        private const string Folder =
            "Assets/StackCraft/Resources/Narratives";
        private const string AssetPath = Folder +
            "/Narrative_Riverbend_FirstCommission.asset";
        private const string QuestId =
            "story_riverbend_whispering_forest_01";

        [MenuItem("Tools/Card Colony/Narrative/Install P0-A Sample")]
        public static void Install()
        {
            Directory.CreateDirectory(Folder);
            NarrativeDefinition definition = AssetDatabase
                .LoadAssetAtPath<NarrativeDefinition>(AssetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<
                    NarrativeDefinition>();
                AssetDatabase.CreateAsset(definition, AssetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue =
                $"quest_offer.{QuestId}";
            serialized.FindProperty("version").intValue = 1;
            serialized.FindProperty("displayName").stringValue =
                "村长发布首个委托";
            serialized.FindProperty("canSkip").boolValue = false;
            serialized.FindProperty("allowCameraInput").boolValue = true;
            serialized.FindProperty("entryNodeId").stringValue = "offer";

            SerializedProperty actors = serialized.FindProperty(
                "actorBindings");
            actors.arraySize = 2;
            SetActor(actors.GetArrayElementAtIndex(0), "Player",
                NarrativeActorResolveMode.PartyLeader, string.Empty,
                "旅行者");
            SetActor(actors.GetArrayElementAtIndex(1), "Mayor",
                NarrativeActorResolveMode.CardDefinitionId,
                "riverbend-village-chief", "村长");

            SerializedProperty nodes = serialized.FindProperty("nodes");
            nodes.arraySize = 3;
            ConfigureOffer(nodes.GetArrayElementAtIndex(0));
            ConfigureAccepted(nodes.GetArrayElementAtIndex(1));
            ConfigureDeclined(nodes.GetArrayElementAtIndex(2));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            NarrativeValidationReport report =
                NarrativeValidator.Validate(definition);
            if (!report.IsValid)
                throw new System.InvalidOperationException(
                    string.Join(" | ", report.Errors));
            Debug.Log($"[Narrative] P0-A 样例已生成：{AssetPath}",
                definition);
        }

        private static void ConfigureOffer(SerializedProperty node)
        {
            SetNodeId(node, "offer");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 2;
            SetDialogue(commands.GetArrayElementAtIndex(0), "greeting",
                NarrativeCommandType.ShowDialogue, "Mayor",
                "story.riverbend.first_commission.greeting",
                "年轻人，低语森林最近很不安宁。你愿意替村子调查吗？");

            SerializedProperty choice = commands.GetArrayElementAtIndex(1);
            SetCommand(choice, "decision", NarrativeCommandType.ShowChoice);
            SerializedProperty dialogue = choice.FindPropertyRelative(
                "dialogueParameters");
            dialogue.FindPropertyRelative("choiceTitleFallback").stringValue =
                "是否接受委托？";
            SerializedProperty choices = dialogue.FindPropertyRelative(
                "choices");
            choices.arraySize = 2;
            SetChoice(choices.GetArrayElementAtIndex(0), "accept",
                "story.riverbend.first_commission.accept", "接受委托",
                "accepted");
            SetChoice(choices.GetArrayElementAtIndex(1), "decline",
                "story.riverbend.first_commission.decline", "暂时拒绝",
                "declined");
        }

        private static void ConfigureAccepted(SerializedProperty node)
        {
            SetNodeId(node, "accepted");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 3;
            SetDialogue(commands.GetArrayElementAtIndex(0), "instructions",
                NarrativeCommandType.ShowDialogue, "Mayor",
                "story.riverbend.first_commission.instructions",
                "先准备一些补给，再去低语森林查明异响的来源。");

            SerializedProperty startQuest = commands.GetArrayElementAtIndex(1);
            SetCommand(startQuest, "start_quest",
                NarrativeCommandType.StartQuest);
            SerializedProperty effect = startQuest.FindPropertyRelative(
                "effectParameters");
            effect.FindPropertyRelative("effectType").enumValueIndex =
                (int)NarrativeEffectType.StartQuest;
            effect.FindPropertyRelative("resultId").stringValue =
                "accept_first_commission";
            effect.FindPropertyRelative("targetId").stringValue = QuestId;
            SetCommand(commands.GetArrayElementAtIndex(2), "end",
                NarrativeCommandType.EndNarrative);
        }

        private static void ConfigureDeclined(SerializedProperty node)
        {
            SetNodeId(node, "declined");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 2;
            SetDialogue(commands.GetArrayElementAtIndex(0), "declined_line",
                NarrativeCommandType.ShowDialogue, "Mayor",
                "story.riverbend.first_commission.declined_line",
                "没关系。准备好了再来找我。");
            SetCommand(commands.GetArrayElementAtIndex(1), "end",
                NarrativeCommandType.EndNarrative);
        }

        private static void SetActor(
            SerializedProperty actor,
            string roleId,
            NarrativeActorResolveMode mode,
            string cardDefinitionId,
            string displayName)
        {
            actor.FindPropertyRelative("roleId").stringValue = roleId;
            actor.FindPropertyRelative("resolveMode").enumValueIndex =
                (int)mode;
            actor.FindPropertyRelative("cardDefinitionId").stringValue =
                cardDefinitionId;
            actor.FindPropertyRelative("displayName").stringValue =
                displayName;
        }

        private static void SetNodeId(SerializedProperty node, string id)
        {
            node.FindPropertyRelative("id").stringValue = id;
            node.FindPropertyRelative("nextNodeId").stringValue = string.Empty;
            node.FindPropertyRelative("failureNodeId").stringValue =
                string.Empty;
        }

        private static void SetCommand(
            SerializedProperty command,
            string commandId,
            NarrativeCommandType type)
        {
            command.FindPropertyRelative("commandId").stringValue = commandId;
            command.FindPropertyRelative("type").enumValueIndex = (int)type;
            command.FindPropertyRelative("failurePolicy").enumValueIndex =
                (int)NarrativeFailurePolicy.FailNarrative;
            command.FindPropertyRelative("failureNodeId").stringValue =
                string.Empty;
        }

        private static void SetDialogue(
            SerializedProperty command,
            string commandId,
            NarrativeCommandType type,
            string actorRole,
            string textKey,
            string fallbackText)
        {
            SetCommand(command, commandId, type);
            SerializedProperty dialogue = command.FindPropertyRelative(
                "dialogueParameters");
            dialogue.FindPropertyRelative("actorRole").stringValue = actorRole;
            dialogue.FindPropertyRelative("textKey").stringValue = textKey;
            dialogue.FindPropertyRelative("fallbackText").stringValue =
                fallbackText;
        }

        private static void SetChoice(
            SerializedProperty choice,
            string id,
            string textKey,
            string fallbackText,
            string targetNodeId)
        {
            choice.FindPropertyRelative("choiceId").stringValue = id;
            choice.FindPropertyRelative("textKey").stringValue = textKey;
            choice.FindPropertyRelative("fallbackText").stringValue =
                fallbackText;
            choice.FindPropertyRelative("targetNodeId").stringValue =
                targetNodeId;
            choice.FindPropertyRelative("important").boolValue = true;
        }
    }
}
#endif
