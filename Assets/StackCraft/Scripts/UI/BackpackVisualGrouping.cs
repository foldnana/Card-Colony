using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public sealed class BackpackVisualGroup
    {
        internal BackpackVisualGroup(
            IEnumerable<BackpackEntryData> entries,
            bool isCurrencyBundle)
        {
            Entries = entries.ToList();
            IsCurrencyBundle = isCurrencyBundle;
        }

        public IReadOnlyList<BackpackEntryData> Entries { get; }
        public BackpackEntryData Representative => Entries[0];
        public int Quantity => Entries.Count;
        public bool IsCurrencyBundle { get; }
    }

    public static class BackpackVisualGrouping
    {
        public static IReadOnlyList<BackpackVisualGroup> Build(
            BackpackData backpack,
            Func<string, bool> isCurrency)
        {
            if (backpack?.Entries == null)
                return Array.Empty<BackpackVisualGroup>();

            isCurrency ??= _ => false;
            return backpack.Entries
                .Where(entry => entry?.Card != null)
                .GroupBy(entry => GetVisualGroupKey(entry, isCurrency))
                .Select(group => new BackpackVisualGroup(
                    group.OrderBy(entry => entry.TableStackOrder)
                        .ThenBy(entry => entry.InstanceId, StringComparer.Ordinal)
                        .ThenBy(entry => entry.SlotIndex),
                    group.Key.StartsWith("currency:", StringComparison.Ordinal)))
                .OrderBy(group => group.Representative.SlotIndex)
                .ToList();
        }

        private static string GetVisualGroupKey(
            BackpackEntryData entry,
            Func<string, bool> isCurrency)
        {
            if (isCurrency(entry.Card.Id))
                return $"currency:{entry.Card.Id}";

            return string.IsNullOrWhiteSpace(entry.TableStackId)
                ? $"entry:{entry.InstanceId}"
                : $"stack:{entry.TableStackId}";
        }
    }
}
