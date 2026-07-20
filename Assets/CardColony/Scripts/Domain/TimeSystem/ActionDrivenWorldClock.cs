using System;
using System.Collections.Generic;

namespace CardColony.TimeSystem
{
    public enum WorldClockSpeed
    {
        Normal = 1,
        Fast = 4
    }

    /// <summary>
    /// Continuous world clock that advances only while an action is active or the player is waiting.
    /// This class has no Unity dependencies so save and simulation logic can be tested in isolation.
    /// </summary>
    public sealed class ActionDrivenWorldClock
    {
        public const double MinutesPerDay = 1440d;

        private readonly HashSet<string> activeActionIds = new HashSet<string>();
        private readonly float gameMinutesPerRealSecond;
        private WorldClockSpeed speed = WorldClockSpeed.Normal;

        public double TotalMinutes { get; private set; }
        public int DayNumber => (int)Math.Floor(TotalMinutes / MinutesPerDay) + 1;
        public double MinuteOfDay => TotalMinutes % MinutesPerDay;
        public int ActiveActionCount => activeActionIds.Count;
        public bool IsWaiting { get; set; }
        public bool IsPaused { get; set; }
        public WorldClockSpeed Speed
        {
            get => speed;
            set
            {
                if (value != WorldClockSpeed.Normal && value != WorldClockSpeed.Fast)
                    throw new ArgumentOutOfRangeException(nameof(value));

                speed = value;
            }
        }
        public bool ShouldAdvance => !IsPaused && (IsWaiting || ActiveActionCount > 0);

        public ActionDrivenWorldClock(float gameMinutesPerRealSecond, double initialTotalMinutes = 0d)
        {
            if (gameMinutesPerRealSecond <= 0f
                || float.IsNaN(gameMinutesPerRealSecond)
                || float.IsInfinity(gameMinutesPerRealSecond))
                throw new ArgumentOutOfRangeException(nameof(gameMinutesPerRealSecond));
            if (initialTotalMinutes < 0d
                || double.IsNaN(initialTotalMinutes)
                || double.IsInfinity(initialTotalMinutes))
                throw new ArgumentOutOfRangeException(nameof(initialTotalMinutes));

            this.gameMinutesPerRealSecond = gameMinutesPerRealSecond;
            TotalMinutes = initialTotalMinutes;
        }

        public IDisposable BeginAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException("Action ID cannot be empty.", nameof(actionId));
            if (!activeActionIds.Add(actionId))
                throw new InvalidOperationException($"Action '{actionId}' is already active.");

            return new ActionHandle(this, actionId);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (unscaledDeltaSeconds < 0f
                || float.IsNaN(unscaledDeltaSeconds)
                || float.IsInfinity(unscaledDeltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            if (!ShouldAdvance || unscaledDeltaSeconds == 0f)
                return;

            double updatedMinutes = TotalMinutes
                + (double)unscaledDeltaSeconds * gameMinutesPerRealSecond * (int)Speed;
            if (double.IsInfinity(updatedMinutes))
                throw new OverflowException("World time exceeded the supported numeric range.");

            TotalMinutes = updatedMinutes;
        }

        public void Restore(double totalMinutes)
        {
            if (totalMinutes < 0d || double.IsNaN(totalMinutes) || double.IsInfinity(totalMinutes))
                throw new ArgumentOutOfRangeException(nameof(totalMinutes));

            TotalMinutes = totalMinutes;
        }

        private void EndAction(string actionId)
        {
            activeActionIds.Remove(actionId);
        }

        private sealed class ActionHandle : IDisposable
        {
            private ActionDrivenWorldClock owner;
            private readonly string actionId;

            public ActionHandle(ActionDrivenWorldClock owner, string actionId)
            {
                this.owner = owner;
                this.actionId = actionId;
            }

            public void Dispose()
            {
                if (owner == null)
                    return;

                owner.EndAction(actionId);
                owner = null;
            }
        }
    }
}
