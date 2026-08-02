using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardColony.Tests
{
    public sealed class CombatSystemUnityTests
    {
        [Test]
        public void CombatRandom_SameSeedProducesSameSequenceAndRestorableState()
        {
            Type type = FindType("CryingSnow.StackCraft.CombatRandom");
            object first = Activator.CreateInstance(type, new object[] { 123456u });
            object second = Activator.CreateInstance(type, new object[] { 123456u });
            MethodInfo nextFloat = type.GetMethod("NextFloat");

            float[] firstValues = Enumerable.Range(0, 8)
                .Select(_ => (float)nextFloat.Invoke(first, null))
                .ToArray();
            float[] secondValues = Enumerable.Range(0, 8)
                .Select(_ => (float)nextFloat.Invoke(second, null))
                .ToArray();

            Assert.That(secondValues, Is.EqualTo(firstValues));
            Assert.That(firstValues.All(value => value >= 0f && value < 1f), Is.True);
            Assert.That((uint)type.GetProperty("State").GetValue(first), Is.Not.EqualTo(0u));
        }

        [Test]
        public void CombatActionScheduler_UsesThresholdCapAndStableJoinOrder()
        {
            Type stateType = FindType("CryingSnow.StackCraft.CombatantTurnState");
            Type schedulerType = FindType("CryingSnow.StackCraft.CombatActionScheduler");
            object scheduler = Activator.CreateInstance(schedulerType);
            IList states = (IList)Activator.CreateInstance(
                typeof(System.Collections.Generic.List<>).MakeGenericType(stateType));
            object later = Activator.CreateInstance(stateType, new object[] { "later", 100f, 2L });
            object earlier = Activator.CreateInstance(stateType, new object[] { "earlier", 100f, 1L });
            states.Add(later);
            states.Add(earlier);

            object selected = schedulerType.GetMethod("SelectReadyCombatant")
                .Invoke(scheduler, new object[] { states });
            Assert.That(stateType.GetProperty("CombatantId").GetValue(selected),
                Is.EqualTo("earlier"));

            MethodInfo addProgress = schedulerType.GetMethod("AddProgress");
            Assert.That(addProgress.Invoke(scheduler, new object[] { 149f, 10f, 1f }),
                Is.EqualTo(150f));
            Assert.That(schedulerType.GetField("ActionThreshold").GetRawConstantValue(),
                Is.EqualTo(100f));
            Assert.That(schedulerType.GetField("ProgressCap").GetRawConstantValue(),
                Is.EqualTo(150f));
        }

        [Test]
        public void CombatResolutionRules_MatchDocumentedDamageAndRetreatFormulae()
        {
            Type type = FindType("CryingSnow.StackCraft.CombatResolutionRules");
            Assert.That(type.GetMethod("CalculateHitChance").Invoke(null, new object[] { 80f, 20f }),
                Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(type.GetMethod("CalculateHitChance").Invoke(null, new object[] { 0f, 100f }),
                Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(type.GetMethod("CalculateBaseDamage").Invoke(null, new object[] { 3f, 10f }),
                Is.EqualTo(1));
            Assert.That(type.GetMethod("CalculateSkillBaseDamage").Invoke(
                    null, new object[] { 10f, 6f, 1.5f, 0 }),
                Is.EqualTo(9));
            Assert.That(type.GetMethod("CalculateRetreatChance").Invoke(null, new object[] { 20f, 3 }),
                Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(type.GetMethod("CalculateRetreatChance").Invoke(null, new object[] { -100f, 20 }),
                Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void CombatCommandQueue_KeepsOnlyLatestCommandForEachActor()
        {
            Type commandType = FindType("CryingSnow.StackCraft.CombatCommand");
            Assert.That(FindType("CryingSnow.StackCraft.CombatActionResolver"), Is.Not.Null);
            Type commandKind = FindType("CryingSnow.StackCraft.CombatCommandType");
            Type queueType = FindType("CryingSnow.StackCraft.CombatCommandQueue");
            Assert.That(commandType.GetProperty("SessionId"), Is.Not.Null);
            Assert.That(commandType.GetProperty("DefinitionId"), Is.Not.Null);
            Assert.That(commandType.GetProperty("RequestedSequence"), Is.Not.Null);
            object queue = Activator.CreateInstance(queueType);
            object basic = Activator.CreateInstance(commandType);
            object skill = Activator.CreateInstance(commandType);
            Set(commandType, basic, "ActorId", "hero");
            Set(commandType, basic, "Type", Enum.Parse(commandKind, "BasicAttack"));
            Set(commandType, skill, "ActorId", "hero");
            Set(commandType, skill, "Type", Enum.Parse(commandKind, "UseSkill"));

            queueType.GetMethod("Queue").Invoke(queue, new[] { basic });
            queueType.GetMethod("Queue").Invoke(queue, new[] { skill });
            object queued = queueType.GetMethod("PeekForActor").Invoke(queue, new object[] { "hero" });

            Assert.That(commandType.GetProperty("Type").GetValue(queued),
                Is.EqualTo(Enum.Parse(commandKind, "UseSkill")));
            Assert.That((int)queueType.GetProperty("Count").GetValue(queue), Is.EqualTo(1));
        }

        [Test]
        public void CombatEventBuffer_KeepsLastHundredAndFormatsChineseLogText()
        {
            Type eventType = FindType("CryingSnow.StackCraft.CombatEvent");
            Type eventKind = FindType("CryingSnow.StackCraft.CombatEventType");
            Type bufferType = FindType("CryingSnow.StackCraft.CombatEventBuffer");
            object buffer = Activator.CreateInstance(bufferType);
            MethodInfo add = bufferType.GetMethod("Add");
            for (int index = 0; index < 105; index++)
            {
                object combatEvent = Activator.CreateInstance(eventType);
                Set(eventType, combatEvent, "Sequence", (long)index);
                Set(eventType, combatEvent, "Type", Enum.Parse(eventKind, "DamageApplied"));
                Set(eventType, combatEvent, "ActorName", "旅行者");
                Set(eventType, combatEvent, "TargetName", "史莱姆");
                Set(eventType, combatEvent, "Amount", 3);
                add.Invoke(buffer, new[] { combatEvent });
            }

            IEnumerable events = (IEnumerable)bufferType.GetProperty("Events").GetValue(buffer);
            object[] values = events.Cast<object>().ToArray();
            Assert.That(values.Length, Is.EqualTo(100));
            Assert.That(eventType.GetProperty("Sequence").GetValue(values[0]), Is.EqualTo(5L));

            Type formatterType = FindType("CryingSnow.StackCraft.CombatEventFormatter");
            string text = (string)formatterType.GetMethod("Format").Invoke(null, new[] { values[^1] });
            Assert.That(text, Does.Contain("旅行者"));
            Assert.That(text, Does.Contain("史莱姆"));
            Assert.That(text, Does.Contain("3"));
        }

        [Test]
        public void CombatContracts_MatchDocumentedEventsContentAndServices()
        {
            Type eventType = FindType("CryingSnow.StackCraft.CombatEvent");
            foreach (string member in new[]
            {
                "SourceId", "SourceName", "DefinitionId", "Value",
                "SecondaryValue", "HitType", "Advantage"
            })
            {
                Assert.That(eventType.GetProperty(member), Is.Not.Null,
                    $"CombatEvent is missing {member}.");
            }

            foreach (string typeName in new[]
            {
                "CryingSnow.StackCraft.CombatRewardService",
                "CryingSnow.StackCraft.CombatSkillService",
                "CryingSnow.StackCraft.CombatTargetingController",
                "CryingSnow.StackCraft.CombatLogView"
            })
            {
                Assert.That(FindType(typeName), Is.Not.Null);
            }

            Type cardDefinition = FindType("CryingSnow.StackCraft.CardDefinition");
            Assert.That(cardDefinition.GetProperty("InnateCombatSkills"), Is.Not.Null);
            Assert.That(cardDefinition.GetProperty("GrantedCombatSkills"), Is.Not.Null);
            Assert.That(cardDefinition.GetProperty("CombatItemDefinition"), Is.Not.Null);

            Type itemDefinition = FindType("CryingSnow.StackCraft.CombatItemDefinition");
            foreach (string member in new[]
            {
                "Id", "TargetRule", "EffectType", "Magnitude", "ConsumesAction"
            })
            {
                Assert.That(itemDefinition.GetProperty(member), Is.Not.Null,
                    $"CombatItemDefinition is missing {member}.");
            }
        }

        [Test]
        public void CombatEventFormatter_CriticalDamageUsesOneReadableDamageLine()
        {
            Type eventType = FindType("CryingSnow.StackCraft.CombatEvent");
            Type eventKind = FindType("CryingSnow.StackCraft.CombatEventType");
            Type hitType = FindType("CryingSnow.StackCraft.HitType");
            Type formatter = FindType("CryingSnow.StackCraft.CombatEventFormatter");

            object criticalSignal = Activator.CreateInstance(eventType);
            Set(eventType, criticalSignal, "Type", Enum.Parse(eventKind, "CriticalHit"));
            Assert.That(formatter.GetMethod("Format").Invoke(null, new[] { criticalSignal }),
                Is.EqualTo(string.Empty));

            object damage = Activator.CreateInstance(eventType);
            Set(eventType, damage, "Type", Enum.Parse(eventKind, "DamageApplied"));
            Set(eventType, damage, "SourceName", "旅行者");
            Set(eventType, damage, "TargetName", "史莱姆");
            Set(eventType, damage, "Value", 6);
            Set(eventType, damage, "HitType", Enum.Parse(hitType, "Critical"));
            string text = (string)formatter.GetMethod("Format").Invoke(null, new[] { damage });
            Assert.That(text, Does.Contain("暴击"));
            Assert.That(text, Does.Contain("6"));
        }

        [Test]
        public void InfoPanel_ExposesHighestPriorityForHudArbitration()
        {
            Type type = FindType("CryingSnow.StackCraft.InfoPanel");
            Assert.That(type.GetProperty("HighestActivePriority"), Is.Not.Null);
            Assert.That(type.GetEvent("HighestActivePriorityChanged"), Is.Not.Null);
        }

        [Test]
        public void BackpackReservation_BlocksMoveAndRemoveUntilReleased()
        {
            Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type entryType = FindType("CryingSnow.StackCraft.BackpackEntryData");
            object backpack = Activator.CreateInstance(backpackType);
            object card = Activator.CreateInstance(cardDataType);
            Set(cardDataType, card, "Id", "medicine");
            object[] addArguments = { card, null };
            Assert.That(backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments), Is.True);
            object entry = addArguments[1];
            string instanceId = (string)entryType.GetField("InstanceId").GetValue(entry);
            Type serviceType = FindType("CryingSnow.StackCraft.CombatItemReservationService");
            object service = Activator.CreateInstance(serviceType, new[] { backpack });

            Assert.That(serviceType.GetMethod("TryReserve").Invoke(
                service, new object[] { instanceId, "session", "command" }), Is.True);
            Assert.That(entryType.GetProperty("IsReserved").GetValue(entry), Is.True);
            Assert.That(backpackType.GetMethod("TryMoveEntry").Invoke(
                backpack, new object[] { instanceId, 1 }), Is.False);
            object[] removeArguments = { instanceId, null };
            Assert.That(backpackType.GetMethod("TryRemove").Invoke(backpack, removeArguments), Is.False);

            serviceType.GetMethod("ReleaseCommand").Invoke(service, new object[] { "command" });
            Assert.That(entryType.GetProperty("IsReserved").GetValue(entry), Is.False);
            Assert.That(backpackType.GetMethod("TryMoveEntry").Invoke(
                backpack, new object[] { instanceId, 1 }), Is.True);
        }

        [Test]
        public void CombatData_V2ContainsDeterministicRuntimeAndQueuedCommands()
        {
            Type combatDataType = FindType("CryingSnow.StackCraft.CombatData");
            foreach (string field in new[]
            {
                "Version", "SessionId", "RandomState", "CreatedSequence",
                "RuntimeStates", "QueuedCommands", "ResolvedDefeatIds"
            })
            {
                Assert.That(combatDataType.GetField(field), Is.Not.Null,
                    $"CombatData v2 is missing {field}.");
            }

            object data = Activator.CreateInstance(combatDataType);
            Assert.That(combatDataType.GetField("Version").GetValue(data), Is.EqualTo(2));
            Assert.That(combatDataType.GetMethod("NormalizeAndMigrate"), Is.Not.Null);
        }

        [Test]
        public void CombatRuntime_ExposesSessionCommandsEventsSkillsAndCooldownState()
        {
            Type taskType = FindType("CryingSnow.StackCraft.CombatTask");
            Type managerType = FindType("CryingSnow.StackCraft.CombatManager");
            Type cardDefinitionType = FindType("CryingSnow.StackCraft.CardDefinition");
            Type combatantType = FindType("CryingSnow.StackCraft.CardCombatant");
            Type commandType = FindType("CryingSnow.StackCraft.CombatCommand");

            Assert.That(taskType.GetProperty("SessionId"), Is.Not.Null);
            Assert.That(taskType.GetProperty("Phase"), Is.Not.Null);
            Assert.That(taskType.GetProperty("RandomState"), Is.Not.Null);
            Assert.That(taskType.GetMethod("QueueCommand", new[] { commandType }), Is.Not.Null);
            Assert.That(taskType.GetProperty("QueuedCommands"), Is.Not.Null);
            Assert.That(managerType.GetEvent("EventPublished"), Is.Not.Null);
            Assert.That(managerType.GetMethod("TrySubmitCommand", new[] { commandType }),
                Is.Not.Null);
            Assert.That(managerType.GetMethod("DeferSaveIfResolving"), Is.Not.Null);
            Assert.That(managerType.GetMethod("NotifyPresentationImpact"), Is.Not.Null);
            Assert.That(managerType.GetMethod("NotifyPresentationCompleted"), Is.Not.Null);
            Assert.That(FindType("CryingSnow.StackCraft.CombatPresentationController"),
                Is.Not.Null);
            Assert.That(cardDefinitionType.GetProperty("CombatSkills"), Is.Not.Null);
            Assert.That(combatantType.GetMethod("SetActionProgress"), Is.Not.Null);
            Assert.That(combatantType.GetMethod("ConsumeActionProgress"), Is.Not.Null);
            Assert.That(combatantType.GetMethod("GetSkillCooldown"), Is.Not.Null);
            Type rectType = FindType("CryingSnow.StackCraft.CombatRect");
            Assert.That(rectType.GetMethod("IsRetreatDropPosition"), Is.Not.Null);
        }

        [Test]
        public void CombatContent_ContainsPowerStrikeMedicineAndSerializedHud()
        {
            UnityEngine.Object skill = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/StackCraft/Resources/Combat/Skills/Skill_PowerStrike.asset");
            UnityEngine.Object item = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/StackCraft/Resources/Combat/Items/CombatItem_Medicine.asset");
            string uiYaml = File.ReadAllText(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");

            Assert.That(skill, Is.Not.Null);
            Assert.That(item, Is.Not.Null);
            Assert.That(item.GetType().GetProperty("Id").GetValue(item),
                Is.EqualTo("combat_item_medicine"));
            Assert.That(uiYaml, Does.Contain("m_Name: CombatLogPanel"),
                "战斗日志必须序列化在 UIRoot 预制体中。");
            Assert.That(uiYaml, Does.Contain("m_Name: CombatHudPanel"),
                "战斗操作页必须序列化在 UIRoot 预制体中。");
            Assert.That(uiYaml, Does.Contain(
                "guid: 73d90ac579304aa8b9b19e39f50542f6"));
            Assert.That(uiYaml, Does.Contain(
                "guid: 0fc2798ecfc34b02a3dc91696253064e"));
            Assert.That(uiYaml, Does.Contain(
                "guid: d5f71fb99a3a4796bfa7f3ced1e3e086"),
                "HUD message coordinator must be a serialized component, not a missing script.");
        }

        [Test]
        public void BackpackCombatItemDrag_HasDedicatedQueuePathAndReservationFeedback()
        {
            Type backpackView = FindType("CryingSnow.StackCraft.BackpackView");
            Type backpackItem = FindType("CryingSnow.StackCraft.BackpackItemView");
            Type proxy = FindType("CryingSnow.StackCraft.BackpackCardProxy");

            Assert.That(backpackView.GetProperty("IsPlayerCombatActive"), Is.Not.Null);
            Assert.That(backpackView.GetMethod("TryQueueCombatItem"), Is.Not.Null);
            Assert.That(backpackItem.GetProperty("IsReserved"), Is.Not.Null);
            Assert.That(proxy.GetProperty("EntryId"), Is.Not.Null);
        }

        [Test]
        public void CombatSafetyContracts_FlushTransitionsFocusDefaultsAndUseScaledPresentationTime()
        {
            Type managerType = FindType("CryingSnow.StackCraft.CombatManager");
            Assert.That(managerType.GetMethod("FlushResolvingActionsForSave"), Is.Not.Null,
                "Scene transitions and quit need an explicit stable combat save boundary.");

            string directorSource = File.ReadAllText(
                "Assets/StackCraft/Scripts/Core/GameDirector.cs");
            string managerSource = File.ReadAllText(
                "Assets/StackCraft/Scripts/Combat/CombatManager.cs");
            string taskSource = File.ReadAllText(
                "Assets/StackCraft/Scripts/Combat/CombatTask.cs");
            string hudSource = File.ReadAllText(
                "Assets/StackCraft/Scripts/Combat/UI/CombatHudPresenter.cs");

            Assert.That(directorSource, Does.Contain("SaveGameAtStableCombatBoundary()"));
            Assert.That(managerSource, Does.Contain("EnsureDefaultCombatFocus()"));
            Assert.That(managerSource, Does.Contain("CombatFocusService.Clear()"));
            Assert.That(hudSource, Does.Contain("CombatFocusService.Focus(activeCombat)"));
            Assert.That(taskSource, Does.Contain("presentationElapsed += delta"));
            Assert.That(taskSource, Does.Not.Contain("Time.unscaledTime - actionStartedAt"));
            Assert.That(taskSource, Does.Contain("CancelQueuedCommandForActor(card"));
            Assert.That(taskSource, Does.Contain("target != null && target.CurrentHealth > 0"));
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing runtime type {fullName}.");
            return type;
        }

        private static void Set(Type type, object instance, string memberName, object value)
        {
            PropertyInfo property = type.GetProperty(memberName);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo field = type.GetField(memberName);
            Assert.That(field, Is.Not.Null, $"Missing member {type.FullName}.{memberName}.");
            field.SetValue(instance, value);
        }

    }
}
