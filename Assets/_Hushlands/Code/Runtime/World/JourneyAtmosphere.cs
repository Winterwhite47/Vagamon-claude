using Hushlands.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Hushlands
{
    /// <summary>
    /// Along the full journey, blends sun, fog and exposure between neighbouring biomes at the rider's position
    /// (continuous, no cuts) and hands the camera the dominant biome. Fog heights follow the track as it descends.
    /// Runs before HazardPresenter (which scales the fog for squalls from SliceRefs.FogBaseMeanFreePath).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class JourneyAtmosphere : MonoBehaviour
    {
        private SliceRefs _refs;
        private LookSettings _look;
        private CameraDirector _camera;
        private Exposure _exposure;
        private bool _hasDominant;

        /// <summary>Blend under the rider right now.</summary>
        public BiomeBlend Current { get; private set; }
        public BiomeId Dominant { get; private set; }

        public void Initialize(SliceRefs refs, LookSettings look, CameraDirector cameraDirector)
        {
            _refs = refs;
            _look = look;
            _camera = cameraDirector;
            if (refs.Volume != null && refs.Volume.sharedProfile != null) refs.Volume.sharedProfile.TryGet(out _exposure);
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>Samples the plan at the rider and applies the blended look.</summary>
        public void Apply()
        {
            if (_refs == null || _refs.Plan == null || _refs.Rider == null) return;
            double x = _refs.Rider.transform.position.x;
            BiomeBlend b = _refs.Plan.BlendAt(x);
            Current = b;
            BiomeLook from = _look.Get(b.From);
            BiomeLook to = _look.Get(b.To);
            float t = (float)b.T;

            if (_refs.Sun != null)
            {
                _refs.Sun.transform.rotation = Quaternion.Slerp(
                    Quaternion.Euler(from.sunElevationDegrees, from.sunAzimuthDegrees, 0f),
                    Quaternion.Euler(to.sunElevationDegrees, to.sunAzimuthDegrees, 0f), t);
                _refs.Sun.intensity = Mathf.Lerp(from.sunLux, to.sunLux, t);
                _refs.Sun.colorTemperature = Mathf.Lerp(from.sunKelvin, to.sunKelvin, t);
            }

            _refs.FogBaseMeanFreePath = Mathf.Lerp(from.fogMeanFreePath, to.fogMeanFreePath, t);
            if (_refs.Fog != null)
            {
                float ground = _refs.Slope.HeightAt(x);
                _refs.Fog.meanFreePath.Override(_refs.FogBaseMeanFreePath);
                _refs.Fog.baseHeight.Override(ground + Mathf.Lerp(from.fogBaseHeight, to.fogBaseHeight, t));
                _refs.Fog.maximumHeight.Override(ground + Mathf.Lerp(from.fogMaximumHeight, to.fogMaximumHeight, t));
                _refs.Fog.anisotropy.Override(Mathf.Lerp(from.fogAnisotropy, to.fogAnisotropy, t));
                Palette.TryParse(from.fogAlbedo, out float ar, out float ag, out float ab);
                Palette.TryParse(to.fogAlbedo, out float br, out float bg, out float bb);
                _refs.Fog.albedo.Override(Color.Lerp(new Color(ar, ag, ab, 1f), new Color(br, bg, bb, 1f), t));
            }
            if (_exposure != null) _exposure.fixedExposure.Override(Mathf.Lerp(from.exposureEV, to.exposureEV, t));

            // Wind for the scarf (HazardPresenter reads SliceRefs.Look).
            BiomeLook look = b.Dominant == b.From ? from : to;
            look.windExtraX = Mathf.Lerp(from.windExtraX, to.windExtraX, t);
            look.windExtraZ = Mathf.Lerp(from.windExtraZ, to.windExtraZ, t);
            _refs.Look = look;

            if (!_hasDominant || b.Dominant != Dominant)
            {
                Dominant = b.Dominant;
                _hasDominant = true;
                if (_camera != null) _camera.SetBiome(Dominant);
            }
        }
    }
}
