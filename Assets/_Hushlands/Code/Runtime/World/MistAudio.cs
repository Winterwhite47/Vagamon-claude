using Hushlands.Core;
using UnityEngine;
using UnityEngine.Audio;

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

        public void Initialize(MixDirector mix, MistField mist, Transform rider)
        {
            _filter = GetComponent<AudioLowPassFilter>();
            _mix = mix;
            _mist = mist;
            _rider = rider;
        }

        private void LateUpdate()
        {
            if (_mix == null || _mist == null || _rider == null) return;
            _mix.SetMistDensity(_mist.DensityAt(_rider.position.x));
            _filter.cutoffFrequency = _mix.LowPassCutoffHz;
        }
    }

    /// <summary>
    /// Optional AudioMixer bridge: transitions to the snapshot named by AudioMixSettings whenever the camera state or
    /// biome changes. The mixer asset (with snapshots named per AudioMixSettings) is authored in the Editor (T16).
    /// </summary>
    public sealed class AudioMixBridge : MonoBehaviour
    {
        [SerializeField] private AudioMixer mixer;
        private MixDirector _mix;

        public string LastSnapshot { get; private set; }

        public void Initialize(MixDirector mix, AudioMixer audioMixer)
        {
            if (_mix != null) _mix.SnapshotChanged -= OnSnapshot;
            _mix = mix;
            if (audioMixer != null) mixer = audioMixer;
            _mix.SnapshotChanged += OnSnapshot;
            OnSnapshot(_mix.CurrentSnapshot, _mix.CurrentBiome);
        }

        private void OnSnapshot(MixSnapshotEntry entry, BiomeId biome)
        {
            LastSnapshot = entry.snapshotName;
            if (mixer == null || string.IsNullOrEmpty(entry.snapshotName)) return;
            AudioMixerSnapshot snap = mixer.FindSnapshot(entry.snapshotName);
            if (snap != null) snap.TransitionTo(entry.transitionSeconds);
        }

        private void OnDestroy()
        {
            if (_mix != null) _mix.SnapshotChanged -= OnSnapshot;
        }
    }
}
