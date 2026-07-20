using NUnit.Framework;
using CardColony.Gameplay;
using CardColony.TimeSystem;

namespace CardColony.Tests
{
    public class PlayableLoopSessionTests
    {
        [Test]
        public void ExploreGatherAndBrew_CompletesThePlayableLoop()
        {
            var session = new PlayableLoopSession(1f, 0d, 8, 100f);

            Assert.That(session.StartExploreWhisperingForest().Succeeded, Is.True);
            session.Tick(PlayableLoopSession.ExploreDurationMinutes);

            Assert.That(session.World.GetOrCreateLocation(PlayableLoopSession.ForestLocationId).IsDiscovered, Is.True);
            Assert.That(session.ActiveAction, Is.Null);

            Assert.That(session.StartGatherHerbs().Succeeded, Is.True);
            session.Tick(PlayableLoopSession.GatherDurationMinutes);

            Assert.That(session.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(3));

            Assert.That(session.StartBrewPotion().Succeeded, Is.True);
            Assert.That(session.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(1));
            session.Tick(PlayableLoopSession.BrewDurationMinutes);

            Assert.That(session.PlayerInventory.GetQuantity(PlayableLoopSession.PotionItemId), Is.EqualTo(1));
            Assert.That(session.Clock.TotalMinutes, Is.EqualTo(65d));
        }

        [Test]
        public void StartGatherHerbs_BeforeForestIsDiscovered_FailsWithoutStartingTime()
        {
            var session = new PlayableLoopSession(1f, 0d, 8, 100f);

            LoopCommandResult result = session.StartGatherHerbs();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(session.ActiveAction, Is.Null);
            Assert.That(session.Clock.ShouldAdvance, Is.False);
        }

        [Test]
        public void CreateSnapshot_AndRestore_ResumeActiveCraftWithoutConsumingTwice()
        {
            var session = new PlayableLoopSession(1f, 0d, 8, 100f);
            session.World.GetOrCreateLocation(PlayableLoopSession.ForestLocationId).IsDiscovered = true;
            session.PlayerInventory.Add(PlayableLoopSession.CreateHerbCard(2));
            session.StartBrewPotion();
            session.Tick(5f);

            RunSnapshot snapshot = session.CreateSnapshot();
            PlayableLoopSession restored = PlayableLoopSession.Restore(snapshot, 1f);

            Assert.That(restored.ActiveAction, Is.Not.Null);
            Assert.That(restored.ActiveAction.Type, Is.EqualTo(LoopActionType.BrewPotion));
            Assert.That(restored.ActiveAction.ElapsedWorldMinutes, Is.EqualTo(5d));
            Assert.That(restored.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.Zero);

            restored.Tick(10f);

            Assert.That(restored.ActiveAction, Is.Null);
            Assert.That(restored.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.Zero);
            Assert.That(restored.PlayerInventory.GetQuantity(PlayableLoopSession.PotionItemId), Is.EqualTo(1));
        }

        [Test]
        public void Snapshot_PreservesClockControlsWorldAndInventory()
        {
            var session = new PlayableLoopSession(2f, 360d, 8, 100f);
            session.Clock.Speed = WorldClockSpeed.Fast;
            session.Clock.IsWaiting = true;
            session.Clock.IsPaused = true;
            session.World.SetFlag("quest.herbalist.met", true);
            session.PlayerInventory.Add(PlayableLoopSession.CreateHerbCard(1));

            PlayableLoopSession restored = PlayableLoopSession.Restore(session.CreateSnapshot(), 2f);

            Assert.That(restored.Clock.TotalMinutes, Is.EqualTo(360d));
            Assert.That(restored.Clock.Speed, Is.EqualTo(WorldClockSpeed.Fast));
            Assert.That(restored.Clock.IsWaiting, Is.True);
            Assert.That(restored.Clock.IsPaused, Is.True);
            Assert.That(restored.World.GetFlag("quest.herbalist.met"), Is.True);
            Assert.That(restored.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(1));
        }

        [Test]
        public void Tick_AtFastSpeed_CompletesActionUsingWorldMinutes()
        {
            var session = new PlayableLoopSession(1f, 0d, 8, 100f);
            session.Clock.Speed = WorldClockSpeed.Fast;
            session.StartExploreWhisperingForest();

            session.Tick((float)PlayableLoopSession.ExploreDurationMinutes / 4f);

            Assert.That(session.ActiveAction, Is.Null);
            Assert.That(session.Clock.TotalMinutes, Is.EqualTo(PlayableLoopSession.ExploreDurationMinutes));
        }

        [Test]
        public void GatherReward_WhenInventoryCannotFitAll_RejectsActionWithoutChangingInventory()
        {
            var session = new PlayableLoopSession(1f, 0d, 1, 100f);
            session.World.GetOrCreateLocation(PlayableLoopSession.ForestLocationId).IsDiscovered = true;
            session.PlayerInventory.Add(
                new CardColony.Inventory.ItemCardStack(
                    PlayableLoopSession.HerbItemId, 19, 20, 0.1f, 0, "forest", "existing"));

            LoopCommandResult result = session.StartGatherHerbs();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(session.ActiveAction, Is.Null);
            Assert.That(session.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(19));
        }

        [Test]
        public void Brew_WhenIngredientsWouldLeaveNoPotionSlot_RejectsWithoutConsuming()
        {
            var session = new PlayableLoopSession(1f, 0d, 1, 100f);
            session.PlayerInventory.Add(
                new CardColony.Inventory.ItemCardStack(
                    PlayableLoopSession.HerbItemId, 3, 20, 0.1f, 0, "forest", "existing"));

            LoopCommandResult result = session.StartBrewPotion();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(session.ActiveAction, Is.Null);
            Assert.That(session.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(3));
        }

        [Test]
        public void InventoryInteraction_DuringAction_IsRejected()
        {
            var session = new PlayableLoopSession(1f, 0d, 2, 100f);
            session.World.GetOrCreateLocation(PlayableLoopSession.ForestLocationId).IsDiscovered = true;
            session.PlayerInventory.Add(
                new CardColony.Inventory.ItemCardStack(
                    PlayableLoopSession.HerbItemId, 3, 20, 0.1f, 0, "forest", "existing"));
            Assert.That(session.StartGatherHerbs().Succeeded, Is.True);

            Assert.That(session.TryTakeCard("existing", 3), Is.False);
            Assert.That(session.TrySplitOne("existing"), Is.False);
            Assert.That(session.HeldCard, Is.Null);
            Assert.That(session.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(3));
        }

        [Test]
        public void Snapshot_PreservesHeldCardAndCanReturnItAfterRestore()
        {
            var session = new PlayableLoopSession(1f, 0d, 8, 100f);
            session.PlayerInventory.Add(
                new CardColony.Inventory.ItemCardStack(
                    PlayableLoopSession.HerbItemId, 3, 20, 0.1f, 0, "forest", "herb-card"));

            Assert.That(session.TrySplitOne("herb-card"), Is.True);
            PlayableLoopSession restored = PlayableLoopSession.Restore(session.CreateSnapshot(), 1f);

            Assert.That(restored.HeldCard, Is.Not.Null);
            Assert.That(restored.HeldCard.ItemId, Is.EqualTo(PlayableLoopSession.HerbItemId));
            Assert.That(restored.HeldCard.Quantity, Is.EqualTo(1));
            Assert.That(restored.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(2));
            Assert.That(restored.TryPutHeldCardBack(), Is.True);
            Assert.That(restored.HeldCard, Is.Null);
            Assert.That(restored.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(3));
        }
    }
}
