using System;
using CardColony.Inventory;
using CardColony.World;

namespace CardColony.Gameplay
{
    [Serializable]
    public sealed class RunSnapshot
    {
        public int SchemaVersion = PlayableLoopSession.CurrentSchemaVersion;
        public WorldClockSnapshot Clock = new WorldClockSnapshot();
        public WorldStateSnapshot World = new WorldStateSnapshot();
        public CardContainerSnapshot PlayerInventory = new CardContainerSnapshot();
        public ItemCardSnapshot HeldCard;
        public LoopActionSnapshot ActiveAction;
    }

    [Serializable]
    public sealed class WorldClockSnapshot
    {
        public double TotalMinutes;
        public int Speed = 1;
        public bool IsPaused;
        public bool IsWaiting;
    }

    [Serializable]
    public sealed class LoopActionSnapshot
    {
        public int Type;
        public string ActionId;
        public double DurationWorldMinutes;
        public double ElapsedWorldMinutes;
    }
}
