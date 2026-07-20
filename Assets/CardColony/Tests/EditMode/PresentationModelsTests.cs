using NUnit.Framework;
using CardColony.Inventory;
using CardColony.Presentation;
using CardColony.TimeSystem;

namespace CardColony.Tests
{
    public class PresentationModelsTests
    {
        [Test]
        public void WorldClockTextFormatter_FormatsDayAndTwentyFourHourTime()
        {
            var clock = new ActionDrivenWorldClock(1f, 1505d);

            string text = WorldClockTextFormatter.Format(clock);

            Assert.That(text, Is.EqualTo("第2天\n01:05"));
        }

        [Test]
        public void BackpackInteraction_TakeWholeCardAndPutBack_RestoresInventory()
        {
            var session = new CardColony.Gameplay.PlayableLoopSession(1f, 0d, 4, 100f);
            CardContainer container = session.PlayerInventory;
            container.Add(new ItemCardStack("herb", 3, 10, 0.1f, 0, "forest", "herb-card"));
            var interaction = new BackpackInteraction(session);

            Assert.That(interaction.TryTake("herb-card", 3), Is.True);
            Assert.That(interaction.HeldCard.Quantity, Is.EqualTo(3));
            Assert.That(container.GetQuantity("herb"), Is.Zero);

            Assert.That(interaction.TryPutBack(), Is.True);
            Assert.That(interaction.HeldCard, Is.Null);
            Assert.That(container.GetQuantity("herb"), Is.EqualTo(3));
        }

        [Test]
        public void BackpackInteraction_SplitOne_LeavesRemainderAndHoldsOneCard()
        {
            var session = new CardColony.Gameplay.PlayableLoopSession(1f, 0d, 4, 100f);
            CardContainer container = session.PlayerInventory;
            container.Add(new ItemCardStack("herb", 3, 10, 0.1f, 0, "forest", "herb-card"));
            var interaction = new BackpackInteraction(session);

            bool split = interaction.TrySplitOne("herb-card");

            Assert.That(split, Is.True);
            Assert.That(interaction.HeldCard.Quantity, Is.EqualTo(1));
            Assert.That(container.Cards[0].Quantity, Is.EqualTo(2));
        }

        [Test]
        public void BackpackInteraction_WhenAlreadyHoldingCard_RejectsAnotherTake()
        {
            var session = new CardColony.Gameplay.PlayableLoopSession(1f, 0d, 4, 100f);
            CardContainer container = session.PlayerInventory;
            container.Add(new ItemCardStack("herb", 3, 10, 0.1f, 0, "forest", "herb-card"));
            container.Add(new ItemCardStack("potion", 1, 10, 0.2f, 0, "crafted", "potion-card"));
            var interaction = new BackpackInteraction(session);
            interaction.TrySplitOne("herb-card");

            bool tookAnother = interaction.TryTake("potion-card", 1);

            Assert.That(tookAnother, Is.False);
            Assert.That(interaction.HeldCard.ItemId, Is.EqualTo("herb"));
        }
    }
}
