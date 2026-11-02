using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>All settings blocks self-validate; config assets and tests call this.</summary>
    public interface ISettings
    {
        void Validate(List<string> errors);
    }

    internal static class SettingsCheck
    {
        public static void Positive(List<string> e, string owner, string field, float v)
        {
            if (!(v > 0f)) e.Add(owner + "." + field + " must be > 0 (was " + v + ")");
        }

        public static void NonNegative(List<string> e, string owner, string field, float v)
        {
            if (!(v >= 0f)) e.Add(owner + "." + field + " must be >= 0 (was " + v + ")");
        }

        public static void Range(List<string> e, string owner, string field, float v, float min, float max)
        {
            if (!(v >= min && v <= max)) e.Add(owner + "." + field + " must be in [" + min + "," + max + "] (was " + v + ")");
        }

        public static void Ordered(List<string> e, string owner, string lo, float a, string hi, float b)
        {
            if (!(a <= b)) e.Add(owner + "." + lo + " must be <= " + hi);
        }

        /// <summary>Every enum value of T must appear exactly once in keys.</summary>
        public static void CoversEnum<T>(List<string> e, string owner, IEnumerable<T> keys) where T : struct, System.Enum
        {
            var seen = new HashSet<T>();
            foreach (T k in keys)
            {
                if (!seen.Add(k)) e.Add(owner + ": duplicate entry " + k);
            }
            foreach (T v in (T[])System.Enum.GetValues(typeof(T)))
            {
                if (!seen.Contains(v)) e.Add(owner + ": missing entry " + v);
            }
        }
    }
}
