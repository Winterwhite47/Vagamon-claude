using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    public sealed class CheckpointService : ICheckpointService
    {
        public int LastCheckpointIndex { get; private set; } = -1;
        public SlopePoint LastCheckpointPoint { get; private set; }

        public void Reach(int index, SlopePoint point)
        {
            if (index <= LastCheckpointIndex) return;
            LastCheckpointIndex = index;
            LastCheckpointPoint = point;
        }
    }

    public sealed class AssistService : IAssistService
    {
        private readonly FailureSettings _failure;
        private readonly AssistSettings _assist;
        private int _successes;

        public AssistService(FailureSettings failure, AssistSettings assist)
        {
            _failure = failure ?? throw new ArgumentNullException(nameof(failure));
            _assist = assist ?? throw new ArgumentNullException(nameof(assist));
        }

        public bool AssistsOffered { get; private set; }
        public bool AssistsActive { get; private set; }

        public void OnFailure(int consecutiveFailures)
        {
            _successes = 0;
            if (consecutiveFailures >= _failure.assistAfterFailures) AssistsOffered = true;
        }

        /// <summary>Assists are free and never gated by progression.</summary>
        public void Accept()
        {
            if (AssistsOffered) AssistsActive = true;
        }

        public void OnSuccess()
        {
            if (!AssistsActive) return;
            _successes++;
            if (_successes >= _assist.successesToClear)
            {
                AssistsActive = false;
                AssistsOffered = false;
                _successes = 0;
            }
        }
    }

    public sealed class FailureService : IFailureService
    {
        private readonly ICheckpointService _checkpoints;
        private readonly IAssistService _assists;
        private readonly IDucklingNestService _ducklings;
        private readonly IRider _rider;
        private readonly ISpeedModel _speed;

        public FailureService(IRider rider, ISpeedModel speed, ICheckpointService checkpoints,
            IAssistService assists, IDucklingNestService ducklings)
        {
            _rider = rider ?? throw new ArgumentNullException(nameof(rider));
            _speed = speed ?? throw new ArgumentNullException(nameof(speed));
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            _assists = assists ?? throw new ArgumentNullException(nameof(assists));
            _ducklings = ducklings ?? throw new ArgumentNullException(nameof(ducklings));
        }

        public int ConsecutiveFailures { get; private set; }
        public event Action<CrashCause> RunFailed;

        public void ReportCrash(CrashCause cause)
        {
            ConsecutiveFailures++;
            _ducklings.OnCrash();
            _assists.OnFailure(ConsecutiveFailures);
            RunFailed?.Invoke(cause);
        }

        public void ReportCheckpointCleared()
        {
            ConsecutiveFailures = 0;
            _assists.OnSuccess();
        }

        public void InstantRetry()
        {
            _ducklings.RestoreToCheckpoint(_checkpoints.LastCheckpointIndex);
            _rider.ResetTo(_checkpoints.LastCheckpointPoint, _speed.MinSpeed);
        }
    }

    /// <summary>
    /// Duckling rescue state. Safety is structural: groups are only Ahead, Rescued or Waiting.
    /// </summary>
    public sealed class DucklingNestService : IDucklingNestService
    {
        private readonly Dictionary<int, DucklingGroupStatus> _status = new Dictionary<int, DucklingGroupStatus>();
        private readonly Dictionary<int, int> _groupCheckpoint = new Dictionary<int, int>();
        private readonly List<int> _groups = new List<int>();

        public IReadOnlyList<int> Groups => _groups;
        public bool NestCoversClosed { get; private set; }

        public void Register(int groupId, int checkpointIndex)
        {
            if (_status.ContainsKey(groupId)) return;
            _groups.Add(groupId);
            _status[groupId] = DucklingGroupStatus.Ahead;
            _groupCheckpoint[groupId] = checkpointIndex;
        }

        public DucklingGroupStatus StatusOf(int groupId)
        {
            return _status.TryGetValue(groupId, out DucklingGroupStatus s) ? s : DucklingGroupStatus.Waiting;
        }

        public void Rescue(int groupId)
        {
            if (_status.ContainsKey(groupId)) _status[groupId] = DucklingGroupStatus.Rescued;
        }

        public void MarkPassed(int groupId)
        {
            if (_status.TryGetValue(groupId, out DucklingGroupStatus s) && s == DucklingGroupStatus.Ahead)
            {
                _status[groupId] = DucklingGroupStatus.Waiting;
            }
        }

        public void OnCrash()
        {
            NestCoversClosed = true;
        }

        public void RestoreToCheckpoint(int checkpointIndex)
        {
            foreach (int g in _groups)
            {
                if (_groupCheckpoint[g] > checkpointIndex && _status[g] != DucklingGroupStatus.Rescued)
                {
                    _status[g] = DucklingGroupStatus.Ahead;
                }
            }
            NestCoversClosed = false;
        }
    }
}
