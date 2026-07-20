using System;
using NUnit.Framework;
using CardColony.TimeSystem;

namespace CardColony.Tests
{
    public class ActionDrivenWorldClockTests
    {
        [Test]
        public void Tick_WhenIdle_DoesNotAdvance()
        {
            var clock = new ActionDrivenWorldClock(2f, 360d);

            clock.Tick(10f);

            Assert.That(clock.TotalMinutes, Is.EqualTo(360d));
        }

        [Test]
        public void Tick_WhileActionIsActive_AdvancesUntilHandleIsDisposed()
        {
            var clock = new ActionDrivenWorldClock(2f, 360d);

            using (clock.BeginAction("gather-herbs"))
            {
                clock.Tick(10f);
                Assert.That(clock.ActiveActionCount, Is.EqualTo(1));
                Assert.That(clock.TotalMinutes, Is.EqualTo(380d));
            }

            clock.Tick(10f);
            Assert.That(clock.ActiveActionCount, Is.Zero);
            Assert.That(clock.TotalMinutes, Is.EqualTo(380d));
        }

        [Test]
        public void Tick_WhenWaiting_AdvancesWithoutAnActiveAction()
        {
            var clock = new ActionDrivenWorldClock(2f, 360d)
            {
                IsWaiting = true
            };

            clock.Tick(10f);

            Assert.That(clock.TotalMinutes, Is.EqualTo(380d));
        }

        [Test]
        public void Tick_WhenPaused_DoesNotAdvanceActionsOrWaiting()
        {
            var clock = new ActionDrivenWorldClock(2f, 360d)
            {
                IsWaiting = true,
                IsPaused = true
            };

            using (clock.BeginAction("travel"))
                clock.Tick(10f);

            Assert.That(clock.TotalMinutes, Is.EqualTo(360d));
        }

        [Test]
        public void Tick_AtFastSpeed_UsesConfiguredMultiplier()
        {
            var clock = new ActionDrivenWorldClock(2f, 360d)
            {
                IsWaiting = true,
                Speed = WorldClockSpeed.Fast
            };

            clock.Tick(10f);

            Assert.That(clock.TotalMinutes, Is.EqualTo(440d));
        }

        [Test]
        public void Tick_WhenCrossingMidnight_UpdatesDayAndMinuteOfDay()
        {
            var clock = new ActionDrivenWorldClock(2f, 1430d)
            {
                IsWaiting = true
            };

            clock.Tick(10f);

            Assert.That(clock.DayNumber, Is.EqualTo(2));
            Assert.That(clock.MinuteOfDay, Is.EqualTo(10d));
        }

        [Test]
        public void Tick_WithMultipleActiveActions_AdvancesOnlyOnce()
        {
            var clock = new ActionDrivenWorldClock(2f, 360d);

            using (clock.BeginAction("travel"))
            using (clock.BeginAction("escort-event"))
                clock.Tick(10f);

            Assert.That(clock.TotalMinutes, Is.EqualTo(380d));
        }

        [Test]
        public void NonFiniteInputs_AreRejectedBeforeTheyCorruptTime()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ActionDrivenWorldClock(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ActionDrivenWorldClock(1f, double.PositiveInfinity));

            var clock = new ActionDrivenWorldClock(1f) { IsWaiting = true };
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Restore(double.PositiveInfinity));
            Assert.That(clock.TotalMinutes, Is.Zero);
        }

        [Test]
        public void Speed_WhenValueIsUndefined_Throws()
        {
            var clock = new ActionDrivenWorldClock(1f);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => clock.Speed = (WorldClockSpeed)99);
        }
    }
}
