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
                "npc_event.riverbend-grocer-ambush";
            serialized.FindProperty("version").intValue = 4;
            serialized.FindProperty("displayName").stringValue =
                "河湾村杂货商遇袭";
            serialized.FindProperty("canSkip").boolValue = true;
            serialized.FindProperty("allowCameraInput").boolValue = true;
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
            nodes.arraySize = 10;
            ConfigureBegin(nodes.GetArrayElementAtIndex(0));
            ConfigureConfront(nodes.GetArrayElementAtIndex(1));
            ConfigureObserve(nodes.GetArrayElementAtIndex(2));
            ConfigureVictory(nodes.GetArrayElementAtIndex(3));
            ConfigureDefeat(nodes.GetArrayElementAtIndex(4));
            ConfigureInterrupted(nodes.GetArrayElementAtIndex(5));
            ConfigureObserved(nodes.GetArrayElementAtIndex(6));
            ConfigureMerchantRobbed(nodes.GetArrayElementAtIndex(7));
            ConfigureMerchantSelfDefense(nodes.GetArrayElementAtIndex(8));
            ConfigureCleanup(nodes.GetArrayElementAtIndex(9));

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
            commands.arraySize = 11;
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
            SetBackgroundCombat(commands.GetArrayElementAtIndex(9),
                "merchant_goblin_fight",
                new[] { "Merchant" },
                new[] { "Threat" },
                "riverbend.market.ambush",
                CombatDefeatRule.PreserveAtOne,
                CombatDefeatRule.Lethal,
                new[]
                {
                    ("victory", "merchant_self_defense"),
                    ("defeat", "merchant_robbed"),
                    ("aborted", "interrupted")
                });

            SerializedProperty choice = commands.GetArrayElementAtIndex(10);
            SetCommand(choice, "player_decision",
                NarrativeCommandType.ShowChoice);
            SerializedProperty dialogue = choice.FindPropertyRelative(
                "dialogueParameters");
            dialogue.FindPropertyRelative("choiceTitleFallback").stringValue =
                "杂货商受伤了，哥布林正准备继续袭击。";
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
            commands.arraySize = 4;
            SetActorCommand(commands.GetArrayElementAtIndex(0),
                "control_player", NarrativeCommandType.AcquireActorControl,
                "Player");
            SetCommand(commands.GetArrayElementAtIndex(1),
                "before_combat", NarrativeCommandType.Checkpoint);
            SetInteraction(commands.GetArrayElementAtIndex(2),
                "fight_goblin", "core.combat",
                new[] { "Player", "Merchant" }, new[] { "Threat" },
                "riverbend.market.ambush");
            SerializedProperty execute = commands.GetArrayElementAtIndex(2);
            execute.FindPropertyRelative("failurePolicy").enumValueIndex =
                (int)NarrativeFailurePolicy.JumpToFailureNode;
            execute.FindPropertyRelative("failureNodeId").stringValue =
                "interrupted";
            SetOutcomeBranch(commands.GetArrayElementAtIndex(3),
                "branch_combat", new[]
                {
                    ("victory", "victory"),
                    ("defeat", "defeat"),
                    ("retreated", "defeat"),
                    ("aborted", "interrupted")
                });
        }

        private static void ConfigureObserve(SerializedProperty node)
        {
            SetNodeId(node, "observe");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 2;
            SetNarration(commands.GetArrayElementAtIndex(0),
                "observe_line", "你退到货架后观察，试图弄清哥布林为何而来。");
            SetCommand(commands.GetArrayElementAtIndex(1),
                "watch_fight", NarrativeCommandType.Wait);
            commands.GetArrayElementAtIndex(1)
                .FindPropertyRelative("timingParameters")
                .FindPropertyRelative("duration").floatValue = 3600f;
        }

        private static void ConfigureVictory(SerializedProperty node)
        {
            SetNodeId(node, "victory");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 5;
            SetDialogue(commands.GetArrayElementAtIndex(0),
                "merchant_thanks", "Merchant", "谢谢你救下我和这些货物。请收下报酬。");
            SetEffect(commands.GetArrayElementAtIndex(1),
                "merchant_reward", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.GiveCoins,
                "merchant_rescue_reward", "4bda315463bf4b73b63f1d232fb522e4",
                5);
            SetEffect(commands.GetArrayElementAtIndex(2),
                "merchant_rescued", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.SetWorldFactBool,
                "merchant_rescued_fact", "riverbend.merchant_rescued",
                boolValue: true);
            SetEffect(commands.GetArrayElementAtIndex(3),
                "report_merchant_rescued", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.ReportQuestEvent,
                "merchant_rescued_quest_event", "merchant_rescued", 1,
                secondaryTargetId: "victory",
                stringValue: "riverbend.market");
            SetNarration(commands.GetArrayElementAtIndex(4),
                "victory_result", "杂货商记住了你的援手，村民也开始谈论这场胜利。");
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void ConfigureDefeat(SerializedProperty node)
        {
            SetNodeId(node, "defeat");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 3;
            SetEffect(commands.GetArrayElementAtIndex(0),
                "ambush_failed", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.SetWorldFactBool,
                "merchant_ambush_failed", "riverbend.merchant_ambush_failed",
                boolValue: true);
            SetInteraction(commands.GetArrayElementAtIndex(1),
                "goblins_take_goods_after_party_defeat",
                NpcStockLossGameplayInteractionHandler.StockLossActionId,
                new[] { "Threat" }, new[] { "Merchant" },
                "riverbend.market.ambush");
            SetFloatArgument(commands.GetArrayElementAtIndex(1),
                "lossFraction", 0.5f);
            SetNarration(commands.GetArrayElementAtIndex(2),
                "defeat_result", "你没能阻止袭击，只能先保住性命。" );
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void ConfigureInterrupted(SerializedProperty node)
        {
            SetNodeId(node, "interrupted");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 2;
            SetEffect(commands.GetArrayElementAtIndex(0),
                "ambush_interrupted", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.SetWorldFactBool,
                "merchant_ambush_interrupted",
                "riverbend.merchant_ambush_interrupted", boolValue: true);
            SetNarration(commands.GetArrayElementAtIndex(1),
                "interrupted_result", "冲突没有正常开始，事件暂时中断。" );
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void ConfigureObserved(SerializedProperty node)
        {
            SetNodeId(node, "observed");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 3;
            SetEffect(commands.GetArrayElementAtIndex(0),
                "ambush_clue", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.SetWorldFactBool,
                "merchant_ambush_clue", "riverbend.merchant_ambush_clue",
                boolValue: true);
            SetEffect(commands.GetArrayElementAtIndex(1),
                "merchant_abandoned", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.IncrementWorldFactInt,
                "merchant_abandoned_fact",
                "riverbend.merchant_abandoned_count", 1);
            SetNarration(commands.GetArrayElementAtIndex(2),
                "observe_result", "你发现哥布林在寻找带有黑色蜡封的货箱，但杂货商也因此失去了部分货物。" );
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void ConfigureCleanup(SerializedProperty node)
        {
            SetNodeId(node, "cleanup");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 8;
            SetActorCommand(commands.GetArrayElementAtIndex(0),
                "end_conflict", NarrativeCommandType.EndConflictInteraction,
                "Merchant", "Threat", duration: 0f);
            SetActorCommand(commands.GetArrayElementAtIndex(1),
                "merchant_returns", NarrativeCommandType.ReturnToOrigin,
                "Merchant", duration: 0.35f);
            SetActorCommand(commands.GetArrayElementAtIndex(2),
                "release_threat", NarrativeCommandType.ReleaseActorControl,
                "Threat");
            SetActorCommand(commands.GetArrayElementAtIndex(3),
                "despawn_threat", NarrativeCommandType.DespawnActor,
                "Threat");
            SetActorCommand(commands.GetArrayElementAtIndex(4),
                "release_merchant", NarrativeCommandType.ReleaseActorControl,
                "Merchant");
            SetActorCommand(commands.GetArrayElementAtIndex(5),
                "release_player", NarrativeCommandType.ReleaseActorControl,
                "Player");
            SetNarration(commands.GetArrayElementAtIndex(6),
                "ambush_ended", "这场突发事件暂时告一段落。");
            SetCommand(commands.GetArrayElementAtIndex(7), "end",
                NarrativeCommandType.EndNarrative);
        }

        private static void SetDamage(
            SerializedProperty command,
            string resultId,
            string actorRole,
            int damage,
            bool nonLethal = false)
        {
            SetCommand(command, resultId, NarrativeCommandType.ApplyDamage);
            command.FindPropertyRelative("actorActionParameters")
                .FindPropertyRelative("actorRole").stringValue = actorRole;
            SerializedProperty effect = command.FindPropertyRelative(
                "effectParameters");
            effect.FindPropertyRelative("effectType").enumValueIndex =
                (int)NarrativeEffectType.ApplyDamage;
            effect.FindPropertyRelative("resultId").stringValue = resultId;
            effect.FindPropertyRelative("intValue").intValue = damage;
            effect.FindPropertyRelative("boolValue").boolValue = nonLethal;
        }

        private static void ConfigureMerchantRobbed(SerializedProperty node)
        {
            SetNodeId(node, "merchant_robbed");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 3;
            SetInteraction(commands.GetArrayElementAtIndex(0),
                "goblins_take_merchant_goods",
                NpcStockLossGameplayInteractionHandler.StockLossActionId,
                new[] { "Threat" }, new[] { "Merchant" },
                "riverbend.market.ambush");
            SetFloatArgument(commands.GetArrayElementAtIndex(0),
                "lossFraction", 0.5f);
            SetEffect(commands.GetArrayElementAtIndex(1),
                "merchant_was_robbed", NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.SetWorldFactBool,
                "merchant_was_robbed_fact",
                "riverbend.merchant_was_robbed", boolValue: true);
            SetNarration(commands.GetArrayElementAtIndex(2),
                "merchant_robbed_result",
                "杂货商支撑不住倒在一旁，哥布林趁机抢走了大约一半货物。" );
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void ConfigureMerchantSelfDefense(
            SerializedProperty node)
        {
            SetNodeId(node, "merchant_self_defense");
            SerializedProperty commands = node.FindPropertyRelative(
                "commands");
            commands.arraySize = 2;
            SetEffect(commands.GetArrayElementAtIndex(0),
                "merchant_defended_himself",
                NarrativeCommandType.SetWorldFact,
                NarrativeEffectType.SetWorldFactBool,
                "merchant_defended_himself_fact",
                "riverbend.merchant_defended_himself", boolValue: true);
            SetNarration(commands.GetArrayElementAtIndex(1),
                "merchant_self_defense_result",
                "杂货商拼尽全力击退了哥布林，但他记住了你选择旁观。" );
            node.FindPropertyRelative("nextNodeId").stringValue = "cleanup";
        }

        private static void SetEffect(
            SerializedProperty command,
            string commandId,
            NarrativeCommandType commandType,
            NarrativeEffectType effectType,
            string resultId,
            string targetId,
            int intValue = 0,
            bool boolValue = false,
            string secondaryTargetId = "",
            string stringValue = "")
        {
            SetCommand(command, commandId, commandType);
            SerializedProperty effect = command.FindPropertyRelative(
                "effectParameters");
            effect.FindPropertyRelative("effectType").enumValueIndex =
                (int)effectType;
            effect.FindPropertyRelative("resultId").stringValue = resultId;
            effect.FindPropertyRelative("targetId").stringValue = targetId;
            effect.FindPropertyRelative("secondaryTargetId").stringValue =
                secondaryTargetId;
            effect.FindPropertyRelative("stringValue").stringValue =
                stringValue;
            effect.FindPropertyRelative("intValue").intValue = intValue;
            effect.FindPropertyRelative("boolValue").boolValue = boolValue;
        }

        private static void SetInteraction(
            SerializedProperty command,
            string commandId,
            string actionId,
            string[] initiatorRoles,
            string[] targetRoles,
            string contextId,
            (string outcome, string target)[] legacyBranches = null)
        {
            SetCommand(command, commandId,
                NarrativeCommandType.ExecuteInteraction);
            SerializedProperty interaction = command.FindPropertyRelative(
                "interactionParameters");
            interaction.FindPropertyRelative("actionId").stringValue =
                actionId;
            interaction.FindPropertyRelative("contextId").stringValue =
                contextId;
            SetStrings(interaction.FindPropertyRelative("initiatorRoles"),
                initiatorRoles);
            SetStrings(interaction.FindPropertyRelative("targetRoles"),
                targetRoles);
            SerializedProperty branches = interaction.FindPropertyRelative(
                "outcomeBranches");
            branches.arraySize = legacyBranches?.Length ?? 0;
            for (int index = 0; index < branches.arraySize; index++)
            {
                branches.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("outcomeId").stringValue =
                    legacyBranches[index].outcome;
                branches.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("targetNodeId").stringValue =
                    legacyBranches[index].target;
            }
        }

        private static void SetOutcomeBranch(
            SerializedProperty command,
            string commandId,
            (string outcome, string target)[] outcomes)
        {
            SetCommand(command, commandId,
                NarrativeCommandType.BranchByInteractionOutcome);
            SerializedProperty branches = command.FindPropertyRelative(
                    "interactionParameters")
                .FindPropertyRelative("outcomeBranches");
            branches.arraySize = outcomes?.Length ?? 0;
            for (int index = 0; index < branches.arraySize; index++)
            {
                SerializedProperty branch = branches.GetArrayElementAtIndex(
                    index);
                branch.FindPropertyRelative("outcomeId").stringValue =
                    outcomes[index].outcome;
                branch.FindPropertyRelative("targetNodeId").stringValue =
                    outcomes[index].target;
            }
        }

        private static void SetStrings(
            SerializedProperty property,
            string[] values)
        {
            property.arraySize = values?.Length ?? 0;
            for (int index = 0; index < property.arraySize; index++)
                property.GetArrayElementAtIndex(index).stringValue =
                    values[index];
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
            ResetCommandPayload(command);
            command.FindPropertyRelative("commandId").stringValue = id;
            command.FindPropertyRelative("type").enumValueIndex = (int)type;
            command.FindPropertyRelative("failurePolicy").enumValueIndex =
                (int)NarrativeFailurePolicy.FailNarrative;
            command.FindPropertyRelative("failureNodeId").stringValue =
                string.Empty;
        }

        private static void SetBackgroundCombat(
            SerializedProperty command,
            string commandId,
            string[] friendlyRoles,
            string[] enemyRoles,
            string contextId,
            CombatDefeatRule friendlyRule,
            CombatDefeatRule enemyRule,
            (string outcome, string target)[] branches)
        {
            SetCommand(command, commandId,
                NarrativeCommandType.BeginBackgroundCombat);
            SerializedProperty interaction = command.FindPropertyRelative(
                "interactionParameters");
            interaction.FindPropertyRelative("actionId").stringValue =
                "narrative.background_combat";
            interaction.FindPropertyRelative("contextId").stringValue =
                contextId;
            SetStrings(interaction.FindPropertyRelative("initiatorRoles"),
                friendlyRoles);
            SetStrings(interaction.FindPropertyRelative("targetRoles"),
                enemyRoles);
            SerializedProperty arguments = interaction.FindPropertyRelative(
                "arguments");
            arguments.arraySize = 2;
            SetStringArgument(arguments.GetArrayElementAtIndex(0),
                "friendlyDefeatRule", friendlyRule.ToString());
            SetStringArgument(arguments.GetArrayElementAtIndex(1),
                "enemyDefeatRule", enemyRule.ToString());
            SerializedProperty outcomes = interaction.FindPropertyRelative(
                "outcomeBranches");
            outcomes.arraySize = branches?.Length ?? 0;
            for (int index = 0; index < outcomes.arraySize; index++)
            {
                outcomes.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("outcomeId").stringValue =
                    branches[index].outcome;
                outcomes.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("targetNodeId").stringValue =
                    branches[index].target;
            }
        }

        private static void SetStringArgument(
            SerializedProperty argument,
            string key,
            string value)
        {
            argument.FindPropertyRelative("key").stringValue = key;
            argument.FindPropertyRelative("valueType").enumValueIndex =
                (int)InteractionValueType.String;
            argument.FindPropertyRelative("stringValue").stringValue = value;
        }

        private static void SetFloatArgument(
            SerializedProperty command,
            string key,
            float value)
        {
            SerializedProperty arguments = command.FindPropertyRelative(
                    "interactionParameters")
                .FindPropertyRelative("arguments");
            arguments.arraySize = 1;
            SerializedProperty argument = arguments.GetArrayElementAtIndex(0);
            argument.FindPropertyRelative("key").stringValue = key;
            argument.FindPropertyRelative("valueType").enumValueIndex =
                (int)InteractionValueType.Float;
            argument.FindPropertyRelative("floatValue").floatValue = value;
        }

        private static void ResetCommandPayload(SerializedProperty command)
        {
            command.FindPropertyRelative("flowParameters")
                .FindPropertyRelative("targetNodeId").stringValue =
                string.Empty;

            SerializedProperty dialogue = command.FindPropertyRelative(
                "dialogueParameters");
            dialogue.FindPropertyRelative("actorRole").stringValue =
                string.Empty;
            dialogue.FindPropertyRelative("textKey").stringValue =
                string.Empty;
            dialogue.FindPropertyRelative("fallbackText").stringValue =
                string.Empty;
            dialogue.FindPropertyRelative("choiceTitleKey").stringValue =
                string.Empty;
            dialogue.FindPropertyRelative("choiceTitleFallback").stringValue =
                string.Empty;
            dialogue.FindPropertyRelative("choices").arraySize = 0;

            SerializedProperty actor = command.FindPropertyRelative(
                "actorActionParameters");
            actor.FindPropertyRelative("actorRole").stringValue = string.Empty;
            actor.FindPropertyRelative("targetRole").stringValue = string.Empty;
            actor.FindPropertyRelative("speed").floatValue = 0f;
            actor.FindPropertyRelative("duration").floatValue = 0f;
            actor.FindPropertyRelative("markerPosition").vector3Value =
                Vector3.zero;
            actor.FindPropertyRelative("offset").vector3Value = Vector3.zero;
            actor.FindPropertyRelative("message").stringValue = string.Empty;
            actor.FindPropertyRelative("emoteId").stringValue = string.Empty;

            command.FindPropertyRelative("timingParameters")
                .FindPropertyRelative("duration").floatValue = 0f;
            SerializedProperty media = command.FindPropertyRelative(
                "mediaParameters");
            media.FindPropertyRelative("assetReference").objectReferenceValue =
                null;
            media.FindPropertyRelative("waitForCompletion").boolValue = false;
            media.FindPropertyRelative("duration").floatValue = 0f;

            SerializedProperty interaction = command.FindPropertyRelative(
                "interactionParameters");
            interaction.FindPropertyRelative("actionId").stringValue =
                string.Empty;
            interaction.FindPropertyRelative("initiatorRoles").arraySize = 0;
            interaction.FindPropertyRelative("targetRoles").arraySize = 0;
            interaction.FindPropertyRelative("contextId").stringValue =
                string.Empty;
            interaction.FindPropertyRelative("arguments").arraySize = 0;
            interaction.FindPropertyRelative("outcomeBranches").arraySize = 0;
            interaction.FindPropertyRelative("timeoutSeconds").floatValue = 0f;

            SerializedProperty effect = command.FindPropertyRelative(
                "effectParameters");
            effect.FindPropertyRelative("effectType").enumValueIndex = 0;
            effect.FindPropertyRelative("resultId").stringValue = string.Empty;
            effect.FindPropertyRelative("targetId").stringValue = string.Empty;
            effect.FindPropertyRelative("secondaryTargetId").stringValue =
                string.Empty;
            effect.FindPropertyRelative("stringValue").stringValue =
                string.Empty;
            effect.FindPropertyRelative("intValue").intValue = 0;
            effect.FindPropertyRelative("boolValue").boolValue = false;
            command.FindPropertyRelative("barrierType").enumValueIndex =
                (int)NarrativeBarrierType.None;
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
