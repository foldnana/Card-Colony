using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Assigns real world-space Y heights to independent card stacks that
    /// remain overlapped after the regular X/Z physics pass.
    /// </summary>
    public static class CardPresentationLayerResolver
    {
        public static void Resolve(
            IList<CardStack> stacks,
            CardSettings settings,
            bool instant = false)
        {
            if (stacks == null || settings == null)
                return;

            List<CardStack> activeStacks = stacks
                .Where(stack =>
                    stack?.TopCard != null &&
                    !stack.IsBeingDragged &&
                    !stack.InheritsPresentationFromParentCard)
                .Distinct()
                .ToList();
            var adjacency = activeStacks.ToDictionary(
                stack => stack,
                _ => new List<CardStack>());

            for (int firstIndex = 0; firstIndex < activeStacks.Count; firstIndex++)
            {
                CardStack first = activeStacks[firstIndex];
                for (int secondIndex = firstIndex + 1;
                     secondIndex < activeStacks.Count;
                     secondIndex++)
                {
                    CardStack second = activeStacks[secondIndex];
                    if (!HasMeaningfulOverlap(
                            first,
                            second,
                            settings.MinimumResidualOverlapRatio))
                        continue;

                    adjacency[first].Add(second);
                    adjacency[second].Add(first);
                }
            }

            var visited = new HashSet<CardStack>();
            foreach (CardStack root in activeStacks)
            {
                if (!visited.Add(root))
                    continue;

                var component = new List<CardStack>();
                var pending = new Queue<CardStack>();
                pending.Enqueue(root);
                while (pending.Count > 0)
                {
                    CardStack current = pending.Dequeue();
                    component.Add(current);
                    foreach (CardStack neighbour in adjacency[current])
                    {
                        if (visited.Add(neighbour))
                            pending.Enqueue(neighbour);
                    }
                }

                ApplyComponentLayers(
                    component,
                    activeStacks,
                    settings,
                    instant);
            }
        }

        private static bool HasMeaningfulOverlap(
            CardStack first,
            CardStack second,
            float minimumOverlapRatio)
        {
            float overlapArea = CardPhysicsSolver.GetPlanarOverlapAreaAt(
                first,
                first.TargetPosition,
                second,
                0f);
            if (overlapArea <= 0f)
                return false;

            float smallerArea = Mathf.Min(
                first.Width * first.FullDepth,
                second.Width * second.FullDepth);
            if (smallerArea <= Mathf.Epsilon)
                return false;

            return overlapArea / smallerArea >=
                Mathf.Clamp01(minimumOverlapRatio);
        }

        private static void ApplyComponentLayers(
            List<CardStack> component,
            List<CardStack> inputOrder,
            CardSettings settings,
            bool instant)
        {
            if (component.Count == 1)
            {
                component[0].SetPresentationLayer(0, 0f, instant);
                return;
            }

            component.Sort((first, second) =>
            {
                int orderComparison = first.PresentationOrder.CompareTo(
                    second.PresentationOrder);
                return orderComparison != 0
                    ? orderComparison
                    : inputOrder.IndexOf(first).CompareTo(inputOrder.IndexOf(second));
            });

            float nextWorldBaseY = 0f;
            int maximumLayer = Mathf.Max(1, settings.MaxOverlapLayers) - 1;
            float maximumWorldBaseY = 0f;
            for (int index = 0; index < component.Count; index++)
            {
                CardStack stack = component[index];
                int layer = Mathf.Min(index, maximumLayer);
                float desiredWorldBaseY = index > maximumLayer
                    ? maximumWorldBaseY
                    : nextWorldBaseY;
                float baseY = index == 0
                    ? 0f
                    : Mathf.Max(
                        0f,
                        desiredWorldBaseY - stack.TargetPosition.y);
                stack.SetPresentationLayer(layer, baseY, instant);
                if (index == maximumLayer)
                    maximumWorldBaseY =
                        stack.TargetPosition.y + baseY;
                if (index >= maximumLayer)
                    continue;

                nextWorldBaseY = stack.PresentationTopY +
                    Mathf.Max(0f, settings.IndependentStackGap);
            }
        }
    }
}
