using System;
using Newtonsoft.Json;
using CardColony.Gameplay;

namespace CardColony.UnityIntegration.Save
{
    public static class RunSnapshotJsonSerializer
    {
        public static string Serialize(RunSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            return JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        }

        public static RunSnapshot Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Save JSON cannot be empty.", nameof(json));

            RunSnapshot snapshot = JsonConvert.DeserializeObject<RunSnapshot>(json);
            return snapshot ?? throw new JsonSerializationException("Save JSON produced no run snapshot.");
        }
    }
}
