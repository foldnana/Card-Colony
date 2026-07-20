using System;
using NUnit.Framework;
using CardColony.Inventory;

namespace CardColony.Tests
{
    public class CardContainerTests
    {
        [Test]
        public void Add_SameItemQualityAndBatch_MergesIntoOneVisibleCard()
        {
            var container = new CardContainer(4, 100f);

            container.Add(new ItemCardStack("wood", 3, 10, 1f, 0, "normal", "wood-a"));
            var result = container.Add(new ItemCardStack("wood", 4, 10, 1f, 0, "normal", "wood-b"));

            Assert.That(result.IsComplete, Is.True);
            Assert.That(container.Cards, Has.Count.EqualTo(1));
            Assert.That(container.Cards[0].Quantity, Is.EqualTo(7));
        }

        [Test]
        public void Add_DifferentQuality_CreatesAnotherVisibleCard()
        {
            var container = new CardContainer(4, 100f);

            container.Add(new ItemCardStack("potion", 2, 10, 0.5f, 1, "normal", "potion-a"));
            container.Add(new ItemCardStack("potion", 2, 10, 0.5f, 2, "normal", "potion-b"));

            Assert.That(container.Cards, Has.Count.EqualTo(2));
        }

        [Test]
        public void Add_WhenSlotsAreFull_ReportsRejectedQuantity()
        {
            var container = new CardContainer(1, 100f);

            container.Add(new ItemCardStack("wood", 10, 10, 1f, 0, "normal", "wood-a"));
            var result = container.Add(new ItemCardStack("stone", 3, 10, 1f, 0, "normal", "stone-a"));

            Assert.That(result.AcceptedQuantity, Is.Zero);
            Assert.That(result.RejectedQuantity, Is.EqualTo(3));
            Assert.That(container.Cards, Has.Count.EqualTo(1));
        }

        [Test]
        public void Add_WhenWeightIsLimited_AcceptsOnlyWholeUnitsThatFit()
        {
            var container = new CardContainer(4, 5f);

            var result = container.Add(new ItemCardStack("ore", 4, 10, 2f, 0, "normal", "ore-a"));

            Assert.That(result.AcceptedQuantity, Is.EqualTo(2));
            Assert.That(result.RejectedQuantity, Is.EqualTo(2));
            Assert.That(container.CurrentWeight, Is.EqualTo(4f));
            Assert.That(container.Cards[0].Quantity, Is.EqualTo(2));
        }

        [Test]
        public void TryRemove_PartOfStack_ReturnsDetachedCardAndKeepsRemainder()
        {
            var container = new CardContainer(4, 100f);
            container.Add(new ItemCardStack("herb", 5, 10, 0.1f, 0, "normal", "herb-a"));

            bool removed = container.TryRemove("herb-a", 2, out ItemCardStack detached);

            Assert.That(removed, Is.True);
            Assert.That(detached.Quantity, Is.EqualTo(2));
            Assert.That(detached.InstanceId, Is.Not.EqualTo("herb-a"));
            Assert.That(container.Cards[0].Quantity, Is.EqualTo(3));
        }

        [Test]
        public void Constructor_WhenSlotCapacityIsInvalid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CardContainer(0, 10f));
        }

        [Test]
        public void Add_WhenUnitWeightsAreOnlyApproximatelyEqual_DoesNotMergeOrOverflow()
        {
            var container = new CardContainer(1, 100001f);
            container.Add(new ItemCardStack("ore", 1, 100000, 1.00009f, 0, "normal", "ore-a"));

            InventoryAddResult result = container.Add(
                new ItemCardStack("ore", 99999, 100000, 1f, 0, "normal", "ore-b"));

            Assert.That(result.AcceptedQuantity, Is.Zero);
            Assert.That(result.RejectedQuantity, Is.EqualTo(99999));
            Assert.That(container.Cards, Has.Count.EqualTo(1));
            Assert.That(container.CurrentWeight, Is.LessThanOrEqualTo(container.MaxWeight));
        }

        [Test]
        public void Constructors_WhenWeightIsNotFinite_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CardContainer(1, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ItemCardStack("ore", 1, 10, float.PositiveInfinity));
        }

        [Test]
        public void Add_WhenUnitWeightIsSmallButPositive_StillEnforcesWeightLimit()
        {
            var container = new CardContainer(1, 1f);

            InventoryAddResult result = container.Add(
                new ItemCardStack("dust", 100000, 100000, 0.00005f));

            Assert.That(result.AcceptedQuantity, Is.GreaterThan(0));
            Assert.That(result.RejectedQuantity, Is.GreaterThan(0));
            Assert.That(container.CurrentWeight, Is.LessThanOrEqualTo(container.MaxWeight));
        }

        [Test]
        public void Add_WhenWeightCapacityInUnitsExceedsIntegerRange_AcceptsRequestedStack()
        {
            var container = new CardContainer(1, 1000000f);

            InventoryAddResult result = container.Add(
                new ItemCardStack("dust", 100000, 100000, 0.00005f));

            Assert.That(result.IsComplete, Is.True);
            Assert.That(container.Cards, Has.Count.EqualTo(1));
            Assert.That(container.Cards[0].Quantity, Is.EqualTo(100000));
        }

        [Test]
        public void TryConsume_WhenQuantitySpansCards_RemovesRequiredAmount()
        {
            var container = new CardContainer(4, 100f);
            container.Add(new ItemCardStack("herb", 2, 10, 0.1f, 0, "wild"));
            container.Add(new ItemCardStack("herb", 2, 10, 0.1f, 1, "wild"));

            bool consumed = container.TryConsume("herb", 3);

            Assert.That(consumed, Is.True);
            Assert.That(container.GetQuantity("herb"), Is.EqualTo(1));
        }

        [Test]
        public void TryConsume_WhenQuantityIsInsufficient_DoesNotMutateContainer()
        {
            var container = new CardContainer(4, 100f);
            container.Add(new ItemCardStack("herb", 2, 10, 0.1f));

            bool consumed = container.TryConsume("herb", 3);

            Assert.That(consumed, Is.False);
            Assert.That(container.GetQuantity("herb"), Is.EqualTo(2));
        }

        [Test]
        public void CreateSnapshot_AndRestore_PreserveVisibleItemCards()
        {
            var container = new CardContainer(4, 50f);
            container.Add(new ItemCardStack("herb", 3, 10, 0.1f, 2, "forest", "herb-card"));

            CardContainerSnapshot snapshot = container.CreateSnapshot();
            CardContainer restored = CardContainer.FromSnapshot(snapshot);

            Assert.That(restored.SlotCapacity, Is.EqualTo(4));
            Assert.That(restored.MaxWeight, Is.EqualTo(50f));
            Assert.That(restored.Cards, Has.Count.EqualTo(1));
            Assert.That(restored.Cards[0].ItemId, Is.EqualTo("herb"));
            Assert.That(restored.Cards[0].Quantity, Is.EqualTo(3));
            Assert.That(restored.Cards[0].Quality, Is.EqualTo(2));
            Assert.That(restored.Cards[0].BatchId, Is.EqualTo("forest"));
            Assert.That(restored.Cards[0].InstanceId, Is.EqualTo("herb-card"));
        }

        [Test]
        public void TryAddAll_WhenOnlyPartFits_LeavesContainerUnchanged()
        {
            var container = new CardContainer(1, 100f);
            container.Add(new ItemCardStack("herb", 19, 20, 0.1f, 0, "forest", "existing"));

            bool added = container.TryAddAll(
                new ItemCardStack("herb", 3, 20, 0.1f, 0, "forest", "reward"));

            Assert.That(added, Is.False);
            Assert.That(container.Cards, Has.Count.EqualTo(1));
            Assert.That(container.Cards[0].Quantity, Is.EqualTo(19));
            Assert.That(container.Cards[0].InstanceId, Is.EqualTo("existing"));
        }

        [Test]
        public void CanAddAll_DoesNotMutateContainer()
        {
            var container = new CardContainer(2, 100f);
            container.Add(new ItemCardStack("herb", 19, 20, 0.1f, 0, "forest", "existing"));

            bool canAdd = container.CanAddAll(
                new ItemCardStack("herb", 3, 20, 0.1f, 0, "forest", "reward"));

            Assert.That(canAdd, Is.True);
            Assert.That(container.Cards, Has.Count.EqualTo(1));
            Assert.That(container.Cards[0].Quantity, Is.EqualTo(19));
        }
    }
}
