using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public sealed class WorldQuestRegistry
    {
        private readonly Dictionary<string, WorldQuestDefinition> definitions;

        public WorldQuestRegistry(
            IEnumerable<WorldQuestDefinition> questDefinitions)
        {
            definitions = new Dictionary<string, WorldQuestDefinition>(
                StringComparer.Ordinal);
            foreach (WorldQuestDefinition definition in
                     questDefinitions ?? Enumerable.Empty<WorldQuestDefinition>())
            {
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!definitions.TryAdd(definition.Id, definition))
                {
                    throw new InvalidOperationException(
                        $"Duplicate world quest id '{definition.Id}'.");
                }
            }
        }

        public IReadOnlyCollection<WorldQuestDefinition> Definitions =>
            definitions.Values;

        public bool TryGet(
            string questId,
            out WorldQuestDefinition definition)
        {
            return definitions.TryGetValue(questId ?? string.Empty,
                out definition);
        }

        public WorldQuestDefinition GetRequired(string questId)
        {
            if (!TryGet(questId, out WorldQuestDefinition definition))
            {
                throw new KeyNotFoundException(
                    $"World quest definition '{questId}' was not found.");
            }

            return definition;
        }
    }
}
