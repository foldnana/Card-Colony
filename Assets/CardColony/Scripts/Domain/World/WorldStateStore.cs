using System;
using System.Collections.Generic;
using System.Linq;

namespace CardColony.World
{
    [Serializable]
    public sealed class WorldStateStore
    {
        public const int CurrentSchemaVersion = 1;

        private readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();
        private readonly Dictionary<string, int> values = new Dictionary<string, int>();
        private readonly Dictionary<string, LocationRuntimeState> locations =
            new Dictionary<string, LocationRuntimeState>();

        public WorldStateStore()
        {
        }

        public WorldStateStore(WorldStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SchemaVersion > CurrentSchemaVersion)
                throw new NotSupportedException(
                    $"World state schema {snapshot.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}.");

            foreach (WorldFlagSnapshot entry in snapshot.Flags ?? new List<WorldFlagSnapshot>())
                flags[ValidateId(entry.Id, nameof(entry.Id))] = entry.Value;
            foreach (WorldValueSnapshot entry in snapshot.Values ?? new List<WorldValueSnapshot>())
                values[ValidateId(entry.Id, nameof(entry.Id))] = entry.Value;
            foreach (LocationStateSnapshot entry in snapshot.Locations ?? new List<LocationStateSnapshot>())
            {
                LocationRuntimeState location = LocationRuntimeState.FromSnapshot(entry);
                locations[location.LocationId] = location;
            }
        }

        public void SetFlag(string id, bool value)
        {
            flags[ValidateId(id, nameof(id))] = value;
        }

        public bool GetFlag(string id)
        {
            return flags.TryGetValue(ValidateId(id, nameof(id)), out bool value) && value;
        }

        public void SetValue(string id, int value)
        {
            values[ValidateId(id, nameof(id))] = value;
        }

        public int GetValue(string id)
        {
            return values.TryGetValue(ValidateId(id, nameof(id)), out int value) ? value : 0;
        }

        public int IncrementValue(string id, int amount)
        {
            string validId = ValidateId(id, nameof(id));
            int updated = checked(GetValue(validId) + amount);
            values[validId] = updated;
            return updated;
        }

        public LocationRuntimeState GetOrCreateLocation(string locationId)
        {
            string validId = ValidateId(locationId, nameof(locationId));
            if (!locations.TryGetValue(validId, out LocationRuntimeState location))
            {
                location = new LocationRuntimeState(validId);
                locations.Add(validId, location);
            }

            return location;
        }

        public WorldStateSnapshot CreateSnapshot()
        {
            return new WorldStateSnapshot
            {
                SchemaVersion = CurrentSchemaVersion,
                Flags = flags.OrderBy(pair => pair.Key)
                    .Select(pair => new WorldFlagSnapshot(pair.Key, pair.Value))
                    .ToList(),
                Values = values.OrderBy(pair => pair.Key)
                    .Select(pair => new WorldValueSnapshot(pair.Key, pair.Value))
                    .ToList(),
                Locations = locations.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value.CreateSnapshot())
                    .ToList()
            };
        }

        private static string ValidateId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID cannot be empty.", parameterName);

            return id;
        }
    }

    [Serializable]
    public sealed class LocationRuntimeState
    {
        private readonly HashSet<string> clearedEncounterIds = new HashSet<string>();
        private float explorationProgress;

        public string LocationId { get; }
        public bool IsDiscovered { get; set; }
        public float ExplorationProgress
        {
            get => explorationProgress;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value));

                explorationProgress = Math.Max(0f, Math.Min(1f, value));
            }
        }

        public LocationRuntimeState(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                throw new ArgumentException("Location ID cannot be empty.", nameof(locationId));

            LocationId = locationId;
        }

        public void MarkEncounterCleared(string encounterId)
        {
            clearedEncounterIds.Add(ValidateEncounterId(encounterId));
        }

        public bool IsEncounterCleared(string encounterId)
        {
            return clearedEncounterIds.Contains(ValidateEncounterId(encounterId));
        }

        internal LocationStateSnapshot CreateSnapshot()
        {
            return new LocationStateSnapshot
            {
                LocationId = LocationId,
                IsDiscovered = IsDiscovered,
                ExplorationProgress = ExplorationProgress,
                ClearedEncounterIds = clearedEncounterIds.OrderBy(id => id).ToList()
            };
        }

        internal static LocationRuntimeState FromSnapshot(LocationStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var state = new LocationRuntimeState(snapshot.LocationId)
            {
                IsDiscovered = snapshot.IsDiscovered,
                ExplorationProgress = snapshot.ExplorationProgress
            };

            foreach (string encounterId in snapshot.ClearedEncounterIds ?? new List<string>())
                state.MarkEncounterCleared(encounterId);

            return state;
        }

        private static string ValidateEncounterId(string encounterId)
        {
            if (string.IsNullOrWhiteSpace(encounterId))
                throw new ArgumentException("Encounter ID cannot be empty.", nameof(encounterId));

            return encounterId;
        }
    }

    [Serializable]
    public sealed class WorldStateSnapshot
    {
        public int SchemaVersion = WorldStateStore.CurrentSchemaVersion;
        public List<WorldFlagSnapshot> Flags = new List<WorldFlagSnapshot>();
        public List<WorldValueSnapshot> Values = new List<WorldValueSnapshot>();
        public List<LocationStateSnapshot> Locations = new List<LocationStateSnapshot>();
    }

    [Serializable]
    public sealed class WorldFlagSnapshot
    {
        public string Id;
        public bool Value;

        public WorldFlagSnapshot()
        {
        }

        public WorldFlagSnapshot(string id, bool value)
        {
            Id = id;
            Value = value;
        }
    }

    [Serializable]
    public sealed class WorldValueSnapshot
    {
        public string Id;
        public int Value;

        public WorldValueSnapshot()
        {
        }

        public WorldValueSnapshot(string id, int value)
        {
            Id = id;
            Value = value;
        }
    }

    [Serializable]
    public sealed class LocationStateSnapshot
    {
        public string LocationId;
        public bool IsDiscovered;
        public float ExplorationProgress;
        public List<string> ClearedEncounterIds = new List<string>();
    }
}
