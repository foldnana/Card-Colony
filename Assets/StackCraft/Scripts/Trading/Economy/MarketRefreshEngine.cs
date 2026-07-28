using System;
using System.Text;

namespace CryingSnow.StackCraft
{
    public sealed class MarketRefreshEngine
    {
        public const int CurrentStateVersion = 1;
        public const int MaxRefreshesPerCatchUp = 32;
        private const int RefreshPatternLength = 16;

        public int LastExplicitRefreshCount { get; private set; }

        public void Initialize(
            MarketProfile profile,
            MarketStateData state,
            long currentWorldHour,
            int economySeed)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (!string.IsNullOrWhiteSpace(state.MarketId))
                return;

            state.MarketId = profile.Id;
            state.AvailableFunds = profile.StartingFunds;
            state.LastRefreshWorldHour = currentWorldHour;
            state.RefreshSequence = 0;
            state.StateRevision = 0;
            state.StateVersion = CurrentStateVersion;
            state.Commodities.Clear();

            var random = CreateRandom(
                economySeed,
                profile.Id,
                state.RefreshSequence);
            foreach (MarketCommodityRule rule in profile.CommodityRules)
            {
                if (rule?.Commodity == null)
                    continue;

                state.Commodities.Add(new MarketCommodityStateData
                {
                    CommodityId = rule.Commodity.Id,
                    Stock = random.NextInclusive(
                        rule.InitialStockMin,
                        rule.InitialStockMax)
                });
            }

            state.NextRefreshWorldHour = SaturatingAddHours(
                currentWorldHour,
                random.NextInclusive(
                    profile.RefreshHoursMin,
                    profile.RefreshHoursMax));
        }

        public void CatchUp(
            MarketProfile profile,
            MarketStateData state,
            long currentWorldHour,
            int economySeed)
        {
            LastExplicitRefreshCount = 0;
            Initialize(
                profile,
                state,
                currentWorldHour,
                economySeed);
            while (LastExplicitRefreshCount <
                       MaxRefreshesPerCatchUp &&
                   state.NextRefreshWorldHour <= currentWorldHour &&
                   state.NextRefreshWorldHour != long.MaxValue &&
                   state.RefreshSequence < int.MaxValue &&
                   state.StateRevision < int.MaxValue)
            {
                RefreshOnce(profile, state, economySeed);
                LastExplicitRefreshCount++;
            }

            if (state.NextRefreshWorldHour > currentWorldHour ||
                state.NextRefreshWorldHour == long.MaxValue ||
                state.RefreshSequence >= int.MaxValue ||
                state.StateRevision >= int.MaxValue)
            {
                return;
            }

            long remainingCapacity = Math.Min(
                (long)int.MaxValue - state.RefreshSequence,
                (long)int.MaxValue - state.StateRevision);
            long remainingRefreshes = CountDueRefreshes(
                profile,
                state.NextRefreshWorldHour,
                state.RefreshSequence,
                currentWorldHour,
                economySeed,
                remainingCapacity);
            AggregateRefreshes(
                profile,
                state,
                economySeed,
                remainingRefreshes);
        }

        private static void RefreshOnce(
            MarketProfile profile,
            MarketStateData state,
            int economySeed)
        {
            long refreshHour = state.NextRefreshWorldHour;
            int nextSequence = state.RefreshSequence + 1;
            var random = CreateRandom(
                economySeed,
                profile.Id,
                nextSequence);
            foreach (MarketCommodityRule rule in
                     profile.CommodityRules)
            {
                int disturbance = random.NextInclusive(-1, 1);
                MarketCommodityStateData commodity =
                    state.GetCommodity(rule?.Commodity?.Id);
                if (commodity == null)
                    continue;

                long stock = (long)commodity.Stock +
                    rule.ProductionPerRefresh -
                    rule.ConsumptionPerRefresh +
                    disturbance;
                commodity.Stock = ClampToInt(
                    stock,
                    rule.MinimumStock,
                    rule.MaximumStock);
                commodity.RecentPlayerPurchaseQuantity = 0;
                commodity.RecentPlayerSaleQuantity = 0;
            }

            state.AvailableFunds = ClampToInt(
                (long)state.AvailableFunds + profile.FundRecovery,
                0,
                profile.MaximumFunds);
            state.LastRefreshWorldHour = refreshHour;
            state.RefreshSequence = nextSequence;
            state.NextRefreshWorldHour = SaturatingAddHours(
                refreshHour,
                random.NextInclusive(
                    profile.RefreshHoursMin,
                    profile.RefreshHoursMax));
            if (state.StateRevision < int.MaxValue)
                state.StateRevision++;
        }

        private static void AggregateRefreshes(
            MarketProfile profile,
            MarketStateData state,
            int economySeed,
            long refreshCount)
        {
            if (refreshCount <= 0)
                return;

            long cycleRefreshCount =
                refreshCount / RefreshPatternLength *
                RefreshPatternLength;
            if (cycleRefreshCount > 0)
            {
                AggregateWholeCycles(
                    profile,
                    state,
                    economySeed,
                    cycleRefreshCount);
            }

            int tailRefreshCount = (int)(
                refreshCount - cycleRefreshCount);
            for (int index = 0;
                 index < tailRefreshCount;
                 index++)
            {
                RefreshOnce(profile, state, economySeed);
            }
        }

        private static void AggregateWholeCycles(
            MarketProfile profile,
            MarketStateData state,
            int economySeed,
            long refreshCount)
        {
            int startingSequence = state.RefreshSequence;
            long firstRefreshHour = state.NextRefreshWorldHour;
            for (int ruleIndex = 0;
                 ruleIndex < profile.CommodityRules.Count;
                 ruleIndex++)
            {
                MarketCommodityRule rule =
                    profile.CommodityRules[ruleIndex];
                MarketCommodityStateData commodity =
                    state.GetCommodity(rule?.Commodity?.Id);
                if (commodity == null)
                    continue;

                commodity.Stock = AdvanceStock(
                    profile,
                    rule,
                    ruleIndex,
                    commodity.Stock,
                    startingSequence,
                    refreshCount,
                    economySeed);
                commodity.RecentPlayerPurchaseQuantity = 0;
                commodity.RecentPlayerSaleQuantity = 0;
            }

            state.AvailableFunds = ClampToInt(
                (long)state.AvailableFunds +
                (long)profile.FundRecovery * refreshCount,
                0,
                profile.MaximumFunds);
            state.LastRefreshWorldHour = AdvanceRefreshHour(
                profile,
                firstRefreshHour,
                startingSequence,
                refreshCount - 1,
                economySeed);
            state.NextRefreshWorldHour = AdvanceRefreshHour(
                profile,
                firstRefreshHour,
                startingSequence,
                refreshCount,
                economySeed);
            state.RefreshSequence += (int)refreshCount;
            state.StateRevision += (int)refreshCount;
        }

        private static long CountDueRefreshes(
            MarketProfile profile,
            long firstRefreshHour,
            int startingSequence,
            long currentWorldHour,
            int economySeed,
            long maximumCount)
        {
            if (maximumCount <= 0 ||
                firstRefreshHour > currentWorldHour)
            {
                return 0;
            }

            long count = 0;
            long refreshHour = firstRefreshHour;
            long cycleHours = GetCycleHours(
                profile,
                startingSequence,
                economySeed);
            long availableHours = SaturatingDifference(
                currentWorldHour,
                refreshHour);
            long fullCycles = Math.Min(
                availableHours / cycleHours,
                maximumCount / RefreshPatternLength);
            if (fullCycles > 0)
            {
                count = fullCycles * RefreshPatternLength;
                refreshHour = SaturatingAddHours(
                    refreshHour,
                    fullCycles * cycleHours);
            }

            while (count < maximumCount &&
                   refreshHour <= currentWorldHour)
            {
                int interval = GetRefreshInterval(
                    profile,
                    (long)startingSequence + count + 1,
                    economySeed);
                count++;
                refreshHour = SaturatingAddHours(
                    refreshHour,
                    interval);
            }
            return count;
        }

        private static int AdvanceStock(
            MarketProfile profile,
            MarketCommodityRule rule,
            int ruleIndex,
            int stock,
            int startingSequence,
            long refreshCount,
            int economySeed)
        {
            long fullCycles =
                refreshCount / RefreshPatternLength;
            int tail = (int)(
                refreshCount % RefreshPatternLength);
            if (fullCycles > 0)
            {
                long cycleDelta = 0;
                int mappedMinimum = rule.MinimumStock;
                int mappedMaximum = rule.MaximumStock;
                for (int offset = 0;
                     offset < RefreshPatternLength;
                     offset++)
                {
                    long delta = GetStockDelta(
                        profile,
                        rule,
                        ruleIndex,
                        (long)startingSequence + offset + 1,
                        economySeed);
                    cycleDelta += delta;
                    mappedMinimum = ClampToInt(
                        (long)mappedMinimum + delta,
                        rule.MinimumStock,
                        rule.MaximumStock);
                    mappedMaximum = ClampToInt(
                        (long)mappedMaximum + delta,
                        rule.MinimumStock,
                        rule.MaximumStock);
                }
                stock = ApplyRepeatedCycle(
                    stock,
                    cycleDelta,
                    mappedMinimum,
                    mappedMaximum,
                    fullCycles);
            }

            long tailSequence =
                (long)startingSequence +
                fullCycles * RefreshPatternLength;
            for (int offset = 0; offset < tail; offset++)
            {
                stock = ClampToInt(
                    (long)stock + GetStockDelta(
                        profile,
                        rule,
                        ruleIndex,
                        tailSequence + offset + 1,
                        economySeed),
                    rule.MinimumStock,
                    rule.MaximumStock);
            }
            return stock;
        }

        private static int ApplyRepeatedCycle(
            int stock,
            long cycleDelta,
            int mappedMinimum,
            int mappedMaximum,
            long cycleCount)
        {
            if (cycleCount <= 0)
                return stock;
            if (cycleDelta == 0)
            {
                return ClampToInt(
                    stock,
                    mappedMinimum,
                    mappedMaximum);
            }

            if (cycleDelta > 0)
            {
                long lowerSteps = Math.Min(
                    cycleCount - 1,
                    DivideRoundUp(
                        (long)mappedMaximum - mappedMinimum,
                        cycleDelta));
                long valueSteps = Math.Min(
                    cycleCount,
                    DivideRoundUp(
                        (long)mappedMaximum - stock,
                        cycleDelta));
                long lowerBound =
                    Math.Min(
                        mappedMaximum,
                        mappedMinimum +
                        lowerSteps * cycleDelta);
                long value = stock + valueSteps * cycleDelta;
                return ClampToInt(
                    value,
                    (int)lowerBound,
                    mappedMaximum);
            }

            long magnitude = -cycleDelta;
            long upperSteps = Math.Min(
                cycleCount - 1,
                DivideRoundUp(
                    (long)mappedMaximum - mappedMinimum,
                    magnitude));
            long fallingSteps = Math.Min(
                cycleCount,
                DivideRoundUp(
                    (long)stock - mappedMinimum,
                    magnitude));
            long upperBound =
                Math.Max(
                    mappedMinimum,
                    mappedMaximum -
                    upperSteps * magnitude);
            long fallingValue =
                stock - fallingSteps * magnitude;
            return ClampToInt(
                fallingValue,
                mappedMinimum,
                (int)upperBound);
        }

        private static long DivideRoundUp(
            long value,
            long divisor)
        {
            if (value <= 0)
                return 0;
            return 1 + (value - 1) / divisor;
        }

        private static long GetStockDelta(
            MarketProfile profile,
            MarketCommodityRule rule,
            int ruleIndex,
            long sequence,
            int economySeed)
        {
            var random = CreateRandom(
                economySeed,
                profile.Id,
                PatternSequence(sequence));
            int disturbance = 0;
            for (int index = 0; index <= ruleIndex; index++)
                disturbance = random.NextInclusive(-1, 1);
            return (long)rule.ProductionPerRefresh -
                rule.ConsumptionPerRefresh +
                disturbance;
        }

        private static int GetRefreshInterval(
            MarketProfile profile,
            long sequence,
            int economySeed)
        {
            var random = CreateRandom(
                economySeed,
                profile.Id,
                PatternSequence(sequence));
            for (int index = 0;
                 index < profile.CommodityRules.Count;
                 index++)
            {
                random.NextInclusive(-1, 1);
            }
            return random.NextInclusive(
                profile.RefreshHoursMin,
                profile.RefreshHoursMax);
        }

        private static long GetCycleHours(
            MarketProfile profile,
            int startingSequence,
            int economySeed)
        {
            long total = 0;
            for (int offset = 0;
                 offset < RefreshPatternLength;
                 offset++)
            {
                total += GetRefreshInterval(
                    profile,
                    (long)startingSequence + offset + 1,
                    economySeed);
            }
            return Math.Max(1L, total);
        }

        private static long AdvanceRefreshHour(
            MarketProfile profile,
            long firstRefreshHour,
            int startingSequence,
            long intervalCount,
            int economySeed)
        {
            if (intervalCount <= 0)
                return firstRefreshHour;

            long fullCycles =
                intervalCount / RefreshPatternLength;
            int tail = (int)(
                intervalCount % RefreshPatternLength);
            long result = SaturatingAddHours(
                firstRefreshHour,
                fullCycles * GetCycleHours(
                    profile,
                    startingSequence,
                    economySeed));
            long tailSequence =
                (long)startingSequence +
                fullCycles * RefreshPatternLength;
            for (int offset = 0; offset < tail; offset++)
            {
                result = SaturatingAddHours(
                    result,
                    GetRefreshInterval(
                        profile,
                        tailSequence + offset + 1,
                        economySeed));
            }
            return result;
        }

        private static long SaturatingAddHours(
            long worldHour,
            long hours)
        {
            long positiveHours = Math.Max(1L, hours);
            return worldHour > long.MaxValue - positiveHours
                ? long.MaxValue
                : worldHour + positiveHours;
        }

        private static long SaturatingDifference(
            long later,
            long earlier)
        {
            if (later <= earlier)
                return 0;
            if (earlier < 0 && later >= 0 &&
                later > long.MaxValue + earlier)
            {
                return long.MaxValue;
            }
            return later - earlier;
        }

        private static int ClampToInt(
            long value,
            int minimum,
            int maximum)
        {
            return (int)Math.Max(minimum, Math.Min(maximum, value));
        }

        private static DeterministicRandom CreateRandom(
            int seed,
            string marketId,
            int sequence)
        {
            sequence = PatternSequence(sequence);
            uint hash = 2166136261u;
            byte[] bytes = Encoding.UTF8.GetBytes(marketId ?? string.Empty);
            foreach (byte value in bytes)
            {
                hash ^= value;
                hash *= 16777619u;
            }
            hash ^= unchecked((uint)seed);
            hash *= 16777619u;
            hash ^= unchecked((uint)sequence);
            hash *= 16777619u;
            return new DeterministicRandom(hash);
        }

        private static int PatternSequence(long sequence)
        {
            long value = sequence % RefreshPatternLength;
            if (value < 0)
                value += RefreshPatternLength;
            return (int)value;
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(uint seed)
            {
                state = seed == 0u ? 0x6D2B79F5u : seed;
            }

            public int NextInclusive(int minimum, int maximum)
            {
                if (maximum <= minimum)
                    return minimum;

                uint value = Next();
                uint range = (uint)(maximum - minimum + 1);
                return minimum + (int)(value % range);
            }

            private uint Next()
            {
                uint value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value;
                return value;
            }
        }
    }
}
