using Hushlands.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Hushlands
{
    /// <summary>
    /// Optional AudioMixer bridge: transitions to the snapshot named by AudioMixSettings whenever the camera state or
    /// biome changes. Without a mixer asset (authored audio, later) the synthesized beds in SoundPresenter follow the
    /// same camera states through SoundSettings.bedScales.
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
