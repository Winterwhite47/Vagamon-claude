using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Mist low-passes audio: drives an AudioLowPassFilter (on the listener) from MixDirector's cutoff.</summary>
    [RequireComponent(typeof(AudioLowPassFilter))]
    public sealed class MistLowPass : MonoBehaviour
    {
        private AudioLowPassFilter _filter;
        private MixDirector _mix;
        private MistField _mist;
        private Transform _rider;
        private RunSession _session;

        /// <summary>Density the rider is in right now (lantern thins it).</summary>
        public float CurrentDensity { get; private set; }

        public void Initialize(MixDirector mix, MistField mist, Transform rider, RunSession session)
        {
            _filter = GetComponent<AudioLowPassFilter>();
            _mix = mix;
            _mist = mist;
            _rider = rider;
            _session = session;
        }

        private void LateUpdate()
        {
            if (_mix == null || _mist == null || _rider == null) return;
            float lantern = _session != null ? _session.Pickups.MistScale : 1f;
            float squall = _session != null ? _session.Squall.MistBonus : 0f;
            CurrentDensity = Mathf.Clamp01(_mist.DensityAt(_rider.position.x) * lantern + squall);
            _mix.SetMistDensity(CurrentDensity);
            _filter.cutoffFrequency = _mix.LowPassCutoffHz;
        }
    }
}
