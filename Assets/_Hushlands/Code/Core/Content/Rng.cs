namespace Hushlands.Core
{
    /// <summary>SplitMix64. Deterministic across runtimes; mirrored by tools/qa/content_sim.py.</summary>
    public sealed class Rng
    {
        private ulong _state;

        public Rng(long seed)
        {
            unchecked
            {
                _state = (ulong)seed;
            }
        }

        public static long Mix(long seed, int salt)
        {
            unchecked
            {
                return seed * 1000003L + salt;
            }
        }

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble()
        {
            return (NextULong() >> 11) * (1.0 / 9007199254740992.0);
        }

        public double Range(double min, double max)
        {
            return min + (max - min) * NextDouble();
        }

        /// <summary>Uniform integer in [min, max] inclusive.</summary>
        public int RangeInclusive(int min, int max)
        {
            int span = max - min + 1;
            int v = min + (int)(NextDouble() * span);
            return v > max ? max : v;
        }
    }
}
