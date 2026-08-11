using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CardColony.Tests
{
    public class CardPhysicalLayeringUnityTests
    {
        [Test]
        public void LaterIndependentStack_RestsPhysicallyAboveEarlierOverlappingStack()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component earlierCard = CreateCard("Earlier Card", settings);
            Component laterCard = CreateCard("Later Card", settings);

            try
            {
                object earlierStack = earlierCard.GetType()
                    .GetProperty("Stack").GetValue(earlierCard);
                object laterStack = laterCard.GetType()
                    .GetProperty("Stack").GetValue(laterCard);
                MethodInfo setOrder = stackType.GetMethod(
                    "SetPresentationOrder",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(setOrder, Is.Not.Null,
                    "CardStack must expose a stable presentation order.");
                setOrder.Invoke(earlierStack, new object[] { 1L });
                setOrder.Invoke(laterStack, new object[] { 2L });

                System.Type resolverType = System.AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType(
                        "CryingSnow.StackCraft.CardPresentationLayerResolver"))
                    .FirstOrDefault(type => type != null);
                Assert.That(resolverType, Is.Not.Null,
                    "A dedicated physical presentation layer resolver is required.");
                MethodInfo resolve = resolverType.GetMethod(
                    "Resolve",
                    BindingFlags.Static | BindingFlags.Public);
                Assert.That(resolve, Is.Not.Null);

                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                stacks.Add(earlierStack);
                stacks.Add(laterStack);

                resolve.Invoke(
                    null,
                    new object[]
                    {
                        stacks,
                        settings,
                        true
                    });

                float earlierY = (float)stackType
                    .GetProperty("PresentationBaseY")
                    .GetValue(earlierStack);
                float laterY = (float)stackType
                    .GetProperty("PresentationBaseY")
                    .GetValue(laterStack);

                Assert.That(earlierY, Is.Zero.Within(0.0001f));
                Assert.That(laterY, Is.GreaterThan(earlierY));
                Assert.That(earlierCard.transform.position.y,
                    Is.EqualTo(earlierY).Within(0.0001f));
                Assert.That(laterCard.transform.position.y,
                    Is.EqualTo(laterY).Within(0.0001f));
                Assert.That(
                    ((Vector3)stackType.GetProperty("TargetPosition")
                        .GetValue(laterStack)).y,
                    Is.Zero.Within(0.0001f),
                    "Physical presentation height must not pollute the logical saved position.");
            }
            finally
            {
                Object.DestroyImmediate(earlierCard.gameObject);
                Object.DestroyImmediate(laterCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardManager_RegistrationAndBringToFrontMaintainStableOrder()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component earlierCard = CreateCard("Registered Earlier Card", settings);
            Component laterCard = CreateCard("Registered Later Card", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Presentation Order Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);

            try
            {
                object earlierStack = earlierCard.GetType()
                    .GetProperty("Stack").GetValue(earlierCard);
                object laterStack = laterCard.GetType()
                    .GetProperty("Stack").GetValue(laterCard);
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { earlierStack });
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { laterStack });

                long earlierOrder = (long)earlierStack.GetType()
                    .GetProperty("PresentationOrder").GetValue(earlierStack);
                long laterOrder = (long)laterStack.GetType()
                    .GetProperty("PresentationOrder").GetValue(laterStack);
                Assert.That(laterOrder, Is.GreaterThan(earlierOrder),
                    "Later registered stacks must begin above earlier stacks.");

                MethodInfo bringToFront = managerType.GetMethod(
                    "BringStackToFront",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(bringToFront, Is.Not.Null);
                bringToFront.Invoke(manager, new[] { earlierStack });

                Assert.That(
                    (long)earlierStack.GetType()
                        .GetProperty("PresentationOrder")
                        .GetValue(earlierStack),
                    Is.GreaterThan(laterOrder),
                    "The most recently interacted stack must become the top stack.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(earlierCard.gameObject);
                Object.DestroyImmediate(laterCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardManager_ResolveOverlapsAddsPhysicalLayersToResidualOverlap()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component earlierCard = CreateCard("Locked Earlier Card", settings);
            Component laterCard = CreateCard("Locked Later Card", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Residual Layer Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField(
                    "cardSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);

            try
            {
                object earlierStack = earlierCard.GetType()
                    .GetProperty("Stack").GetValue(earlierCard);
                object laterStack = laterCard.GetType()
                    .GetProperty("Stack").GetValue(laterCard);
                earlierStack.GetType().GetProperty("IsLocked")
                    .SetValue(earlierStack, true);
                laterStack.GetType().GetProperty("IsLocked")
                    .SetValue(laterStack, true);
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { earlierStack });
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { laterStack });

                managerType.GetMethod("ResolveOverlaps", System.Type.EmptyTypes)
                    .Invoke(manager, null);

                Assert.That(
                    (float)laterStack.GetType()
                        .GetProperty("PresentationBaseY")
                        .GetValue(laterStack),
                    Is.GreaterThan(
                        (float)earlierStack.GetType()
                            .GetProperty("PresentationBaseY")
                            .GetValue(earlierStack)),
                    "Residual overlap after the planar pass must receive physical Y layers.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(earlierCard.gameObject);
                Object.DestroyImmediate(laterCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardManager_UnregisterStackReturnsRemainingStackToGround()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component lowerCard = CreateCard("Removed Lower Card", settings);
            Component upperCard = CreateCard("Surviving Upper Card", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Layer Removal Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField(
                    "cardSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);

            try
            {
                object lowerStack = lowerCard.GetType()
                    .GetProperty("Stack").GetValue(lowerCard);
                object upperStack = upperCard.GetType()
                    .GetProperty("Stack").GetValue(upperCard);
                lowerStack.GetType().GetProperty("IsLocked")
                    .SetValue(lowerStack, true);
                upperStack.GetType().GetProperty("IsLocked")
                    .SetValue(upperStack, true);
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { lowerStack });
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { upperStack });
                managerType.GetMethod("ResolveOverlaps", System.Type.EmptyTypes)
                    .Invoke(manager, null);

                Assert.That(
                    (float)upperStack.GetType()
                        .GetProperty("PresentationBaseY")
                        .GetValue(upperStack),
                    Is.GreaterThan(0f));

                managerType.GetMethod("UnregisterStack")
                    .Invoke(manager, new[] { lowerStack });

                Assert.That(
                    (float)upperStack.GetType()
                        .GetProperty("PresentationBaseY")
                        .GetValue(upperStack),
                    Is.Zero.Within(0.0001f),
                    "Removing an overlapping stack must not leave the survivor floating.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(lowerCard.gameObject);
                Object.DestroyImmediate(upperCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Resolver_ClampsAbnormalOverlapGroupToConfiguredMaximumHeight()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            var cards = new List<Component>();

            try
            {
                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                MethodInfo setOrder = stackType.GetMethod("SetPresentationOrder");
                for (int index = 0; index < 14; index++)
                {
                    Component card = CreateCard($"Crowded Card {index}", settings);
                    cards.Add(card);
                    object stack = card.GetType().GetProperty("Stack")
                        .GetValue(card);
                    setOrder.Invoke(stack, new object[] { (long)index + 1L });
                    stacks.Add(stack);
                }

                System.Type resolverType = FindType(
                    "CryingSnow.StackCraft.CardPresentationLayerResolver");
                resolverType.GetMethod("Resolve").Invoke(
                    null,
                    new object[] { stacks, settings, true });

                int maximumLayers = (int)settingsType
                    .GetProperty("MaxOverlapLayers").GetValue(settings);
                float gap = (float)settingsType
                    .GetProperty("IndependentStackGap").GetValue(settings);
                float maximumAllowedBaseY = (maximumLayers - 1) * gap;
                float actualMaximumBaseY = stacks.Cast<object>()
                    .Max(stack => (float)stackType
                        .GetProperty("PresentationBaseY").GetValue(stack));

                Assert.That(actualMaximumBaseY,
                    Is.LessThanOrEqualTo(maximumAllowedBaseY + 0.0001f),
                    "An abnormal overlap group must not grow beyond the configured physical layer cap.");
            }
            finally
            {
                foreach (Component card in cards)
                    Object.DestroyImmediate(card.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardManager_BringToFrontImmediatelyReordersCurrentOverlap()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component firstCard = CreateCard("Bring Front First Card", settings);
            Component secondCard = CreateCard("Bring Front Second Card", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Immediate Bring Front Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField(
                    "cardSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);

            try
            {
                object firstStack = firstCard.GetType()
                    .GetProperty("Stack").GetValue(firstCard);
                object secondStack = secondCard.GetType()
                    .GetProperty("Stack").GetValue(secondCard);
                firstStack.GetType().GetProperty("IsLocked")
                    .SetValue(firstStack, true);
                secondStack.GetType().GetProperty("IsLocked")
                    .SetValue(secondStack, true);
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { firstStack });
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { secondStack });
                managerType.GetMethod("ResolveOverlaps", System.Type.EmptyTypes)
                    .Invoke(manager, null);

                managerType.GetMethod("BringStackToFront")
                    .Invoke(manager, new[] { firstStack });

                float firstY = (float)firstStack.GetType()
                    .GetProperty("PresentationBaseY").GetValue(firstStack);
                float secondY = (float)secondStack.GetType()
                    .GetProperty("PresentationBaseY").GetValue(secondStack);
                Assert.That(firstY, Is.GreaterThan(secondY),
                    "Bringing a stack to front must update the physical overlap immediately.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(firstCard.gameObject);
                Object.DestroyImmediate(secondCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardStack_MergePreservesNewestPresentationOrder()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component destinationCard = CreateCard("Older Destination Stack", settings);
            Component droppedCard = CreateCard("Newer Dropped Stack", settings);

            try
            {
                object destinationStack = destinationCard.GetType()
                    .GetProperty("Stack").GetValue(destinationCard);
                object droppedStack = droppedCard.GetType()
                    .GetProperty("Stack").GetValue(droppedCard);
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(destinationStack, new object[] { 2L });
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(droppedStack, new object[] { 7L });

                stackType.GetMethod("MergeWith")
                    .Invoke(destinationStack, new[] { droppedStack });

                Assert.That(
                    (long)stackType.GetProperty("PresentationOrder")
                        .GetValue(destinationStack),
                    Is.EqualTo(7L),
                    "A merge must not discard the newest physical presentation order.");
            }
            finally
            {
                Object.DestroyImmediate(destinationCard.gameObject);
                Object.DestroyImmediate(droppedCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Resolver_SeparatedStacksReturnToGroundAndRemainStable()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component firstCard = CreateCard("Separating First Card", settings);
            Component secondCard = CreateCard("Separating Second Card", settings);

            try
            {
                object firstStack = firstCard.GetType().GetProperty("Stack")
                    .GetValue(firstCard);
                object secondStack = secondCard.GetType().GetProperty("Stack")
                    .GetValue(secondCard);
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(firstStack, new object[] { 1L });
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(secondStack, new object[] { 2L });
                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                stacks.Add(firstStack);
                stacks.Add(secondStack);
                MethodInfo resolve = FindType(
                    "CryingSnow.StackCraft.CardPresentationLayerResolver")
                    .GetMethod("Resolve");
                resolve.Invoke(null, new object[] { stacks, settings, true });

                Assert.That(
                    (float)stackType.GetProperty("PresentationBaseY")
                        .GetValue(secondStack),
                    Is.GreaterThan(0f));

                stackType.GetMethod("SetTargetPosition")
                    .Invoke(secondStack, new object[]
                    {
                        new Vector3(3f, 0f, 0f),
                        true
                    });
                resolve.Invoke(null, new object[] { stacks, settings, true });
                resolve.Invoke(null, new object[] { stacks, settings, true });

                Assert.That(
                    (float)stackType.GetProperty("PresentationBaseY")
                        .GetValue(firstStack),
                    Is.Zero.Within(0.0001f));
                Assert.That(
                    (float)stackType.GetProperty("PresentationBaseY")
                        .GetValue(secondStack),
                    Is.Zero.Within(0.0001f));
                Assert.That(firstCard.transform.position.y,
                    Is.Zero.Within(0.0001f));
                Assert.That(secondCard.transform.position.y,
                    Is.Zero.Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(firstCard.gameObject);
                Object.DestroyImmediate(secondCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Resolver_AccountsForTheLowerStacksInternalThickness()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component lowerCard = CreateCard("Thick Lower Card", settings);
            Component lowerExtraCard = CreateCard("Thick Lower Extra Card", settings);
            Component upperCard = CreateCard("Card Above Thick Stack", settings);

            try
            {
                object lowerStack = lowerCard.GetType().GetProperty("Stack")
                    .GetValue(lowerCard);
                object extraStack = lowerExtraCard.GetType().GetProperty("Stack")
                    .GetValue(lowerExtraCard);
                object upperStack = upperCard.GetType().GetProperty("Stack")
                    .GetValue(upperCard);
                stackType.GetMethod("RemoveCard")
                    .Invoke(extraStack, new object[] { lowerExtraCard });
                stackType.GetMethod("AddCard")
                    .Invoke(lowerStack, new object[] { lowerExtraCard });
                stackType.GetMethod("SetTargetPosition")
                    .Invoke(lowerStack, new object[] { Vector3.zero, true });
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(lowerStack, new object[] { 1L });
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(upperStack, new object[] { 2L });
                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                stacks.Add(lowerStack);
                stacks.Add(upperStack);

                FindType("CryingSnow.StackCraft.CardPresentationLayerResolver")
                    .GetMethod("Resolve")
                    .Invoke(null, new object[] { stacks, settings, true });

                float internalStep = Mathf.Abs(
                    ((Vector3)settingsType.GetProperty("StackStep")
                        .GetValue(settings)).y);
                float gap = (float)settingsType
                    .GetProperty("IndependentStackGap").GetValue(settings);
                float upperBaseY = (float)stackType
                    .GetProperty("PresentationBaseY").GetValue(upperStack);
                Assert.That(upperBaseY,
                    Is.GreaterThanOrEqualTo(internalStep + gap - 0.0001f),
                    "The upper independent stack must clear every card in the lower logical stack.");
            }
            finally
            {
                Object.DestroyImmediate(lowerCard.gameObject);
                Object.DestroyImmediate(lowerExtraCard.gameObject);
                Object.DestroyImmediate(upperCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardManager_ResolveOverlapsWithStackOnTopUsesOneFinalOrder()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component firstCard = CreateCard("Final Order First Card", settings);
            Component secondCard = CreateCard("Final Order Second Card", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Final Order Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField(
                    "cardSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);

            try
            {
                object firstStack = firstCard.GetType().GetProperty("Stack")
                    .GetValue(firstCard);
                object secondStack = secondCard.GetType().GetProperty("Stack")
                    .GetValue(secondCard);
                firstStack.GetType().GetProperty("IsLocked")
                    .SetValue(firstStack, true);
                secondStack.GetType().GetProperty("IsLocked")
                    .SetValue(secondStack, true);
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { firstStack });
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { secondStack });

                MethodInfo resolveWithTop = managerType.GetMethod(
                    "ResolveOverlapsWithStackOnTop",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(resolveWithTop, Is.Not.Null,
                    "Movement completion needs one combined planar-and-presentation resolve entry point.");
                resolveWithTop.Invoke(manager, new[] { firstStack });

                Assert.That(
                    (float)firstStack.GetType().GetProperty("PresentationBaseY")
                        .GetValue(firstStack),
                    Is.GreaterThan(
                        (float)secondStack.GetType()
                            .GetProperty("PresentationBaseY")
                            .GetValue(secondStack)));
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(firstCard.gameObject);
                Object.DestroyImmediate(secondCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardController_PlanarDragDistanceIgnoresPresentationHeight()
        {
            System.Type controllerType = FindType(
                "CryingSnow.StackCraft.CardController");
            MethodInfo calculate = controllerType.GetMethod(
                "CalculatePlanarDragDistance",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(calculate, Is.Not.Null,
                "Click detection needs an explicit planar distance calculation.");

            float distance = (float)calculate.Invoke(
                null,
                new object[]
                {
                    new Vector3(1f, 0.024f, 2f),
                    new Vector3(1f, 0f, 2f)
                });

            Assert.That(distance, Is.Zero.Within(0.0001f),
                "A stationary card on a high physical layer must still register as a click.");
        }

        [Test]
        public void Resolver_UsesAbsoluteWorldHeightWhenLogicalBasesDiffer()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component earlierCard = CreateCard("Raised Logical Earlier Card", settings);
            Component laterCard = CreateCard("Grounded Logical Later Card", settings);

            try
            {
                object earlierStack = earlierCard.GetType().GetProperty("Stack")
                    .GetValue(earlierCard);
                object laterStack = laterCard.GetType().GetProperty("Stack")
                    .GetValue(laterCard);
                stackType.GetMethod("SetTargetPosition")
                    .Invoke(earlierStack, new object[]
                    {
                        new Vector3(0f, 0.02f, 0f),
                        true
                    });
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(earlierStack, new object[] { 1L });
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(laterStack, new object[] { 2L });
                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                stacks.Add(earlierStack);
                stacks.Add(laterStack);

                FindType("CryingSnow.StackCraft.CardPresentationLayerResolver")
                    .GetMethod("Resolve")
                    .Invoke(null, new object[] { stacks, settings, true });

                Assert.That(laterCard.transform.position.y,
                    Is.GreaterThan(earlierCard.transform.position.y),
                    "The later card must be physically above the earlier card even when their logical base Y values differ.");
            }
            finally
            {
                Object.DestroyImmediate(earlierCard.gameObject);
                Object.DestroyImmediate(laterCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Resolver_DoesNotMoveAnActivelyDraggedCardOffItsDragHeight()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component lowerCard = CreateCard("Drag Height Lower Card", settings);
            Component draggedCard = CreateCard("Actively Dragged Card", settings);

            try
            {
                object lowerStack = lowerCard.GetType().GetProperty("Stack")
                    .GetValue(lowerCard);
                object draggedStack = draggedCard.GetType().GetProperty("Stack")
                    .GetValue(draggedCard);
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(lowerStack, new object[] { 1L });
                stackType.GetMethod("SetPresentationOrder")
                    .Invoke(draggedStack, new object[] { 2L });
                draggedCard.GetType().GetProperty("IsBeingDragged")
                    .SetValue(draggedCard, true);
                stackType.GetMethod("SetDragTargetPosition")
                    .Invoke(draggedStack, new object[]
                    {
                        new Vector3(0f, 0.1f, 0f)
                    });
                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                stacks.Add(lowerStack);
                stacks.Add(draggedStack);

                FindType("CryingSnow.StackCraft.CardPresentationLayerResolver")
                    .GetMethod("Resolve")
                    .Invoke(null, new object[] { stacks, settings, true });

                Assert.That(draggedCard.transform.position.y,
                    Is.EqualTo(0.1f).Within(0.0001f),
                    "Presentation solves must not fight the temporary drag position.");
            }
            finally
            {
                Object.DestroyImmediate(lowerCard.gameObject);
                Object.DestroyImmediate(draggedCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardManager_SafeDragHeightClearsTallPresentationStacks()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component tallCard = CreateCard("Tall Presentation Card", settings);
            Component draggedCard = CreateCard("Drag Height Query Card", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Safe Drag Height Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField(
                    "cardSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);

            try
            {
                object tallStack = tallCard.GetType().GetProperty("Stack")
                    .GetValue(tallCard);
                object draggedStack = draggedCard.GetType().GetProperty("Stack")
                    .GetValue(draggedCard);
                tallStack.GetType().GetMethod("SetPresentationLayer")
                    .Invoke(tallStack, new object[] { 10, 0.12f, true });
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { tallStack });
                managerType.GetMethod("RegisterStack")
                    .Invoke(manager, new[] { draggedStack });

                MethodInfo safeHeight = managerType.GetMethod(
                    "GetSafeDragHeight",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(safeHeight, Is.Not.Null,
                    "Drag input needs a shared query that clears every physical presentation layer.");
                float result = (float)safeHeight.Invoke(
                    manager,
                    new object[] { draggedStack, 0.1f });

                Assert.That(result, Is.GreaterThan(0.12f),
                    "The dragged card must remain physically above an unusually tall overlap group.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(tallCard.gameObject);
                Object.DestroyImmediate(draggedCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardStack_SplitAtDoesNotPromotePresentationHeightIntoLogicalPosition()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component bottomCard = CreateCard("Split Bottom Card", settings);
            Component topCard = CreateCard("Split Top Card", settings);

            try
            {
                object stack = bottomCard.GetType().GetProperty("Stack")
                    .GetValue(bottomCard);
                stackType.GetMethod("AddCard").Invoke(stack, new object[] { topCard });
                stackType.GetMethod("SetPresentationLayer")
                    .Invoke(stack, new object[] { 4, 0.08f, true });

                object split = stackType.GetMethod("SplitAt")
                    .Invoke(stack, new object[] { topCard });
                Vector3 logicalPosition = (Vector3)stackType
                    .GetProperty("TargetPosition").GetValue(split);

                Assert.That(logicalPosition.y, Is.Zero.Within(0.0001f),
                    "Splitting a raised card must keep its new stack on the logical ground plane.");
            }
            finally
            {
                Object.DestroyImmediate(bottomCard.gameObject);
                Object.DestroyImmediate(topCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardStack_RemovingInternalThicknessReflowsOverlappingStacks()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component lowerCard = CreateCard("Thick Lower Card", settings);
            Component extraCard = CreateCard("Lower Stack Extra Card", settings);
            Component upperCard = CreateCard("Upper Reflow Card", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Thickness Reflow Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField("cardSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);

            try
            {
                object lowerStack = lowerCard.GetType().GetProperty("Stack").GetValue(lowerCard);
                object extraStack = extraCard.GetType().GetProperty("Stack").GetValue(extraCard);
                object upperStack = upperCard.GetType().GetProperty("Stack").GetValue(upperCard);
                stackType.GetMethod("RemoveCard").Invoke(extraStack, new object[] { extraCard });
                stackType.GetMethod("AddCard").Invoke(lowerStack, new object[] { extraCard });
                managerType.GetMethod("RegisterStack").Invoke(manager, new[] { lowerStack });
                managerType.GetMethod("RegisterStack").Invoke(manager, new[] { upperStack });
                managerType.GetMethod("ResolvePresentationLayers").Invoke(manager, new object[] { true });
                float before = (float)stackType.GetProperty("PresentationBaseY").GetValue(upperStack);

                stackType.GetMethod("RemoveCard").Invoke(lowerStack, new object[] { extraCard });
                float after = (float)stackType.GetProperty("PresentationBaseY").GetValue(upperStack);

                Assert.That(after, Is.LessThan(before),
                    "Removing stack thickness must immediately lower dependent presentation layers.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(lowerCard.gameObject);
                Object.DestroyImmediate(extraCard.gameObject);
                Object.DestroyImmediate(upperCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CardInstance_ExposesPresentationOnlyMovementPath()
        {
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            MethodInfo method = cardType.GetMethod(
                "SetPresentationTargetAnimated",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(method, Is.Not.Null,
                "Layer settling needs a movement path that does not cancel combat or level-up tweens.");
        }

        [Test]
        public void CardManager_SafeDragHeightIncludesCurrentTweenedWorldHeight()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component settlingCard = CreateCard("Still Settling Card", settings);
            Component draggedCard = CreateCard("Actual Height Drag Query", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Actual Height Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField("cardSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);

            try
            {
                object settlingStack = settlingCard.GetType().GetProperty("Stack").GetValue(settlingCard);
                object draggedStack = draggedCard.GetType().GetProperty("Stack").GetValue(draggedCard);
                managerType.GetMethod("RegisterStack").Invoke(manager, new[] { settlingStack });
                managerType.GetMethod("RegisterStack").Invoke(manager, new[] { draggedStack });
                settlingCard.transform.position = new Vector3(0f, 0.24f, 0f);

                float result = (float)managerType.GetMethod("GetSafeDragHeight")
                    .Invoke(manager, new object[] { draggedStack, 0.1f });

                Assert.That(result, Is.GreaterThan(0.24f),
                    "A falling presentation tween must not let the dragged card pass underneath its current world height.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(settlingCard.gameObject);
                Object.DestroyImmediate(draggedCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Resolver_ExcludesStacksParentedToAnotherCard()
        {
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component ownerCard = CreateCard("Dock Owner Card", settings);
            Component occupantCard = CreateCard("Docked Occupant Card", settings);

            try
            {
                occupantCard.transform.SetParent(ownerCard.transform, true);
                object ownerStack = ownerCard.GetType().GetProperty("Stack").GetValue(ownerCard);
                object occupantStack = occupantCard.GetType().GetProperty("Stack").GetValue(occupantCard);
                stackType.GetMethod("SetPresentationOrder").Invoke(ownerStack, new object[] { 1L });
                stackType.GetMethod("SetPresentationOrder").Invoke(occupantStack, new object[] { 2L });
                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                stacks.Add(ownerStack);
                stacks.Add(occupantStack);

                FindType("CryingSnow.StackCraft.CardPresentationLayerResolver")
                    .GetMethod("Resolve").Invoke(null, new object[] { stacks, settings, true });

                float childLayer = (float)stackType.GetProperty("PresentationBaseY")
                    .GetValue(occupantStack);
                Assert.That(childLayer, Is.Zero.Within(0.0001f),
                    "A docked card inherits its owner transform and must not receive a second independent layer.");
            }
            finally
            {
                occupantCard.transform.SetParent(null, true);
                Object.DestroyImmediate(ownerCard.gameObject);
                Object.DestroyImmediate(occupantCard.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CombatRect_ArrangesCombatantsAboveCurrentWorldCardHeight()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type rectType = FindType("CryingSnow.StackCraft.CombatRect");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component worldCard = CreateCard("Raised World Resource", settings);
            Component combatant = CreateCard("Combatant Above Resource", settings);
            PropertyInfo instanceProperty = managerType.GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.Static);
            object previousManager = instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, null);
            Component manager = new GameObject("Combat Height Manager")
                .AddComponent(managerType);
            instanceProperty.SetValue(null, manager);
            managerType.GetField("cardSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);
            GameObject rectObject = new GameObject(
                "Combat Height Rect",
                typeof(RectTransform));
            Component combatRect = rectObject.AddComponent(rectType);

            try
            {
                object worldStack = worldCard.GetType().GetProperty("Stack").GetValue(worldCard);
                worldStack.GetType().GetMethod("SetPresentationLayer")
                    .Invoke(worldStack, new object[] { 8, 0.14f, true });
                managerType.GetMethod("RegisterStack").Invoke(manager, new[] { worldStack });

                rectType.GetProperty("Rect", BindingFlags.Instance | BindingFlags.Public)
                    .SetValue(combatRect, rectObject.GetComponent<RectTransform>());
                rectType.GetField("cellSize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(combatRect, Vector2.one);
                System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
                System.Type listType = typeof(List<>).MakeGenericType(cardType);
                var attackers = (IList)System.Activator.CreateInstance(listType);
                var defenders = (IList)System.Activator.CreateInstance(listType);
                attackers.Add(combatant);
                rectType.GetField("_attackers", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(combatRect, attackers);
                rectType.GetField("_defenders", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(combatRect, defenders);

                rectType.GetMethod("ArrangeCards", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(combatRect, new object[] { false });

                Assert.That(combatant.transform.position.y,
                    Is.GreaterThan(worldCard.transform.position.y),
                    "Combat cards must render above immovable world resources intersecting the combat area.");
            }
            finally
            {
                instanceProperty.SetValue(null, previousManager);
                Object.DestroyImmediate(rectObject);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(worldCard.gameObject);
                Object.DestroyImmediate(combatant.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CombatRect_BoardReturnPositionRemovesCombatPresentationHeight()
        {
            System.Type rectType = FindType("CryingSnow.StackCraft.CombatRect");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            Component card = CreateCard("Elevated Returning Combatant", settings);

            try
            {
                card.transform.position = new Vector3(2f, 0.25f, -3f);
                MethodInfo method = rectType.GetMethod(
                    "GetBoardReturnPosition",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(method, Is.Not.Null,
                    "Combat cleanup needs a single conversion from presentation height back to the board plane.");
                Vector3 result = (Vector3)method.Invoke(null, new object[] { card });
                Assert.That(result, Is.EqualTo(new Vector3(2f, 0f, -3f)));
            }
            finally
            {
                Object.DestroyImmediate(card.gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        private static Component CreateCard(string name, ScriptableObject settings)
        {
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>();
            Component card = gameObject.AddComponent(cardType);
            cardType.GetProperty("Settings")
                .SetValue(card, settings);
            cardType.GetProperty("Size")
                .SetValue(card, Vector2.one);
            _ = System.Activator.CreateInstance(
                stackType,
                card,
                Vector3.zero);
            return card;
        }

        private static System.Type FindType(string fullName)
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }
    }
}
