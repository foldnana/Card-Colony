using System;

namespace CardColony.Gameplay
{
    public enum LoopActionType
    {
        ExploreWhisperingForest = 1,
        GatherHerbs = 2,
        BrewPotion = 3
    }

    public sealed class LoopActionState
    {
        public LoopActionType Type { get; }
        public string ActionId { get; }
        public double DurationWorldMinutes { get; }
        public double ElapsedWorldMinutes { get; private set; }
        public double Progress01 => Math.Min(1d, ElapsedWorldMinutes / DurationWorldMinutes);
        public bool IsComplete => ElapsedWorldMinutes >= DurationWorldMinutes;

        internal LoopActionState(
            LoopActionType type,
            string actionId,
            double durationWorldMinutes,
            double elapsedWorldMinutes = 0d)
        {
            if (!Enum.IsDefined(typeof(LoopActionType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException("Action ID cannot be empty.", nameof(actionId));
            if (durationWorldMinutes <= 0d
                || double.IsNaN(durationWorldMinutes)
                || double.IsInfinity(durationWorldMinutes))
                throw new ArgumentOutOfRangeException(nameof(durationWorldMinutes));
            if (elapsedWorldMinutes < 0d
                || elapsedWorldMinutes > durationWorldMinutes
                || double.IsNaN(elapsedWorldMinutes)
                || double.IsInfinity(elapsedWorldMinutes))
                throw new ArgumentOutOfRangeException(nameof(elapsedWorldMinutes));

            Type = type;
            ActionId = actionId;
            DurationWorldMinutes = durationWorldMinutes;
            ElapsedWorldMinutes = elapsedWorldMinutes;
        }

        internal void Advance(double worldMinutes)
        {
            if (worldMinutes < 0d || double.IsNaN(worldMinutes) || double.IsInfinity(worldMinutes))
                throw new ArgumentOutOfRangeException(nameof(worldMinutes));

            ElapsedWorldMinutes = Math.Min(DurationWorldMinutes, ElapsedWorldMinutes + worldMinutes);
        }

        internal LoopActionSnapshot CreateSnapshot()
        {
            return new LoopActionSnapshot
            {
                Type = (int)Type,
                ActionId = ActionId,
                DurationWorldMinutes = DurationWorldMinutes,
                ElapsedWorldMinutes = ElapsedWorldMinutes
            };
        }
    }
}
