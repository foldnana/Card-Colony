using System;
using System.Linq;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class ProtagonistRunChronicle
    {
        public int SlotNumber;
        public string ProtagonistPersistentId;
        public int ProtagonistLevel;
        public int WorldDay;
        public string LocationId;
        public int PartySize;
        public int BackpackItemCount;
        public string DeathCause;
        public DateTime EndedAt;
    }

    public static class ProtagonistChronicleService
    {
        public static ProtagonistRunChronicle Create(
            GameData gameData,
            string deathCause)
        {
            if (gameData == null)
                return null;

            CardData protagonist = gameData.GetProtagonistData();
            return new ProtagonistRunChronicle
            {
                SlotNumber = gameData.SlotNumber,
                ProtagonistPersistentId =
                    gameData.ProtagonistPersistentId,
                ProtagonistLevel =
                    System.Math.Max(1, protagonist?.Level ?? 1),
                WorldDay = gameData.GetWorldDay(),
                LocationId = gameData.ActiveLocationId,
                PartySize = gameData.PartyMembers?.Count(
                    member => member != null) ?? 0,
                BackpackItemCount =
                    gameData.EnsureBackpack().Entries.Count,
                DeathCause = string.IsNullOrWhiteSpace(deathCause)
                    ? "未知原因"
                    : deathCause,
                EndedAt = DateTime.Now
            };
        }

        public static string GetArchiveFileName(
            ProtagonistRunChronicle chronicle)
        {
            if (chronicle == null)
                return null;

            return $"Chronicles/Run_{chronicle.SlotNumber:D3}_" +
                $"{chronicle.EndedAt:yyyyMMdd_HHmmss}";
        }
    }
}
