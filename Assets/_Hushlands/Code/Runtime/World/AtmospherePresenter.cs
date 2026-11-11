using Hushlands.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Hushlands
{
    /// <summary>
    /// Drives sun, fog and exposure every frame: the biome look (blended between neighbours along the journey,
    /// continuous, no cuts), the day's slow swing and the weather. Hands the camera the dominant biome.
    /// Along the journey, fog heights follow the track as it descends.
    /// Runs before HazardPresenter (which scales the fog for squalls from SliceRefs.FogBaseMeanFreePath).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class AtmospherePresenter : MonoBehaviour
    {
        private SliceRefs _refs;
        private LookSettings _look;
        private DirectorSettings _director;
        private CameraDirector _camera;
        private Exposure _exposure;
        private BiomeId _biome;
        private bool _hasDominant;

        /// <summary>Blend under the rider right now.</summary>
        public BiomeBlend Current { get; private set; }
        public BiomeId Dominant { get; private set; }
        /// <summary>What was applied this frame (before the squall's fog scale).</summary>
        public AtmosphereState State { get; private set; }

        public void Initialize(SliceRefs refs, LookSettings look, DirectorSettings director, CameraDirector cameraDirector, BiomeId biome)
        {
            _refs = refs;
            _look = look;
            _director = director;
            _camera = cameraDirector;
            _biome = biome;
            if (refs.Volume != null && refs.Volume.sharedProfile != null) refs.Volume.sharedProfile.TryGet(out _exposure);
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>Samples the plan at the rider and applies the look.</summary>
        public void Apply()
        {
            if (_refs == null || _refs.Rider == null) return;
            double x = _refs.Rider.transform.position.x;
            BiomeBlend b = _refs.Plan != null ? _refs.Plan.BlendAt(x) : new BiomeBlend(_biome, _biome, 0d);
            Current = b;
            BiomeLook from = _look.Get(b.From);
            BiomeLook to = _look.Get(b.To);
            float t = (float)b.T;

            RunSession session = _refs.Run != null ? _refs.Run.Session : null;
            double swing = session != null ? session.Day.Swing(session.RunSeconds) : 0d;
            WeatherDirector weather = session != null ? session.Weather : null;
            AtmosphereState a = AtmosphereModel.Evaluate(from, to, b.T, _director, swing,
                weather != null ? weather.SunLuxScale : 1f, weather != null ? weather.FogScale : 1f,
                weather != null ? weather.ExposureOffsetEV : 0f);
            State = a;

            if (_refs.Sun != null)
            {
                _refs.Sun.transform.rotation = Quaternion.Euler(a.SunElevationDegrees, a.SunAzimuthDegrees, 0f);
                _refs.Sun.intensity = a.SunLux;
                _refs.Sun.colorTemperature = a.SunKelvin;
            }

            _refs.FogBaseMeanFreePath = a.FogMeanFreePath;
            if (_refs.Fog != null)
            {
                // A single slice keeps its fixed fog heights; the journey descends for kilometres, so they follow the track.
                float ground = _refs.Plan != null ? _refs.Slope.HeightAt(x) : 0f;
                _refs.Fog.meanFreePath.Override(a.FogMeanFreePath);
                _refs.Fog.baseHeight.Override(ground + a.FogBaseHeight);
                _refs.Fog.maximumHeight.Override(ground + a.FogMaximumHeight);
                _refs.Fog.anisotropy.Override(a.FogAnisotropy);
                Palette.TryParse(from.fogAlbedo, out float ar, out float ag, out float ab);
                Palette.TryParse(to.fogAlbedo, out float br, out float bg, out float bb);
                _refs.Fog.albedo.Override(Color.Lerp(new Color(ar, ag, ab, 1f), new Color(br, bg, bb, 1f), t));
            }
            if (_exposure != null) _exposure.fixedExposure.Override(a.ExposureEV);

            // Wind for the scarf (HazardPresenter reads SliceRefs.Look).
            BiomeLook look = b.Dominant == b.From ? from : to;
            look.windExtraX = a.WindExtraX;
            look.windExtraZ = a.WindExtraZ;
            _refs.Look = look;

            if (!_hasDominant || b.Dominant != Dominant)
            {
                Dominant = b.Dominant;
                bool first = !_hasDominant;
                _hasDominant = true;
                // The camera was initialised with the starting biome; only later changes need telling.
                if (!first && _camera != null) _camera.SetBiome(Dominant);
            }
        }
    }
}
