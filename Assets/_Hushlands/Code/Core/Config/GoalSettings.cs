using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    public enum GoalKind
    {
        RunDistance = 0,
        RunRescues = 1,
        RunFlips = 2,
        RunCoins = 3,
        RunCleanDistance = 4,
        LifetimeDistance = 5,
        LifetimeRescues = 6,
        LifetimeCoins = 7,
    }

    [Serializable]
    public struct GoalDef
    {
        public string id;
        public GoalKind kind;
        public int target;
        public int rewardCoins;
        /// <summary>"{0}" is replaced with the target.</summary>
        public string text;

        public GoalDef(string id, GoalKind kind, int target, int reward, string text)
        {
            this.id = id;
            this.kind = kind;
            this.target = target;
            rewardCoins = reward;
            this.text = text;
        }

        public bool IsRunGoal => kind <= GoalKind.RunCleanDistance;
    }

    /// <summary>
    /// Calm goals: three active at a time, completed goals rotate to the next; after the list, goals repeat with the
    /// target scaled by repeatScale per cycle. No goal depends on a vehicle or upgrade (canon); no timers, no failure.
    /// </summary>
    [Serializable]
    public sealed class GoalSettings : ISettings
    {
        public int activeSlots = 3;
        public float repeatScale = 1.5f;
        public GoalDef[] goals =
        {
            new GoalDef("first_ride", GoalKind.RunDistance, 300, 20, "Ride {0} m in one run"),
            new GoalDef("first_rescue", GoalKind.RunRescues, 1, 20, "Bring {0} little group home"),
            new GoalDef("first_flip", GoalKind.RunFlips, 1, 20, "Land {0} flip"),
            new GoalDef("spice_run", GoalKind.RunCoins, 25, 30, "Gather {0} spice coins in one run"),
            new GoalDef("steady", GoalKind.RunCleanDistance, 500, 40, "Ride {0} m without stopping"),
            new GoalDef("wanderer", GoalKind.LifetimeDistance, 3000, 50, "Ride {0} m in all"),
            new GoalDef("flock", GoalKind.LifetimeRescues, 8, 60, "Bring {0} groups home in all"),
            new GoalDef("flips_three", GoalKind.RunFlips, 3, 40, "Land {0} flips in one run"),
            new GoalDef("long_ride", GoalKind.RunDistance, 1000, 60, "Ride {0} m in one run"),
            new GoalDef("treasury", GoalKind.LifetimeCoins, 500, 80, "Gather {0} spice coins in all"),
        };

        public void Validate(List<string> e)
        {
            const string o = nameof(GoalSettings);
            SettingsCheck.Positive(e, o, nameof(activeSlots), activeSlots);
            if (repeatScale < 1f) e.Add(o + ".repeatScale must be >= 1");
            if (goals == null || goals.Length < activeSlots) e.Add(o + ": fewer goals than active slots");
            var ids = new HashSet<string>();
            var lines = new MessageSettings();
            foreach (GoalDef g in goals ?? new GoalDef[0])
            {
                if (string.IsNullOrEmpty(g.id) || !ids.Add(g.id)) e.Add(o + ": bad or duplicate id '" + g.id + "'");
                if (g.target <= 0) e.Add(o + "." + g.id + ": target must be > 0");
                if (g.rewardCoins < 0) e.Add(o + "." + g.id + ": reward must be >= 0");
                if (!Enum.IsDefined(typeof(GoalKind), g.kind)) e.Add(o + "." + g.id + ": unknown kind");
                string text = string.Format(System.Globalization.CultureInfo.InvariantCulture, g.text ?? "", g.target);
                if (!lines.IsClean(text)) e.Add(o + "." + g.id + ": text not clean or too long");
            }
        }
    }
}
