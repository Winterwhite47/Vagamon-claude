using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Plays the synthesized placeholder audio: wind, sled and pad beds mixed by SoundMix (which follows the camera
    /// state), and short tones for pickups, rescues, landings, collapse warnings, retries and humor beats.
    /// The mist low-pass on the listener filters all of it. Every clip is generated here: original, no imported audio.
    /// </summary>
    public sealed class SoundPresenter : MonoBehaviour
    {
        private SoundSettings _s;
        private SpeedSettings _speed;
        private SliceRefs _refs;
        private CameraDirector _camera;
        private IGameState _game;
        private SoundMix _mix;
        private AudioSource _wind;
        private AudioSource _sled;
        private AudioSource _pad;
        private AudioSource[] _voices;
        private int _nextVoice;
        private readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        private RunSession _session;

        public SoundMix Mix => _mix;
        /// <summary>Short sounds played so far, by kind.</summary>
        public Dictionary<SfxId, int> Played { get; } = new Dictionary<SfxId, int>();

        /// <summary>Whether a ride is in progress (title and pause keep only the ambient beds).</summary>
        public interface IGameState
        {
            bool Riding { get; }
        }

        public void Initialize(SoundSettings settings, SpeedSettings speed, SliceRefs refs, CameraDirector cameraDirector,
            IGameState game, long seed)
        {
            _s = settings;
            _speed = speed;
            _refs = refs;
            _camera = cameraDirector;
            _game = game;
            _mix = new SoundMix(settings);
            _session = refs.Run.Session;
            if (!settings.enabled) return;

            _wind = Bed("Wind", SoundSynth.NoiseLoop(settings, settings.windLowPassHz, Rng.Mix(seed, 301)));
            _sled = Bed("Sled", SoundSynth.NoiseLoop(settings, settings.sledLowPassHz, Rng.Mix(seed, 302)));
            _pad = Bed("Pad", SoundSynth.PadLoop(settings));
            _voices = new AudioSource[settings.voices];
            for (int i = 0; i < _voices.Length; i++) _voices[i] = Source("Voice_" + i, false);
            foreach (SfxEntry e in settings.sfx)
                _clips[e.id] = Clip("Sfx_" + e.id, SoundSynth.Tone(settings, e, Rng.Mix(seed, 310 + (int)e.id)));

            _session.Pickups.Collected += OnPickup;
            _session.Ducklings.Rescued += OnRescue;
            _session.Collapse.WarningStarted += OnCollapseWarning;
            _session.Flow.Retried += OnRetried;
            _session.Humor.BeatStarted += OnHumor;
            refs.Rider.Landed += OnLanded;
        }

        private void OnDestroy()
        {
            if (_session == null || _s == null || !_s.enabled) return;
            _session.Pickups.Collected -= OnPickup;
            _session.Ducklings.Rescued -= OnRescue;
            _session.Collapse.WarningStarted -= OnCollapseWarning;
            _session.Flow.Retried -= OnRetried;
            _session.Humor.BeatStarted -= OnHumor;
            if (_refs != null && _refs.Rider != null) _refs.Rider.Landed -= OnLanded;
        }

        private AudioSource Source(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            AudioSource a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = loop;
            a.spatialBlend = 0f;
            a.volume = 0f;
            return a;
        }

        private AudioSource Bed(string bedName, float[] samples)
        {
            AudioSource a = Source(bedName, true);
            a.clip = Clip(bedName, samples);
            a.Play();
            return a;
        }

        private AudioClip Clip(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, _s.sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void Update()
        {
            if (_mix == null || _refs == null || _wind == null) return;
            PhysxRider rider = _refs.Rider;
            bool riding = _game != null && _game.Riding && !_refs.Run.Paused && !_refs.Run.Finished;
            var inputs = new SoundInputs
            {
                Riding = riding,
                Grounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking,
                Speed = rider.Speed,
                MinSpeed = _speed.minSpeed,
                MaxSpeed = _speed.maxSpeed,
                SquallIntensity = _session.Squall.Intensity,
                Tension = _session.Pacing.Intensity,
                State = _camera != null ? _camera.Current : CameraState.Cruise,
            };
            // Unscaled: the beds keep easing while the world is paused under a menu.
            _mix.Step(inputs, Time.unscaledDeltaTime);
            _wind.volume = _mix.WindGain;
            _wind.pitch = _mix.WindPitch;
            _sled.volume = _mix.SledGain;
            _sled.pitch = _mix.SledPitch;
            _pad.volume = _mix.PadGain;
        }

        private void Play(SfxId id, float gain, float pitch)
        {
            Played.TryGetValue(id, out int n);
            Played[id] = n + 1;
            if (_voices == null || gain <= 0f) return;
            AudioSource v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            v.Stop();
            v.clip = _clips[id];
            v.volume = gain;
            v.pitch = pitch;
            v.Play();
        }

        private void OnPickup(PickupInstance p)
        {
            Play(SfxId.Pickup, _s.Get(SfxId.Pickup).gain, _mix.PickupPitch(p.Kind));
        }

        private void OnRescue(NestSite site)
        {
            Play(SfxId.Rescue, _s.Get(SfxId.Rescue).gain, 1f);
        }

        private void OnCollapseWarning(CollapseController.Zone zone)
        {
            Play(SfxId.CollapseWarning, _s.Get(SfxId.CollapseWarning).gain, 1f);
        }

        private void OnRetried(SlopePoint point)
        {
            Play(SfxId.Retry, _s.Get(SfxId.Retry).gain, 1f);
        }

        private void OnHumor(HumorBeat beat)
        {
            Play(SfxId.Humor, _s.Get(SfxId.Humor).gain, 1f);
        }

        private void OnLanded(float verticalSpeed)
        {
            float gain = _mix.LandingGain(verticalSpeed);
            if (gain > 0f) Play(SfxId.Landing, gain, 1f);
        }
    }
}
