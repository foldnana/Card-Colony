#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft.NarrativeEditor
{
    public static class NarrativeP0BSampleInstaller
    {
        private const string Folder =
            "Assets/StackCraft/Resources/Narratives";
        private const string AssetPath = Folder +
            "/Narrative_Riverbend_MerchantThreat.asset";

        [MenuItem("Tools/Card Colony/Narrative/Install P0-B Sample")]
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
                "npc_event.riverbend-grocer";
            serialized.FindProperty("version").intValue = 1;
            serialized.FindProperty("displayName").stringValue =
                "河湾村杂货商受威吓";
            serialized.FindProperty("canSkip").boolValue = true;
            serialized.FindProperty("allowCameraInput").boolValue = false;
            serialized.FindProperty("entryNodeId").stringValue = "begin";

            SerializedProperty actors = serialized.FindProperty(
                "actorBindings");
            actors.arraySize = 3;
            SetActor(actors.GetArrayElementAtIndex(0), "Player",
                NarrativeActorResolveMode.PartyLeader, string.Empty,
                "旅行者");
            SetActor(actors.GetArrayElementAtIndex(1), "Merchant",
                NarrativeActorResolveMode.CardDefinitionId,
                "riverbend-grocer", "杂货商");
            SetActor(actors.GetArrayElementAtIndex(2), "Threat",
                NarrativeActorResolveMode.SpawnTemporary,
                "f6cac76a302245f3912997c679ee3b17", "哥布林");

            SerializedProperty nodes = serialized.FindProperty("nodes");
            nodes.arraySize = 4;
            ConfigureBegin(nodes.GetArrayElementAtIndex(0));
            ConfigureConfront(nodes.GetArrayElementAtIndex(1));
            ConfigureObserve(nodes.GetArrayElementAtIndex(2));
            ConfigureCleanup(nodes.GetArrayElementAtIndex(3));

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            NarrativeValidationReport report =
                NarrativeValidator.Validate(definition);
            if (!report.IsValid)
            {
                throw new System.InvalidOperationException(
                    string.Join(" | ", report.Errors));
            }
            Debug.Log($"[Narrative] P0-B 地点样例已生成：{AssetPath}",
                definition);
        }

        private static void ConfigureBegin(SerializedProperty node)
        {
            SetNodeId(node, "begin");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 12;
            SetActorCommand(commands.GetArrayElementAtIndex(0),
                "control_merchant", NarrativeCommandType.AcquireActorControl,
                "Merchant");
            SetActorCommand(commands.GetArrayElementAtIndex(1),
                "spawn_threat", NarrativeCommandType.SpawnActor,
                "Threat", "Merchant", new Vector3(1.8f, 0f, 0f), 0f);
            SetActorCommand(commands.GetArrayElementAtIndex(2),
                "control_threat", NarrativeCommandType.AcquireActorControl,
                "Threat");
            SetActorCommand(commands.GetArrayElementAtIndex(3),
                "focus_merchant", NarrativeCommandType.FocusActor,
                "Merchant", duration: 0.35f);
            SetActorCommand(commands.GetArrayElementAtIndex(4),
                "approach_merchant", NarrativeCommandType.MoveToActor,
                "Threat", "Merchant", new Vector3(1.1f, 0f, 0f), 0.7f);
            SetActorCommand(commands.GetArrayElementAtIndex(5),
                "face_threat", NarrativeCommandType.FaceActor,
                "Merchant", "Threat", duration: 0.25f);
            SetActorCommand(commands.GetArrayElementAtIndex(6),
                "merchant_surprised", NarrativeCommandType.ShowEmote,
                "Merchant", message: "！", duration: 0.55f);
            SetDialogue(commands.GetArrayElementAtIndex(7),
                "merchant_question", "Merchant", "你们想干什么？");
            SetDialogue(commands.GetArrayElementAtIndex(8),
                "threat_demand", "Threat", "把值钱的货物都交出来！");
            SetActorCommand(commands.GetArrayElementAtIndex(9),
                "threatening_swing", NarrativeCommandType.PlayCinematicAttack,
                "Threat", "Merchant", duration: 0.7f);
            SetActorCommand(commands.GetArrayElementAtIndex(10),
                "merchant_shock", NarrativeCommandType.ShowEmote,
                "Merchant", message: "惊", duration: 0.65f);

            SerializedProperty choice = commands.GetArrayElementAtIndex(11);
            SetCommand(choice, "player_decision",
                NarrativeCommandType.ShowChoice);
            SerializedProperty dialogue = choice.FindPropertyRelative(
                "dialogueParameters");
            dialogue.FindPropertyRelative("choiceTitleFallback").stringValue =
                "眼前的冲突尚未演变成正式战斗。";
            SerializedProperty choices = dialogue.FindPropertyRelative(
                "choices");
            choices.arraySize = 2;
            SetChoice(choices.GetArrayElementAtIndex(0), "confront",
                "上前制止", "confront");
            SetChoice(choices.GetArrayElementAtIndex(1), "observe",
                "暂时观察", "observe");
        }

        private static void ConfigureConfront(SerializedProperty node)
        {
            SetNodeId(node, "confront");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 7;
            SetActorCommand(commands.GetArrayElementAtIndex(0),
                "control_player", NarrativeCommandType.AcquireActorControl,
                "Player");
            SetActorCommand(commands.GetArrayElementAtIndex(1),
                "player_steps_in", NarrativeCommandType.MoveToActor,
                "Player", "Threat", new Vector3(-1.05f, 0f, 0f), 0.55f);
            SetActorCommand(commands.GetArrayElementAtIndex(2),
                "player_faces_threat", NarrativeCommandType.FaceActor,
                "Player", "Threat", duration: 0.2f);
            SetDialogue(commands.GetArrayElementAtIndex(3),
                "player_warning", "Player", "到此为止。离开这里。");
            SetActorCommand(commands.GetArrayElementAtIndex(4),
                "threat_angry", NarrativeCommandType.ShowSpeechBubble,
                "Threat", message: "这次先放过你们！", duration: 0.8f);
            SetActorCommand(commands.GetArrayElementAtIndex(5),
                "player_returns", NarrativeCommandType.ReturnToOrigin,
                "Player", duration: 0.45f);
            SetActorCommand(commands.GetArrayElementAtIndex(6),
                "release_player", NarrativeCommandType.ReleaseActorControl,
                "Player");
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void ConfigureObserve(SerializedProperty node)
        {
            SetNodeId(node, "observe");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 2;
            SetNarration(commands.GetArrayElementAtIndex(0),
                "observe_line", "你没有贸然介入。哥布林见附近有人，暂时退开了。");
            SetActorCommand(commands.GetArrayElementAtIndex(1),
                "threat_retreats", NarrativeCommandType.ShowSpeechBubble,
                "Threat", message: "别多管闲事。", duration: 0.75f);
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void ConfigureCleanup(SerializedProperty node)
        {
            SetNodeId(node, "cleanup");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 6;
            SetActorCommand(commands.GetArrayElementAtIndex(0),
                "merchant_returns", NarrativeCommandType.ReturnToOrigin,
                "Merchant", duration: 0.35f);
            SetActorCommand(commands.GetArrayElementAtIndex(1),
                "release_threat", NarrativeCommandType.ReleaseActorControl,
                "Threat");
            SetActorCommand(commands.GetArrayElementAtIndex(2),
                "despawn_threat", NarrativeCommandType.DespawnActor,
                "Threat");
            SetActorCommand(commands.GetArrayElementAtIndex(3),
                "release_merchant", NarrativeCommandType.ReleaseActorControl,
                "Merchant");
            SetNarration(commands.GetArrayElementAtIndex(4),
                "no_combat_yet", "冲突暂时平息，没有进入正式战斗。");
            SetCommand(commands.GetArrayElementAtIndex(5), "end",
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
            string id,
            NarrativeCommandType type)
        {
            command.FindPropertyRelative("commandId").stringValue = id;
            command.FindPropertyRelative("type").enumValueIndex = (int)type;
            command.FindPropertyRelative("failurePolicy").enumValueIndex =
                (int)NarrativeFailurePolicy.FailNarrative;
            command.FindPropertyRelative("failureNodeId").stringValue =
                string.Empty;
        }

        private static void SetActorCommand(
            SerializedProperty command,
            string id,
            NarrativeCommandType type,
            string actorRole,
            string targetRole = "",
            Vector3? offset = null,
            float duration = 0.5f,
            string message = "")
        {
            SetCommand(command, id, type);
            SerializedProperty actor = command.FindPropertyRelative(
                "actorActionParameters");
            actor.FindPropertyRelative("actorRole").stringValue = actorRole;
            actor.FindPropertyRelative("targetRole").stringValue = targetRole;
            actor.FindPropertyRelative("offset").vector3Value =
                offset ?? Vector3.zero;
            actor.FindPropertyRelative("duration").floatValue = duration;
            actor.FindPropertyRelative("message").stringValue = message;
        }

        private static void SetDialogue(
            SerializedProperty command,
            string id,
            string actorRole,
            string text)
        {
            SetCommand(command, id, NarrativeCommandType.ShowDialogue);
            SerializedProperty dialogue = command.FindPropertyRelative(
                "dialogueParameters");
            dialogue.FindPropertyRelative("actorRole").stringValue = actorRole;
            dialogue.FindPropertyRelative("fallbackText").stringValue = text;
        }

        private static void SetNarration(
            SerializedProperty command,
            string id,
            string text)
        {
            SetCommand(command, id, NarrativeCommandType.ShowNarration);
            command.FindPropertyRelative("dialogueParameters")
                .FindPropertyRelative("fallbackText").stringValue = text;
        }

        private static void SetChoice(
            SerializedProperty choice,
            string id,
            string text,
            string targetNode)
        {
            choice.FindPropertyRelative("choiceId").stringValue = id;
            choice.FindPropertyRelative("fallbackText").stringValue = text;
            choice.FindPropertyRelative("targetNodeId").stringValue =
                targetNode;
            choice.FindPropertyRelative("important").boolValue = true;
        }
    }
}
#endif
