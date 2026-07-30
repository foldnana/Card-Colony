using System.Linq;

namespace CryingSnow.StackCraft
{
    public readonly struct ProtagonistRetreatResult
    {
        public bool Succeeded { get; }
        public int HoursLost { get; }
        public int CoinsLost { get; }

        public ProtagonistRetreatResult(
            bool succeeded,
            int hoursLost,
            int coinsLost)
        {
            Succeeded = succeeded;
            HoursLost = hoursLost;
            CoinsLost = coinsLost;
        }
    }

    public static class ProtagonistRetreatService
    {
        public const int DefaultHoursLost = 6;
        public const int DefaultMaximumCoinsLost = 3;
        public const string DefaultCoinDefinitionId =
            "4bda315463bf4b73b63f1d232fb522e4";

        public static ProtagonistRetreatResult Apply(
            GameData gameData,
            string coinDefinitionId,
            int hoursLost,
            int maximumCoinsLost)
        {
            CardData protagonist = gameData?.GetProtagonistData();
            if (protagonist == null || !protagonist.IsDowned)
                return new ProtagonistRetreatResult(false, 0, 0);

            hoursLost = System.Math.Max(0, hoursLost);
            maximumCoinsLost = System.Math.Max(0, maximumCoinsLost);
            BackpackData backpack = gameData.EnsureBackpack();
            string[] coinEntryIds = backpack.Entries
                .Where(entry =>
                    entry?.Card != null &&
                    entry.Card.Id == coinDefinitionId)
                .Take(maximumCoinsLost)
                .Select(entry => entry.InstanceId)
                .ToArray();
            foreach (string entryId in coinEntryIds)
                backpack.TryRemove(entryId, out _);

            gameData.WorldElapsedHours = System.Math.Max(
                0L,
                gameData.WorldElapsedHours) + hoursLost;
            int resultingDay =
                (int)(gameData.WorldElapsedHours / 24L) + 1;
            gameData.WorldDay = System.Math.Max(
                gameData.GetWorldDay(),
                resultingDay);
            ProtagonistRecoveryService.Revive(
                protagonist,
                restoredHealth: 1,
                restoredEnergy: 0);

            return new ProtagonistRetreatResult(
                true,
                hoursLost,
                coinEntryIds.Length);
        }
    }
}
