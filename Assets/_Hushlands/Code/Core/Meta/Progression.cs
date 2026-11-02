using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hushlands.Core
{
    /// <summary>Per-run counters kept by RunSession.</summary>
    public sealed class RunStats
    {
        public double StartX;
        public double FurthestX;
        /// <summary>Where the current crash-free stretch began (run start or last retry point).</summary>
        public double CleanFromX;
        public double CurrentClean;
        public double LongestClean;
        public int Crashes;
        public int Flips;
        public int Coins;
        public int Rescues;

        public double Distance => Math.Max(0d, FurthestX - StartX);
        public double BestClean => Math.Max(LongestClean, CurrentClean);
    }

    public readonly struct RunSummary
    {
        public readonly long Score;
        public readonly double Distance;
        public readonly int Coins;
        public readonly int Flips;
        public readonly int Rescues;
        public readonly int Missed;
        public readonly int Crashes;
        public readonly double LongestClean;

        public RunSummary(long score, double distance, int coins, int flips, int rescues, int missed, int crashes, double clean)
        {
            Score = score;
            Distance = distance;
            Coins = coins;
            Flips = flips;
            Rescues = rescues;
            Missed = missed;
            Crashes = crashes;
            LongestClean = clean;
        }
    }

    /// <summary>
    /// Lifetime profile. Missed groups become "waiting" (never lost); every later rescue first brings a waiting group
    /// home, so the waiting count only ever goes down by rescues ("someone is waiting for you to come home").
    /// </summary>
    public sealed class PlayerProfile
    {
        public const string KeyRuns = "profile.runs";
        public const string KeyBestScore = "profile.bestScore";
        public const string KeyBestDistance = "profile.bestDistance";
        public const string KeyLifetimeDistance = "profile.lifetimeDistance";
        public const string KeyLifetimeFlips = "profile.lifetimeFlips";
        public const string KeyGroupsRescued = "profile.groupsRescued";
        public const string KeyGroupsWaiting = "profile.groupsWaiting";
        public const string KeyHomecomings = "profile.homecomings";

        private readonly ISaveService _save;
        private readonly Garage _garage;

        public PlayerProfile(ISaveService save, Garage garage)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _garage = garage ?? throw new ArgumentNullException(nameof(garage));
        }

        public int Runs => Int(KeyRuns);
        public long BestScore => Long(KeyBestScore);
        public double BestDistance => Double(KeyBestDistance);
        public double LifetimeDistance => Double(KeyLifetimeDistance);
        public int LifetimeFlips => Int(KeyLifetimeFlips);
        public int GroupsRescued => Int(KeyGroupsRescued);
        public int GroupsWaiting => Int(KeyGroupsWaiting);
        public int Homecomings => Int(KeyHomecomings);
        public long LifetimeCoins => _garage.LifetimeCoins;
        public bool NewBest { get; private set; }

        public void ApplyRun(RunSummary r)
        {
            _save.Save(KeyRuns, Runs + 1);
            NewBest = r.Score > BestScore;
            if (NewBest) _save.Save(KeyBestScore, r.Score);
            if (r.Distance > BestDistance) _save.Save(KeyBestDistance, r.Distance);
            _save.Save(KeyLifetimeDistance, LifetimeDistance + r.Distance);
            _save.Save(KeyLifetimeFlips, LifetimeFlips + r.Flips);
            _save.Save(KeyGroupsRescued, GroupsRescued + r.Rescues);
            int waiting = GroupsWaiting;
            int home = Math.Min(waiting, r.Rescues);
            _save.Save(KeyHomecomings, Homecomings + home);
            _save.Save(KeyGroupsWaiting, waiting - home + r.Missed);
            _garage.AddLifetimeCoins(r.Coins);
        }

        private int Int(string k)
        {
            return _save.TryLoad(k, out int v) ? v : 0;
        }

        private long Long(string k)
        {
            return _save.TryLoad(k, out long v) ? v : 0L;
        }

        private double Double(string k)
        {
            return _save.TryLoad(k, out double v) ? v : 0d;
        }
    }

    public readonly struct GoalStatus
    {
        public readonly GoalDef Def;
        public readonly int Target;
        public readonly double Progress;
        public readonly string Text;

        public GoalStatus(GoalDef def, int target, double progress, string text)
        {
            Def = def;
            Target = target;
            Progress = progress;
            Text = text;
        }

        public bool Done => Progress >= Target;
    }

    /// <summary>
    /// Active goals, persisted as slot indices into the (cycling) goal list. Run goals check live run stats; lifetime
    /// goals check the profile plus the current run. Completing a goal grants its coins (unlocking vehicles) and fills
    /// the slot with the next goal. Never fails, never times out.
    /// </summary>
    public sealed class GoalTracker
    {
        public const string KeyNext = "goals.next";
        public const string KeySlotPrefix = "goals.slot.";

        private readonly GoalSettings _s;
        private readonly ISaveService _save;
        private readonly PlayerProfile _profile;
        private readonly Garage _garage;
        private readonly int[] _slots;

        public GoalTracker(GoalSettings settings, ISaveService save, PlayerProfile profile, Garage garage)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _garage = garage ?? throw new ArgumentNullException(nameof(garage));
            _slots = new int[settings.activeSlots];
            int next = save.TryLoad(KeyNext, out int n) ? n : 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (save.TryLoad(KeySlotPrefix + i, out int idx) && idx >= 0) _slots[i] = idx;
                else _slots[i] = next++;
            }
            _save.Save(KeyNext, Math.Max(next, MaxSlot() + 1));
            for (int i = 0; i < _slots.Length; i++) _save.Save(KeySlotPrefix + i, _slots[i]);
        }

        public int CompletedCount { get; private set; }
        public event Action<GoalStatus> Completed;

        public GoalStatus Status(int slot, RunStats run)
        {
            int idx = _slots[slot];
            GoalDef def = _s.goals[idx % _s.goals.Length];
            int cycle = idx / _s.goals.Length;
            int target = (int)Math.Ceiling(def.target * Math.Pow(_s.repeatScale, cycle));
            double progress = ProgressOf(def.kind, run);
            string text = string.Format(CultureInfo.InvariantCulture, def.text, target);
            return new GoalStatus(def, target, progress, text);
        }

        public IReadOnlyList<GoalStatus> Active(RunStats run)
        {
            var list = new List<GoalStatus>();
            for (int i = 0; i < _slots.Length; i++) list.Add(Status(i, run));
            return list;
        }

        /// <summary>Checks every slot; completed goals reward coins and rotate. Call during and at the end of a run.</summary>
        public void Evaluate(RunStats run)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                GoalStatus st = Status(i, run);
                if (!st.Done) continue;
                _garage.AddLifetimeCoins(st.Def.rewardCoins);
                CompletedCount++;
                int next = _save.TryLoad(KeyNext, out int n) ? n : MaxSlot() + 1;
                _slots[i] = next;
                _save.Save(KeyNext, next + 1);
                _save.Save(KeySlotPrefix + i, next);
                Completed?.Invoke(st);
            }
        }

        private double ProgressOf(GoalKind kind, RunStats run)
        {
            switch (kind)
            {
                case GoalKind.RunDistance: return run != null ? run.Distance : 0d;
                case GoalKind.RunRescues: return run != null ? run.Rescues : 0;
                case GoalKind.RunFlips: return run != null ? run.Flips : 0;
                case GoalKind.RunCoins: return run != null ? run.Coins : 0;
                case GoalKind.RunCleanDistance: return run != null ? run.BestClean : 0d;
                case GoalKind.LifetimeDistance: return _profile.LifetimeDistance + (run != null ? run.Distance : 0d);
                case GoalKind.LifetimeRescues: return _profile.GroupsRescued + (run != null ? run.Rescues : 0);
                default: return _profile.LifetimeCoins + (run != null ? run.Coins : 0);
            }
        }

        private int MaxSlot()
        {
            int m = -1;
            foreach (int s in _slots) m = Math.Max(m, s);
            return m;
        }
    }
}

namespace Hushlands.Core
{
    /// <summary>Save + garage + profile + goals: what a run reports to at its end.</summary>
    public sealed class ProgressionService
    {
        public ProgressionService(ISaveService save, VehicleSettings vehicles, GoalSettings goals)
        {
            Save = save ?? throw new ArgumentNullException(nameof(save));
            Garage = new Garage(vehicles, save);
            Profile = new PlayerProfile(save, Garage);
            Goals = new GoalTracker(goals, save, Profile, Garage);
        }

        public ISaveService Save { get; }
        public Garage Garage { get; }
        public PlayerProfile Profile { get; }
        public GoalTracker Goals { get; }

        /// <summary>Applies a finished run, settles goals, and flushes a file save.</summary>
        public void Complete(RunSummary summary, RunStats stats)
        {
            Goals.Evaluate(stats);
            Profile.ApplyRun(summary);
            Goals.Evaluate(null);
            if (Save is FileSaveService file) file.Flush();
        }
    }
}
