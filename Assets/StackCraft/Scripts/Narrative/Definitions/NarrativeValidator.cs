using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public sealed class NarrativeValidationReport
    {
        private readonly List<string> errors = new();
        private readonly List<string> warnings = new();

        public bool IsValid => errors.Count == 0;
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                errors.Add(message);
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                warnings.Add(message);
        }
    }

    public static class NarrativeValidator
    {
        public static NarrativeValidationReport ValidateInteractions(
            NarrativeDefinition definition,
            GameplayInteractionRegistry registry)
        {
            NarrativeValidationReport report = Validate(definition);
            if (definition == null || registry == null)
            {
                if (registry == null)
                    report.AddError("互动注册表不能为空。");
                return report;
            }

            foreach (NarrativeCommandDefinition command in definition.Nodes
                         .Where(node => node != null)
                         .SelectMany(node => node.Commands ??
                             Array.Empty<NarrativeCommandDefinition>())
                         .Where(value => value != null && value.Type ==
                             NarrativeCommandType.ExecuteInteraction))
            {
                NarrativeInteractionParameters parameters =
                    command.InteractionParameters;
                string actionId = parameters?.ActionId;
                if (string.IsNullOrWhiteSpace(actionId))
                    continue;
                if (!registry.TryGet(actionId, out var handler))
                {
                    report.AddError(
                        $"互动指令 {command.CommandId} 使用了未注册的 " +
                        $"ActionId：{actionId}");
                    continue;
                }

                var arguments = (parameters.Arguments ??
                        Array.Empty<NarrativeInteractionArgument>())
                    .Where(value => value != null)
                    .Select(value => new GameplayInteractionArgument(
                        value.Key,
                        value.ValueType,
                        GetArgumentValue(value)))
                    .ToArray();
                InteractionValidationResult schema = handler.Schema?.Validate(
                    arguments) ?? InteractionValidationResult.Valid;
                if (!schema.IsValid)
                {
                    report.AddError(
                        $"互动指令 {command.CommandId} 参数不符合 " +
                        $"{actionId} Schema：{schema.ErrorCode}");
                }
            }
            return report;
        }

        public static NarrativeValidationReport ValidateAll(
            IEnumerable<NarrativeDefinition> definitions)
        {
            var report = new NarrativeValidationReport();
            NarrativeDefinition[] values = (definitions ??
                    Array.Empty<NarrativeDefinition>())
                .Where(value => value != null)
                .ToArray();
            foreach (NarrativeDefinition definition in values)
            {
                NarrativeValidationReport item = Validate(definition);
                foreach (string error in item.Errors)
                    report.AddError($"[{definition.name}] {error}");
                foreach (string warning in item.Warnings)
                    report.AddWarning($"[{definition.name}] {warning}");
            }
            foreach (IGrouping<string, NarrativeDefinition> duplicate in
                     values.GroupBy(value => value.Id ?? string.Empty,
                             StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                report.AddError($"剧情 ID 重复：{duplicate.Key}");
            }
            return report;
        }

        public static NarrativeValidationReport Validate(
            NarrativeDefinition definition)
        {
            var report = new NarrativeValidationReport();
            if (definition == null)
            {
                report.AddError("剧情资源不能为空。");
                return report;
            }
            if (string.IsNullOrWhiteSpace(definition.Id))
                report.AddError("剧情 ID 不能为空。");
            if (definition.SerializedVersion < 1)
                report.AddError("剧情版本必须大于 0。");
            if (string.IsNullOrWhiteSpace(definition.EntryNodeId))
                report.AddError("入口节点不能为空。");

            IReadOnlyList<NarrativeNodeDefinition> nodes =
                definition.Nodes ?? Array.Empty<NarrativeNodeDefinition>();
            foreach (IGrouping<string, NarrativeNodeDefinition> duplicate in
                     nodes.Where(node => node != null)
                         .GroupBy(node => node.Id ?? string.Empty)
                         .Where(group => group.Count() > 1))
            {
                report.AddError($"节点 ID 重复：{duplicate.Key}");
            }
            if (!string.IsNullOrWhiteSpace(definition.EntryNodeId) &&
                definition.FindNode(definition.EntryNodeId) == null)
            {
                report.AddError(
                    $"入口节点不存在：{definition.EntryNodeId}");
            }
            var nodeIds = new HashSet<string>(
                nodes.Where(node => node != null &&
                        !string.IsNullOrWhiteSpace(node.Id))
                    .Select(node => node.Id),
                StringComparer.Ordinal);
            var resultIds = new HashSet<string>(StringComparer.Ordinal);
            var actorRoles = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeActorBinding binding in
                     definition.ActorBindings ??
                     Array.Empty<NarrativeActorBinding>())
            {
                if (binding == null)
                    continue;
                if (string.IsNullOrWhiteSpace(binding.RoleId))
                    report.AddError("参与者 RoleId 不能为空。");
                else if (!actorRoles.Add(binding.RoleId))
                    report.AddError($"参与者 RoleId 重复：{binding.RoleId}");
                if (binding.ResolveMode == NarrativeActorResolveMode.PersistentId &&
                    string.IsNullOrWhiteSpace(binding.PersistentId))
                {
                    report.AddError(
                        $"参与者 {binding.RoleId} 缺少 PersistentId。");
                }
                if (binding.ResolveMode is
                        NarrativeActorResolveMode.CardDefinitionId or
                        NarrativeActorResolveMode.SpawnTemporary &&
                    string.IsNullOrWhiteSpace(binding.CardDefinitionId))
                {
                    report.AddError(
                        $"参与者 {binding.RoleId} 缺少 CardDefinitionId。");
                }
            }

            foreach (NarrativeNodeDefinition node in nodes)
            {
                if (node == null)
                {
                    report.AddError("剧情包含空节点。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(node.Id))
                    report.AddError("节点 ID 不能为空。");
                ValidateTarget(node.NextNodeId, node.Id, "NextNodeId",
                    nodeIds, report);
                ValidateTarget(node.FailureNodeId, node.Id, "FailureNodeId",
                    nodeIds, report);
                var commandIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (NarrativeCommandDefinition command in
                         node.Commands ??
                         Array.Empty<NarrativeCommandDefinition>())
                {
                    ValidateCommand(
                        node,
                        command,
                        nodeIds,
                        actorRoles,
                        commandIds,
                        resultIds,
                        report);
                }
            }
            return report;
        }

        private static void ValidateCommand(
            NarrativeNodeDefinition node,
            NarrativeCommandDefinition command,
            HashSet<string> nodeIds,
            HashSet<string> actorRoles,
            HashSet<string> commandIds,
            HashSet<string> resultIds,
            NarrativeValidationReport report)
        {
            if (command == null)
            {
                report.AddError($"节点 {node.Id} 包含空指令。");
                return;
            }
            if (string.IsNullOrWhiteSpace(command.CommandId))
                report.AddError($"节点 {node.Id} 的指令 ID 不能为空。");
            else if (!commandIds.Add(command.CommandId))
                report.AddError(
                    $"节点 {node.Id} 的指令 ID 重复：{command.CommandId}");

            ValidateTarget(command.FailureNodeId, node.Id,
                $"指令 {command.CommandId} FailureNodeId", nodeIds, report);
            switch (command.Type)
            {
                case NarrativeCommandType.Jump:
                    ValidateRequiredTarget(
                        command.FlowParameters?.TargetNodeId,
                        node.Id,
                        $"指令 {command.CommandId} 跳转",
                        nodeIds,
                        report);
                    break;
                case NarrativeCommandType.ShowNarration:
                case NarrativeCommandType.ShowDialogue:
                    ValidateDialogue(command, actorRoles, report);
                    break;
                case NarrativeCommandType.ShowChoice:
                    ValidateChoices(command, nodeIds, report);
                    break;
                case NarrativeCommandType.SetWorldFact:
                case NarrativeCommandType.StartQuest:
                    ValidateEffect(command, resultIds, report,
                        requiresTargetId: true);
                    break;
                case NarrativeCommandType.ApplyDamage:
                    ValidateActorAction(
                        command, actorRoles, requiresTarget: false, report);
                    ValidateEffect(command, resultIds, report,
                        requiresTargetId: false);
                    if ((command.EffectParameters?.IntValue ?? 0) <= 0)
                    {
                        report.AddError(
                            $"伤害指令 {command.CommandId} 的伤害值必须大于 0。");
                    }
                    break;
                case NarrativeCommandType.ExecuteInteraction:
                    ValidateInteraction(command, nodeIds, report);
                    break;
                case NarrativeCommandType.AcquireActorControl:
                case NarrativeCommandType.ReleaseActorControl:
                case NarrativeCommandType.MoveToMarker:
                case NarrativeCommandType.ReturnToOrigin:
                case NarrativeCommandType.ShowSpeechBubble:
                case NarrativeCommandType.ShowEmote:
                case NarrativeCommandType.SpawnActor:
                case NarrativeCommandType.DespawnActor:
                case NarrativeCommandType.FocusActor:
                    ValidateActorAction(
                        command, actorRoles, requiresTarget: false, report);
                    break;
                case NarrativeCommandType.MoveToActor:
                case NarrativeCommandType.FaceActor:
                case NarrativeCommandType.PlayCinematicAttack:
                    ValidateActorAction(
                        command, actorRoles, requiresTarget: true, report);
                    break;
                case NarrativeCommandType.ShowFullscreenImage:
                    if (command.MediaParameters?.AssetReference == null)
                    {
                        report.AddError(
                            $"指令 {command.CommandId} ShowFullscreenImage " +
                            "缺少图片资源。");
                    }
                    break;
            }
        }

        private static void ValidateActorAction(
            NarrativeCommandDefinition command,
            HashSet<string> actorRoles,
            bool requiresTarget,
            NarrativeValidationReport report)
        {
            NarrativeActorActionParameters parameters =
                command.ActorActionParameters;
            if (parameters == null ||
                string.IsNullOrWhiteSpace(parameters.ActorRole))
            {
                report.AddError(
                    $"指令 {command.CommandId} 缺少 ActorRole。");
                return;
            }
            if (!actorRoles.Contains(parameters.ActorRole))
            {
                report.AddError(
                    $"指令 {command.CommandId} 引用了未声明角色：" +
                    parameters.ActorRole);
            }
            if (string.IsNullOrWhiteSpace(parameters.TargetRole))
            {
                if (requiresTarget)
                {
                    report.AddError(
                        $"指令 {command.CommandId} 缺少 TargetRole。");
                }
            }
            else if (!actorRoles.Contains(parameters.TargetRole))
            {
                report.AddError(
                    $"指令 {command.CommandId} 引用了未声明目标角色：" +
                    parameters.TargetRole);
            }
        }

        private static void ValidateDialogue(
            NarrativeCommandDefinition command,
            HashSet<string> actorRoles,
            NarrativeValidationReport report)
        {
            NarrativeDialogueParameters parameters =
                command.DialogueParameters;
            if (parameters == null)
            {
                report.AddError(
                    $"指令 {command.CommandId} 缺少 DialogueParameters。");
                return;
            }
            if (!string.IsNullOrWhiteSpace(parameters.ActorRole) &&
                !actorRoles.Contains(parameters.ActorRole))
            {
                report.AddError(
                    $"指令 {command.CommandId} 引用了未声明角色：" +
                    parameters.ActorRole);
            }
            if (string.IsNullOrWhiteSpace(parameters.TextKey) &&
                string.IsNullOrWhiteSpace(parameters.FallbackText))
            {
                report.AddError(
                    $"指令 {command.CommandId} 缺少 TextKey 和 FallbackText。");
            }
        }

        private static void ValidateChoices(
            NarrativeCommandDefinition command,
            HashSet<string> nodeIds,
            NarrativeValidationReport report)
        {
            IReadOnlyList<NarrativeChoiceDefinition> choices =
                command.DialogueParameters?.Choices ??
                Array.Empty<NarrativeChoiceDefinition>();
            if (choices.Count == 0)
                report.AddError($"选择指令 {command.CommandId} 没有选项。");
            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeChoiceDefinition choice in choices)
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.ChoiceId))
                {
                    report.AddError(
                        $"选择指令 {command.CommandId} 包含空 ChoiceId。");
                    continue;
                }
                if (!choiceIds.Add(choice.ChoiceId))
                    report.AddError(
                        $"选择指令 {command.CommandId} 的 ChoiceId 重复：" +
                        choice.ChoiceId);
                ValidateRequiredTarget(choice.TargetNodeId, string.Empty,
                    $"选项 {choice.ChoiceId}", nodeIds, report);
            }
        }

        private static void ValidateEffect(
            NarrativeCommandDefinition command,
            HashSet<string> resultIds,
            NarrativeValidationReport report,
            bool requiresTargetId)
        {
            NarrativeEffectParameters effect = command.EffectParameters;
            if (effect == null)
            {
                report.AddError(
                    $"结果指令 {command.CommandId} 缺少 EffectParameters。");
                return;
            }
            if (string.IsNullOrWhiteSpace(effect.ResultId))
                report.AddError($"结果指令 {command.CommandId} 缺少 ResultId。");
            else if (!resultIds.Add(effect.ResultId))
                report.AddError($"ResultId 重复：{effect.ResultId}");
            if (requiresTargetId && string.IsNullOrWhiteSpace(effect.TargetId))
                report.AddError($"结果指令 {command.CommandId} 缺少 TargetId。");
        }

        private static void ValidateInteraction(
            NarrativeCommandDefinition command,
            HashSet<string> nodeIds,
            NarrativeValidationReport report)
        {
            NarrativeInteractionParameters parameters =
                command.InteractionParameters;
            if (parameters == null ||
                string.IsNullOrWhiteSpace(parameters.ActionId))
            {
                report.AddError(
                    $"互动指令 {command.CommandId} 的 ActionId 不能为空。");
                return;
            }
            foreach (NarrativeInteractionOutcomeBranch branch in
                     parameters.OutcomeBranches ??
                     Array.Empty<NarrativeInteractionOutcomeBranch>())
            {
                if (branch == null || string.IsNullOrWhiteSpace(branch.OutcomeId))
                {
                    report.AddError(
                        $"互动指令 {command.CommandId} 包含空 OutcomeId。");
                    continue;
                }
                ValidateRequiredTarget(branch.TargetNodeId, string.Empty,
                    $"互动结果 {branch.OutcomeId}", nodeIds, report);
            }
        }

        private static void ValidateRequiredTarget(
            string targetNodeId,
            string sourceNodeId,
            string field,
            HashSet<string> nodeIds,
            NarrativeValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(targetNodeId))
            {
                report.AddError($"{field} 缺少目标节点。");
                return;
            }
            ValidateTarget(targetNodeId, sourceNodeId, field, nodeIds, report);
        }

        private static void ValidateTarget(
            string targetNodeId,
            string sourceNodeId,
            string field,
            HashSet<string> nodeIds,
            NarrativeValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(targetNodeId))
                return;
            if (!nodeIds.Contains(targetNodeId))
            {
                report.AddError(
                    $"{field} 引用了不存在的节点 {targetNodeId}" +
                    (string.IsNullOrWhiteSpace(sourceNodeId)
                        ? "。"
                        : $"（来源：{sourceNodeId}）。"));
            }
        }

        private static object GetArgumentValue(
            NarrativeInteractionArgument argument) =>
            argument.ValueType switch
            {
                InteractionValueType.Int => argument.IntValue,
                InteractionValueType.Float => argument.FloatValue,
                InteractionValueType.Bool => argument.BoolValue,
                _ => argument.StringValue
            };
    }
}
