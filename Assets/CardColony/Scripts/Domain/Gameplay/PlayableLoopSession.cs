using System;
using CardColony.Inventory;
using CardColony.TimeSystem;
using CardColony.World;

namespace CardColony.Gameplay
{
    public sealed class PlayableLoopSession
    {
        public const int CurrentSchemaVersion = 2;
        public const string ForestLocationId = "whispering-forest";
        public const string HerbItemId = "wild-herb";
        public const string PotionItemId = "healing-potion";
        public const float ExploreDurationMinutes = 30f;
        public const float GatherDurationMinutes = 20f;
        public const float BrewDurationMinutes = 15f;

        private IDisposable actionHandle;

        public ActionDrivenWorldClock Clock { get; }
        public CardContainer PlayerInventory { get; }
        public WorldStateStore World { get; }
        public ItemCardStack HeldCard { get; private set; }
        public LoopActionState ActiveAction { get; private set; }
        public string LastMessage { get; private set; } = "准备探索低语森林。";

        public PlayableLoopSession(
            float gameMinutesPerRealSecond,
            double initialTotalMinutes,
            int inventorySlots,
            float inventoryMaxWeight)
            : this(
                new ActionDrivenWorldClock(gameMinutesPerRealSecond, initialTotalMinutes),
                new CardContainer(inventorySlots, inventoryMaxWeight),
                new WorldStateStore())
        {
        }

        private PlayableLoopSession(
            ActionDrivenWorldClock clock,
            CardContainer playerInventory,
            WorldStateStore world)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            PlayerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            World = world ?? throw new ArgumentNullException(nameof(world));
        }

        public LoopCommandResult StartExploreWhisperingForest()
        {
            if (ActiveAction != null)
                return Failure("已有行动正在进行。" );
            if (World.GetOrCreateLocation(ForestLocationId).IsDiscovered)
                return Failure("低语森林已经探索过了。" );

            return StartAction(
                LoopActionType.ExploreWhisperingForest,
                "explore-whispering-forest",
                ExploreDurationMinutes,
                "开始探索低语森林。" );
        }

        public LoopCommandResult StartGatherHerbs()
        {
            if (ActiveAction != null)
                return Failure("已有行动正在进行。" );
            if (!World.GetOrCreateLocation(ForestLocationId).IsDiscovered)
                return Failure("需要先探索低语森林。" );
            if (!PlayerInventory.CanAddAll(CreateHerbCard(3)))
                return Failure("背包空间不足，无法开始采集。" );

            return StartAction(
                LoopActionType.GatherHerbs,
                "gather-wild-herbs",
                GatherDurationMinutes,
                "开始采集草药。" );
        }

        public LoopCommandResult StartBrewPotion()
        {
            if (ActiveAction != null)
                return Failure("已有行动正在进行。" );
            if (HeldCard != null)
                return Failure("请先把手持卡放回背包。" );
            if (PlayerInventory.GetQuantity(HerbItemId) < 2)
                return Failure("制作药水需要两份草药。" );

            CardContainer simulatedInventory = CardContainer.FromSnapshot(PlayerInventory.CreateSnapshot());
            simulatedInventory.TryConsume(HerbItemId, 2);
            if (!simulatedInventory.CanAddAll(CreatePotionCard(1)))
                return Failure("背包空间不足，无法开始制作。" );

            PlayerInventory.TryConsume(HerbItemId, 2);

            return StartAction(
                LoopActionType.BrewPotion,
                "brew-healing-potion",
                BrewDurationMinutes,
                "消耗两份草药，开始制作药水。" );
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            double before = Clock.TotalMinutes;
            Clock.Tick(unscaledDeltaSeconds);
            if (ActiveAction == null)
                return;

            double advancedWorldMinutes = Clock.TotalMinutes - before;
            if (advancedWorldMinutes <= 0d)
                return;

            ActiveAction.Advance(advancedWorldMinutes);
            if (ActiveAction.IsComplete)
                CompleteActiveAction();
        }

        public RunSnapshot CreateSnapshot()
        {
            return new RunSnapshot
            {
                SchemaVersion = CurrentSchemaVersion,
                Clock = new WorldClockSnapshot
                {
                    TotalMinutes = Clock.TotalMinutes,
                    Speed = (int)Clock.Speed,
                    IsPaused = Clock.IsPaused,
                    IsWaiting = Clock.IsWaiting
                },
                World = World.CreateSnapshot(),
                PlayerInventory = PlayerInventory.CreateSnapshot(),
                HeldCard = HeldCard == null ? null : CreateItemCardSnapshot(HeldCard),
                ActiveAction = ActiveAction?.CreateSnapshot()
            };
        }

        public static PlayableLoopSession Restore(RunSnapshot snapshot, float gameMinutesPerRealSecond)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SchemaVersion > CurrentSchemaVersion)
                throw new NotSupportedException(
                    $"Run schema {snapshot.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}.");
            if (snapshot.Clock == null || snapshot.World == null || snapshot.PlayerInventory == null)
                throw new ArgumentException("Run snapshot is missing required data.", nameof(snapshot));

            var clock = new ActionDrivenWorldClock(gameMinutesPerRealSecond, snapshot.Clock.TotalMinutes)
            {
                Speed = (WorldClockSpeed)snapshot.Clock.Speed,
                IsPaused = snapshot.Clock.IsPaused,
                IsWaiting = snapshot.Clock.IsWaiting
            };
            var session = new PlayableLoopSession(
                clock,
                CardContainer.FromSnapshot(snapshot.PlayerInventory),
                new WorldStateStore(snapshot.World));

            if (snapshot.HeldCard != null)
                session.HeldCard = RestoreItemCard(snapshot.HeldCard);

            if (snapshot.ActiveAction != null)
            {
                LoopActionType type = (LoopActionType)snapshot.ActiveAction.Type;
                session.ActiveAction = new LoopActionState(
                    type,
                    snapshot.ActiveAction.ActionId,
                    snapshot.ActiveAction.DurationWorldMinutes,
                    snapshot.ActiveAction.ElapsedWorldMinutes);
                session.actionHandle = session.Clock.BeginAction(session.ActiveAction.ActionId);
                session.LastMessage = "已恢复正在进行的行动。";
            }

            return session;
        }

        public bool TryTakeCard(string cardInstanceId, int quantity)
        {
            if (ActiveAction != null || HeldCard != null)
                return false;
            if (!PlayerInventory.TryRemove(cardInstanceId, quantity, out ItemCardStack detached))
                return false;

            HeldCard = detached;
            return true;
        }

        public bool TrySplitOne(string cardInstanceId)
        {
            ItemCardStack source = null;
            foreach (ItemCardStack card in PlayerInventory.Cards)
            {
                if (card.InstanceId == cardInstanceId)
                {
                    source = card;
                    break;
                }
            }

            return source != null && source.Quantity > 1 && TryTakeCard(cardInstanceId, 1);
        }

        public bool TryPutHeldCardBack()
        {
            if (HeldCard == null || !PlayerInventory.TryAddAll(HeldCard))
                return false;

            HeldCard = null;
            return true;
        }

        public static ItemCardStack CreateHerbCard(int quantity)
        {
            return new ItemCardStack(HerbItemId, quantity, 20, 0.1f, 0, "forest");
        }

        public static ItemCardStack CreatePotionCard(int quantity)
        {
            return new ItemCardStack(PotionItemId, quantity, 10, 0.25f, 1, "crafted");
        }

        private static ItemCardSnapshot CreateItemCardSnapshot(ItemCardStack card)
        {
            return new ItemCardSnapshot
            {
                ItemId = card.ItemId,
                Quantity = card.Quantity,
                MaxStackSize = card.MaxStackSize,
                UnitWeight = card.UnitWeight,
                Quality = card.Quality,
                BatchId = card.BatchId,
                InstanceId = card.InstanceId
            };
        }

        private static ItemCardStack RestoreItemCard(ItemCardSnapshot card)
        {
            return new ItemCardStack(
                card.ItemId,
                card.Quantity,
                card.MaxStackSize,
                card.UnitWeight,
                card.Quality,
                card.BatchId,
                card.InstanceId);
        }

        private LoopCommandResult StartAction(
            LoopActionType type,
            string actionId,
            double durationWorldMinutes,
            string message)
        {
            if (HeldCard != null)
                return Failure("请先把手持卡放回背包。" );

            ActiveAction = new LoopActionState(type, actionId, durationWorldMinutes);
            actionHandle = Clock.BeginAction(actionId);
            LastMessage = message;
            return LoopCommandResult.Success(message);
        }

        private void CompleteActiveAction()
        {
            LoopActionType completedType = ActiveAction.Type;

            switch (completedType)
            {
                case LoopActionType.ExploreWhisperingForest:
                    LocationRuntimeState forest = World.GetOrCreateLocation(ForestLocationId);
                    forest.IsDiscovered = true;
                    forest.ExplorationProgress = Math.Max(forest.ExplorationProgress, 0.25f);
                    World.SetFlag("location.whispering-forest.discovered", true);
                    LastMessage = "已发现低语森林，可以采集草药。";
                    break;

                case LoopActionType.GatherHerbs:
                    if (!TryAddReward(CreateHerbCard(3)))
                        return;
                    LastMessage = "采集完成，获得三份草药。";
                    break;

                case LoopActionType.BrewPotion:
                    if (!TryAddReward(CreatePotionCard(1)))
                        return;
                    LastMessage = "制作完成，获得一瓶治疗药水。";
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported action type '{completedType}'.");
            }

            actionHandle.Dispose();
            actionHandle = null;
            ActiveAction = null;
        }

        private bool TryAddReward(ItemCardStack reward)
        {
            if (PlayerInventory.TryAddAll(reward))
                return true;

            LastMessage = "背包空间不足，行动奖励正在等待领取。";
            return false;
        }

        private LoopCommandResult Failure(string message)
        {
            LastMessage = message;
            return LoopCommandResult.Failure(message);
        }
    }
}
