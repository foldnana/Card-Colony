using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CardColony.Tests
{
    public sealed class NarrativeP0BUnityTests
    {
        [Test]
        public void LocationNpcActivity_ControlLeasesAreOwnerScoped()
        {
            Type activityType = RequireType(
                "CryingSnow.StackCraft.LocationNpcActivity");
            var host = new GameObject("NarrativeNpcLeaseTest");
            try
            {
                Component activity = host.AddComponent(activityType);
                object firstOwner = new object();
                object secondOwner = new object();

                var first = (IDisposable)Invoke(
                    activity, "AcquirePause", firstOwner);
                var duplicate = (IDisposable)Invoke(
                    activity, "AcquirePause", firstOwner);
                var second = (IDisposable)Invoke(
                    activity, "AcquirePause", secondOwner);

                Assert.That(GetProperty<bool>(activity,
                    "IsInteractionPaused"), Is.True);
                first.Dispose();
                Assert.That(GetProperty<bool>(activity,
                    "IsInteractionPaused"), Is.True,
                    "同一所有者的重复租约以及其他所有者仍然持有控制权。");
                duplicate.Dispose();
                Assert.That(GetProperty<bool>(activity,
                    "IsInteractionPaused"), Is.True,
                    "释放一个所有者不能恢复另一个系统暂停的 NPC。");
                second.Dispose();
                Assert.That(GetProperty<bool>(activity,
                    "IsInteractionPaused"), Is.False);
                second.Dispose();
                Assert.That(GetProperty<bool>(activity,
                    "IsInteractionPaused"), Is.False,
                    "重复释放必须安全且幂等。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrativeCleanupScope_ReleasesActionsInReverseOrderOnce()
        {
            Type scopeType = RequireType(
                "CryingSnow.StackCraft.NarrativeCleanupScope");
            var released = new List<int>();
            object scope = Activator.CreateInstance(scopeType);
            Invoke(scope, "Push", (Action)(() => released.Add(1)));
            Invoke(scope, "Push", (Action)(() => released.Add(2)));

            Invoke(scope, "Dispose");
            Invoke(scope, "Dispose");

            Assert.That(released, Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void NarrativeDefinition_P0BDeclaresRequiredCommandsAndBarriers()
        {
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            string[] requiredCommands =
            {
                "Wait",
                "AcquireActorControl",
                "ReleaseActorControl",
                "MoveToActor",
                "MoveToMarker",
                "FaceActor",
                "ReturnToOrigin",
                "ShowSpeechBubble",
                "ShowEmote",
                "SpawnActor",
                "DespawnActor",
                "PlayCinematicAttack",
                "ApplyDamage",
                "EnterVisualNovelMode",
                "ExitVisualNovelMode",
                "ShowFullscreenImage",
                "HideFullscreenImage",
                "FocusActor",
                "ShakeCamera",
                "Checkpoint",
                "SceneTransition",
                "IrreversibleConfirmation"
            };
            foreach (string command in requiredCommands)
                Assert.That(Enum.GetNames(commandType), Does.Contain(command));

            Type barrierType = RequireType(
                "CryingSnow.StackCraft.NarrativeBarrierType");
            Assert.That(Enum.GetNames(barrierType), Is.EquivalentTo(new[]
            {
                "None",
                "ImportantChoice",
                "GameplayInteraction",
                "SceneTransition",
                "IrreversibleConfirmation",
                "Checkpoint",
                "NarrativeEnd"
            }));

            Type actorParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorActionParameters");
            foreach (string propertyName in new[]
                     {
                         "ActorRole", "TargetRole", "Speed", "Duration",
                         "MarkerPosition", "Offset", "Message", "EmoteId"
                     })
            {
                Assert.That(actorParametersType.GetProperty(propertyName),
                    Is.Not.Null, $"Missing {propertyName}.");
            }
        }

        [Test]
        public void NarrativeRuntime_SkipStopsAtImportantChoice()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type dialogueType = RequireType(
                "CryingSnow.StackCraft.NarrativeDialogueParameters");
            Type timingType = RequireType(
                "CryingSnow.StackCraft.NarrativeTimingParameters");
            Type choiceType = RequireType(
                "CryingSnow.StackCraft.NarrativeChoiceDefinition");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");

            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.p0b.skip");
                SetField(definition, "version", 1);
                SetField(definition, "canSkip", true);
                SetField(definition, "entryNodeId", "begin");

                object firstLine = CreateCommand(
                    commandType, commandKindType, "line-1", "ShowDialogue");
                object firstDialogue = Activator.CreateInstance(dialogueType);
                SetField(firstDialogue, "fallbackText", "第一句");
                SetField(firstLine, "dialogueParameters", firstDialogue);

                object wait = CreateCommand(
                    commandType, commandKindType, "wait", "Wait");
                object timing = Activator.CreateInstance(timingType);
                SetField(timing, "duration", 8f);
                SetField(wait, "timingParameters", timing);

                object skippedLine = CreateCommand(
                    commandType, commandKindType, "line-2", "ShowNarration");
                object skippedDialogue = Activator.CreateInstance(dialogueType);
                SetField(skippedDialogue, "fallbackText", "应该被跳过");
                SetField(skippedLine, "dialogueParameters", skippedDialogue);

                object choiceCommand = CreateCommand(
                    commandType, commandKindType, "choice", "ShowChoice");
                object choiceDialogue = Activator.CreateInstance(dialogueType);
                SetField(choiceDialogue, "choiceTitleFallback", "必须选择");
                object choice = Activator.CreateInstance(choiceType);
                SetField(choice, "choiceId", "continue");
                SetField(choice, "fallbackText", "继续");
                SetField(choice, "targetNodeId", "end");
                SetField(choice, "important", true);
                IList choices = CreateList(choiceType);
                choices.Add(choice);
                SetField(choiceDialogue, "choices", choices);
                SetField(choiceCommand, "dialogueParameters", choiceDialogue);

                object begin = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(firstLine);
                commands.Add(wait);
                commands.Add(skippedLine);
                commands.Add(choiceCommand);
                SetField(begin, "commands", commands);
                object end = CreateNode(nodeType, "end");
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                nodes.Add(end);
                SetField(definition, "nodes", nodes);

                object data = Activator.CreateInstance(gameDataType);
                object effects = Activator.CreateInstance(serviceType, data);
                object runtime = Activator.CreateInstance(runtimeType, effects);
                Assert.That(Invoke(runtime, "Start", definition, "skip-run"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForInput"));

                Assert.That(Invoke(runtime, "SkipToNextBarrier"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForChoice"));
                Assert.That(GetProperty<string>(runtime,
                    "CurrentChoiceTitle"), Is.EqualTo("必须选择"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void NarrativeActorControlService_ReleasesOnlyOwnedLease()
        {
            Type activityType = RequireType(
                "CryingSnow.StackCraft.LocationNpcActivity");
            Type handleType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorHandle");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorControlService");
            var host = new GameObject("NarrativeActorControlTest");
            try
            {
                Component activity = host.AddComponent(activityType);
                object foreignOwner = new object();
                var foreignLease = (IDisposable)Invoke(
                    activity, "AcquirePause", foreignOwner);
                Component card = host.GetComponent(RequireType(
                    "CryingSnow.StackCraft.CardInstance"));
                object handle = Activator.CreateInstance(
                    handleType, "Merchant", "商人", null, card);
                object service = Activator.CreateInstance(serviceType);

                Assert.That(Invoke(service, "Acquire", handle),
                    Is.EqualTo(true));
                Assert.That(Invoke(service, "IsControlled", "Merchant"),
                    Is.EqualTo(true));
                Assert.That(Invoke(service, "Release", "Merchant"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<bool>(activity,
                    "IsInteractionPaused"), Is.True,
                    "剧情释放后，其他系统的暂停租约仍必须有效。");

                foreignLease.Dispose();
                Assert.That(GetProperty<bool>(activity,
                    "IsInteractionPaused"), Is.False);
                Invoke(service, "Dispose");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrativeRuntime_SkipCompletesMovementAtChoiceBarrier()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type actorParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorActionParameters");
            Type dialogueType = RequireType(
                "CryingSnow.StackCraft.NarrativeDialogueParameters");
            Type choiceType = RequireType(
                "CryingSnow.StackCraft.NarrativeChoiceDefinition");
            Type handleType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorHandle");
            Type bindingType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorBinding");
            Type resolveModeType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorResolveMode");
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type controlType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorControlService");
            Type executorType = RequireType(
                "CryingSnow.StackCraft.NarrativeWorldActionExecutor");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");

            var actorObject = new GameObject("NarrativeMovingActor");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                Component card = actorObject.AddComponent(cardType);
                object actor = Activator.CreateInstance(
                    handleType, "Merchant", "商人", null, card);
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                    typeof(string), handleType);
                object actors = Activator.CreateInstance(dictionaryType);
                dictionaryType.GetMethod("Add")?.Invoke(
                    actors, new[] { "Merchant", actor });
                object controls = Activator.CreateInstance(controlType);
                object executor = Activator.CreateInstance(
                    executorType, actors, controls, null);

                SetField(definition, "id", "story.p0b.move-skip");
                SetField(definition, "version", 1);
                SetField(definition, "canSkip", true);
                SetField(definition, "entryNodeId", "begin");
                object binding = Activator.CreateInstance(bindingType);
                SetField(binding, "roleId", "Merchant");
                SetField(binding, "resolveMode", Enum.Parse(
                    resolveModeType, "PresentationOnly"));
                IList bindings = CreateList(bindingType);
                bindings.Add(binding);
                SetField(definition, "actorBindings", bindings);

                object acquire = CreateCommand(
                    commandType, commandKindType, "acquire",
                    "AcquireActorControl");
                object acquireParameters = Activator.CreateInstance(
                    actorParametersType);
                SetField(acquireParameters, "actorRole", "Merchant");
                SetField(acquire, "actorActionParameters", acquireParameters);

                object move = CreateCommand(
                    commandType, commandKindType, "move", "MoveToMarker");
                object moveParameters = Activator.CreateInstance(
                    actorParametersType);
                SetField(moveParameters, "actorRole", "Merchant");
                SetField(moveParameters, "markerPosition",
                    new Vector3(4f, 0f, 2f));
                SetField(moveParameters, "duration", 10f);
                SetField(move, "actorActionParameters", moveParameters);

                object choiceCommand = CreateCommand(
                    commandType, commandKindType, "choice", "ShowChoice");
                object choiceDialogue = Activator.CreateInstance(dialogueType);
                object choice = Activator.CreateInstance(choiceType);
                SetField(choice, "choiceId", "ok");
                SetField(choice, "fallbackText", "继续");
                SetField(choice, "targetNodeId", "end");
                SetField(choice, "important", true);
                IList choices = CreateList(choiceType);
                choices.Add(choice);
                SetField(choiceDialogue, "choices", choices);
                SetField(choiceCommand, "dialogueParameters", choiceDialogue);

                object begin = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(acquire);
                commands.Add(move);
                commands.Add(choiceCommand);
                SetField(begin, "commands", commands);
                object end = CreateNode(nodeType, "end");
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                nodes.Add(end);
                SetField(definition, "nodes", nodes);

                object data = Activator.CreateInstance(gameDataType);
                object effects = Activator.CreateInstance(serviceType, data);
                object runtime = Activator.CreateInstance(
                    runtimeType, effects, null, actors, executor);
                Assert.That(Invoke(runtime, "Start", definition, "move-run"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForPresentation"));

                Assert.That(Invoke(runtime, "SkipToNextBarrier"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForChoice"));
                Assert.That(actorObject.transform.position,
                    Is.EqualTo(new Vector3(4f, 0f, 2f)));

                Invoke(executor, "Dispose");
                Invoke(controls, "Dispose");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void NarrativePresentationService_RestoresVisualSurfacesOnDispose()
        {
            Type viewType = RequireType(
                "CryingSnow.StackCraft.NarrativePresentationView");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.NarrativePresentationService");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type mediaType = RequireType(
                "CryingSnow.StackCraft.NarrativeMediaParameters");
            var root = new GameObject("NarrativePresentationTestRoot");
            var mapMask = new GameObject("MapMask");
            var visualNovel = new GameObject("VisualNovel");
            var imageObject = new GameObject("FullscreenImage");
            var texture = new Texture2D(4, 4);
            try
            {
                mapMask.transform.SetParent(root.transform);
                visualNovel.transform.SetParent(root.transform);
                imageObject.transform.SetParent(root.transform);
                var fullscreenImage = imageObject.AddComponent<RawImage>();
                Component view = root.AddComponent(viewType);
                SetField(view, "mapMask", mapMask);
                SetField(view, "visualNovelRoot", visualNovel);
                SetField(view, "fullscreenImage", fullscreenImage);
                object service = Activator.CreateInstance(
                    serviceType, view, null);

                object enter = CreateCommand(
                    commandType, commandKindType, "enter-vn",
                    "EnterVisualNovelMode");
                object enterResult = Invoke(service, "Execute", enter, null,
                    false);
                Assert.That(GetProperty<bool>(enterResult, "IsCompleted"),
                    Is.True);
                Assert.That(visualNovel.activeSelf, Is.True);
                Assert.That(mapMask.activeSelf, Is.True);

                object showImage = CreateCommand(
                    commandType, commandKindType, "show-image",
                    "ShowFullscreenImage");
                object media = Activator.CreateInstance(mediaType);
                SetField(media, "assetReference", texture);
                SetField(showImage, "mediaParameters", media);
                Invoke(service, "Execute", showImage, null, false);
                Assert.That(fullscreenImage.texture, Is.EqualTo(texture));
                Assert.That(imageObject.activeSelf, Is.True);

                Invoke(service, "Dispose");
                Assert.That(visualNovel.activeSelf, Is.False);
                Assert.That(mapMask.activeSelf, Is.False);
                Assert.That(imageObject.activeSelf, Is.False);
                Assert.That(fullscreenImage.texture, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NarrativeDirector_SkipToEndRestoresInput()
        {
            Type inputType = RequireType(
                "CryingSnow.StackCraft.InputManager");
            Type directorType = RequireType(
                "CryingSnow.StackCraft.NarrativeDirector");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type timingType = RequireType(
                "CryingSnow.StackCraft.NarrativeTimingParameters");
            var inputHost = new GameObject("P0BInputManager");
            var directorHost = new GameObject("P0BNarrativeDirector");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                Component input = inputHost.AddComponent(inputType);
                Component director = directorHost.AddComponent(directorType);
                SetField(definition, "id", "story.p0b.director-skip");
                SetField(definition, "version", 1);
                SetField(definition, "canSkip", true);
                SetField(definition, "entryNodeId", "begin");
                object wait = CreateCommand(
                    commandType, commandKindType, "wait", "Wait");
                object timing = Activator.CreateInstance(timingType);
                SetField(timing, "duration", 30f);
                SetField(wait, "timingParameters", timing);
                object endCommand = CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative");
                object begin = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(wait);
                commands.Add(endCommand);
                SetField(begin, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                SetField(definition, "nodes", nodes);

                Assert.That(Invoke(director, "Play", definition, "skip-run"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(director, "State").ToString(),
                    Is.EqualTo("Playing"));
                Assert.That(Invoke(director, "SkipToNextBarrier"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(director, "State").ToString(),
                    Is.EqualTo("Idle"));
                Assert.That(GetProperty<bool>(input, "IsInputEnabled"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(directorHost);
                UnityEngine.Object.DestroyImmediate(inputHost);
            }
        }

        [Test]
        public void NarrativeValidator_P0BRejectsUnknownActorsAndMissingMedia()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type actorParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorActionParameters");
            Type validatorType = RequireType(
                "CryingSnow.StackCraft.NarrativeValidator");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.p0b.invalid");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object move = CreateCommand(
                    commandType, commandKindType, "move", "MoveToActor");
                object actorParameters = Activator.CreateInstance(
                    actorParametersType);
                SetField(actorParameters, "actorRole", "MissingActor");
                SetField(actorParameters, "targetRole", "MissingTarget");
                SetField(move, "actorActionParameters", actorParameters);
                object image = CreateCommand(
                    commandType, commandKindType, "image",
                    "ShowFullscreenImage");
                object begin = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(move);
                commands.Add(image);
                SetField(begin, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                SetField(definition, "nodes", nodes);

                object report = validatorType.GetMethod(
                    "Validate", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { definitionType }, null)
                    ?.Invoke(null, new object[] { definition });
                IList errors = GetProperty<IList>(report, "Errors");
                string joined = string.Join("\n", errors.Cast<object>());
                Assert.That(joined, Does.Contain("MissingActor"));
                Assert.That(joined, Does.Contain("MissingTarget"));
                Assert.That(joined, Does.Contain("ShowFullscreenImage"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void NarrativeActorResolver_SpawnTemporaryKeepsDefinitionId()
        {
            Type bindingType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorBinding");
            Type resolveModeType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorResolveMode");
            Type resolverType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorResolver");
            object binding = Activator.CreateInstance(bindingType);
            SetField(binding, "roleId", "GoblinLeader");
            SetField(binding, "resolveMode", Enum.Parse(
                resolveModeType, "SpawnTemporary"));
            SetField(binding, "cardDefinitionId", "goblin");
            object resolver = Activator.CreateInstance(resolverType);

            object actor = Invoke(resolver, "Resolve", binding);

            Assert.That(actor, Is.Not.Null);
            Assert.That(GetProperty<object>(actor, "Card"), Is.Null);
            Assert.That(GetProperty<string>(actor, "CardDefinitionId"),
                Is.EqualTo("goblin"));
        }

        [Test]
        public void NarrativePresentationPrefab_IsIndependentAndBootstrapped()
        {
            const string presentationPath =
                "Assets/StackCraft/Prefabs/UI/NarrativePresentation.prefab";
            const string uiRootPath =
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
            GameObject presentation = AssetDatabase.LoadAssetAtPath<GameObject>(
                presentationPath);
            Assert.That(presentation, Is.Not.Null,
                "P0-B 必须提供可直接编辑的独立剧情表现预制体。");
            Assert.That(presentation.GetComponent(RequireType(
                "CryingSnow.StackCraft.NarrativePresentationView")),
                Is.Not.Null);
            foreach (string childName in new[]
                     {
                         "MapMask", "FullscreenImage", "VisualNovelRoot",
                         "SkipButton"
                     })
            {
                Assert.That(FindDescendant(presentation.transform, childName),
                    Is.Not.Null, $"Missing {childName}.");
            }

            GameObject uiRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                uiRootPath);
            Assert.That(uiRoot, Is.Not.Null);
            Component bootstrap = uiRoot.GetComponentInChildren(
                RequireType(
                    "CryingSnow.StackCraft.NarrativePresentationBootstrap"),
                true);
            Assert.That(bootstrap, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(bootstrap);
            Assert.That(serialized.FindProperty("presentationPrefab")
                .objectReferenceValue, Is.EqualTo(presentation.GetComponent(
                    RequireType(
                        "CryingSnow.StackCraft.NarrativePresentationView"))));
        }

        [Test]
        public void WorldEffectService_ApplyDamageCommitsOnlyOnce()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            var host = new GameObject("NarrativeDamageTarget");
            try
            {
                Component card = host.AddComponent(cardType);
                SetField(card, "_renderer", host.GetComponent<MeshRenderer>());
                SetField(card, "<CurrentHealth>k__BackingField", 10);
                object data = Activator.CreateInstance(gameDataType);
                object service = Activator.CreateInstance(serviceType, data);

                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "Instantiating material due to calling renderer.material"));
                object first = Invoke(service, "ApplyDamage",
                    "story.p0b.damage", 1, "run-1", "begin",
                    "merchant_wounded", card, 3);
                object duplicate = Invoke(service, "ApplyDamage",
                    "story.p0b.damage", 1, "run-1", "begin",
                    "merchant_wounded", card, 3);

                Assert.That(GetProperty<bool>(first, "Applied"), Is.True);
                Assert.That(GetProperty<bool>(duplicate, "AlreadyApplied"),
                    Is.True);
                Assert.That(GetProperty<int>(card, "CurrentHealth"),
                    Is.EqualTo(7),
                    "相同 ResultId 恢复或跳过时不能重复扣血。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrativeWorldActionExecutor_ReturnToOriginRestoresRotation()
        {
            Type handleType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorHandle");
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type controlType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorControlService");
            Type executorType = RequireType(
                "CryingSnow.StackCraft.NarrativeWorldActionExecutor");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type actorParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorActionParameters");
            var host = new GameObject("NarrativeReturnActor");
            try
            {
                Component card = host.AddComponent(cardType);
                Quaternion originRotation = Quaternion.Euler(0f, 37f, 0f);
                host.transform.SetPositionAndRotation(
                    new Vector3(1f, 0f, 2f), originRotation);
                object actor = Activator.CreateInstance(
                    handleType, "Merchant", "商人", null, card);
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                    typeof(string), handleType);
                object actors = Activator.CreateInstance(dictionaryType);
                dictionaryType.GetMethod("Add")?.Invoke(
                    actors, new[] { "Merchant", actor });
                object controls = Activator.CreateInstance(controlType);
                Assert.That(Invoke(controls, "Acquire", actor), Is.True);
                object executor = Activator.CreateInstance(
                    executorType, actors, controls, null);

                host.transform.SetPositionAndRotation(
                    new Vector3(7f, 0f, 8f),
                    Quaternion.Euler(0f, 155f, 0f));
                object command = CreateCommand(
                    commandType, commandKindType, "return", "ReturnToOrigin");
                object parameters = Activator.CreateInstance(
                    actorParametersType);
                SetField(parameters, "actorRole", "Merchant");
                SetField(parameters, "duration", 5f);
                SetField(command, "actorActionParameters", parameters);

                object operation = Invoke(executor, "Execute", command, true);

                object result = GetProperty<object>(operation, "Result");
                Assert.That(GetProperty<bool>(result, "Success"), Is.True);
                Assert.That(host.transform.position,
                    Is.EqualTo(new Vector3(1f, 0f, 2f)));
                Assert.That(Quaternion.Angle(
                    host.transform.rotation, originRotation), Is.LessThan(0.1f));
                Invoke(executor, "Dispose");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RiverbendThreatSample_IsAValidP0BLocationNarrative()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            UnityEngine.Object definition = Resources.Load(
                "Narratives/Narrative_Riverbend_MerchantThreat",
                definitionType);
            Assert.That(definition, Is.Not.Null,
                "P0-B 必须包含可在河湾村触发的商人受威吓样例。");

            string narrativeId = GetProperty<string>(definition, "Id");
            Assert.That(narrativeId,
                Is.EqualTo("npc_event.riverbend-grocer")
                    .Or.EqualTo("npc_event.riverbend-grocer-ambush"));
            IEnumerable nodes = GetProperty<IEnumerable>(definition, "Nodes");
            string[] commandNames = nodes.Cast<object>()
                .SelectMany(node => GetProperty<IEnumerable>(node, "Commands")
                    .Cast<object>())
                .Select(command => GetProperty<object>(command, "Type")
                    .ToString())
                .ToArray();
            Assert.That(commandNames, Does.Contain("SpawnActor"));
            Assert.That(commandNames, Does.Contain("MoveToActor"));
            Assert.That(commandNames, Does.Contain("BeginBackgroundCombat"));
            Assert.That(commandNames, Does.Not.Contain("PlayCinematicAttack"),
                "杂货商遇袭应使用持续的真实战斗，而不是单次假攻击。 ");
            Assert.That(commandNames, Does.Contain("ShowChoice"));
            Assert.That(commandNames, Does.Contain("DespawnActor"));
            if (narrativeId == "npc_event.riverbend-grocer")
            {
                Assert.That(commandNames,
                    Does.Not.Contain("ExecuteInteraction"),
                    "P0-B v1 样例不得提前进入正式战斗。 ");
            }
            else
            {
                Assert.That(commandNames, Does.Contain("ExecuteInteraction"),
                    "P0-C v2 样例必须把同一事件升级为正式互动。 ");
            }

            Type validatorType = RequireType(
                "CryingSnow.StackCraft.NarrativeValidator");
            object report = validatorType.GetMethod(
                    "Validate", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { definitionType }, null)
                ?.Invoke(null, new[] { definition });
            Assert.That(GetProperty<bool>(report, "IsValid"), Is.True,
                string.Join("\n", GetProperty<IList>(report, "Errors")
                    .Cast<object>()));
        }

        [Test]
        public void NarrativeTemporaryActor_IsExcludedFromSceneSaveData()
        {
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type settingsType = RequireType(
                "CryingSnow.StackCraft.CardSettings");
            Type stackType = RequireType(
                "CryingSnow.StackCraft.CardStack");
            Type stackDataType = RequireType(
                "CryingSnow.StackCraft.StackData");
            var host = new GameObject("NarrativeTemporaryCard");
            ScriptableObject settings = ScriptableObject.CreateInstance(
                settingsType);
            try
            {
                Component card = host.AddComponent(cardType);
                SetField(card, "<Settings>k__BackingField", settings);
                Invoke(card, "MarkNarrativeTemporary");
                object stack = Activator.CreateInstance(
                    stackType, card, Vector3.zero);

                object data = Activator.CreateInstance(stackDataType, stack);

                IList cards = (IList)stackDataType.GetField("Cards")
                    .GetValue(data);
                Assert.That(cards, Is.Empty,
                    "剧情临时人物不能写入地点存档。 ");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrativePresentationService_RestoresCameraOnDispose()
        {
            Type cameraType = RequireType(
                "CryingSnow.StackCraft.CameraController");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.NarrativePresentationService");
            var host = new GameObject("NarrativeCameraRestore");
            try
            {
                host.transform.position = new Vector3(2f, 9f, -4f);
                Component cameraController = host.AddComponent(cameraType);
                object service = Activator.CreateInstance(
                    serviceType, null, cameraController);

                host.transform.position = new Vector3(11f, 8f, 7f);
                Invoke(service, "Dispose");

                Assert.That(host.transform.position,
                    Is.EqualTo(new Vector3(2f, 9f, -4f)),
                    "剧情结束或失败后必须恢复演出前镜头位置。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing type {fullName}.");
            return type;
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == methodName)
                .Where(candidate => candidate.GetParameters().Length ==
                    arguments.Length)
                .SingleOrDefault(candidate => candidate.GetParameters()
                    .Select((parameter, index) => new
                    {
                        parameter.ParameterType,
                        Value = arguments[index]
                    })
                    .All(pair => pair.Value == null ||
                        pair.ParameterType.IsInstanceOfType(pair.Value)));
            Assert.That(method, Is.Not.Null,
                $"Missing method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"Missing property {target.GetType().Name}.{propertyName}.");
            return (T)property.GetValue(target);
        }

        private static object CreateNode(Type nodeType, string id)
        {
            object node = Activator.CreateInstance(nodeType);
            SetField(node, "id", id);
            return node;
        }

        private static object CreateCommand(
            Type commandType,
            Type commandKindType,
            string id,
            string kind)
        {
            object command = Activator.CreateInstance(commandType);
            SetField(command, "commandId", id);
            SetField(command, "type", Enum.Parse(commandKindType, kind));
            return command;
        }

        private static IList CreateList(Type itemType) =>
            (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(itemType));

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing field {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }

        private static Transform FindDescendant(
            Transform root,
            string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
        }
    }
}
