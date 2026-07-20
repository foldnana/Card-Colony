using System;
using NUnit.Framework;
using CardColony.World;

namespace CardColony.Tests
{
    public class WorldStateStoreTests
    {
        [Test]
        public void FlagsAndValues_CanBeChangedAndQueried()
        {
            var state = new WorldStateStore();

            state.SetFlag("quest.river-village.helped", true);
            state.SetValue("relation.herbalist", 10);
            state.IncrementValue("relation.herbalist", 5);

            Assert.That(state.GetFlag("quest.river-village.helped"), Is.True);
            Assert.That(state.GetValue("relation.herbalist"), Is.EqualTo(15));
        }

        [Test]
        public void GetOrCreateLocation_ReturnsStableStateForLocationId()
        {
            var state = new WorldStateStore();

            LocationRuntimeState first = state.GetOrCreateLocation("whispering-forest");
            first.IsDiscovered = true;
            first.ExplorationProgress = 0.4f;
            first.MarkEncounterCleared("slime-clearing");

            LocationRuntimeState second = state.GetOrCreateLocation("whispering-forest");

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.IsDiscovered, Is.True);
            Assert.That(second.ExplorationProgress, Is.EqualTo(0.4f));
            Assert.That(second.IsEncounterCleared("slime-clearing"), Is.True);
        }

        [Test]
        public void CreateSnapshot_AndRestore_PreserveGlobalAndLocationState()
        {
            var original = new WorldStateStore();
            original.SetFlag("boss.shield-broken", true);
            original.SetValue("relation.blacksmith", 25);
            LocationRuntimeState location = original.GetOrCreateLocation("old-mine");
            location.IsDiscovered = true;
            location.ExplorationProgress = 0.75f;
            location.MarkEncounterCleared("mine-troll");

            WorldStateSnapshot snapshot = original.CreateSnapshot();
            var restored = new WorldStateStore(snapshot);

            Assert.That(restored.GetFlag("boss.shield-broken"), Is.True);
            Assert.That(restored.GetValue("relation.blacksmith"), Is.EqualTo(25));
            LocationRuntimeState restoredLocation = restored.GetOrCreateLocation("old-mine");
            Assert.That(restoredLocation.IsDiscovered, Is.True);
            Assert.That(restoredLocation.ExplorationProgress, Is.EqualTo(0.75f));
            Assert.That(restoredLocation.IsEncounterCleared("mine-troll"), Is.True);
        }

        [Test]
        public void GetOrCreateLocation_WhenIdIsEmpty_Throws()
        {
            var state = new WorldStateStore();

            Assert.Throws<ArgumentException>(() => state.GetOrCreateLocation(""));
        }

        [Test]
        public void ExplorationProgress_WhenValueIsNotFinite_Throws()
        {
            var location = new LocationRuntimeState("old-mine");

            Assert.Throws<ArgumentOutOfRangeException>(
                () => location.ExplorationProgress = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => location.ExplorationProgress = float.PositiveInfinity);
        }
    }
}
