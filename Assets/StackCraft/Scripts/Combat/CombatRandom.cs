using System;

namespace CryingSnow.StackCraft
{
    /// <summary>Small deterministic PRNG stored with each combat session.</summary>
    [Serializable]
    public sealed class CombatRandom
    {
        private const uint NonZeroFallback = 0x6D2B79F5u;

        public uint State { get; private set; }

        public CombatRandom(uint seed)
        {
            State = seed == 0u ? NonZeroFallback : seed;
        }

        public static uint MixSeed(
            int locationSeed,
            int worldMinute,
            long createdSequence)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)locationSeed) * 16777619u;
                hash = (hash ^ (uint)worldMinute) * 16777619u;
                hash = (hash ^ (uint)createdSequence) * 16777619u;
                return hash == 0u ? NonZeroFallback : hash;
            }
        }

        public float NextFloat()
        {
            uint value = NextUInt();
            return (value >> 8) * (1f / 16777216f);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            return minInclusive + (int)(NextUInt() %
                (uint)(maxExclusive - minInclusive));
        }

        private uint NextUInt()
        {
            uint value = State;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            State = value == 0u ? NonZeroFallback : value;
            return State;
        }
    }
}
