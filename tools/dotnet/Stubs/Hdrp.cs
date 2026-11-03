// API surface of com.unity.render-pipelines.high-definition used by Hushlands, mirrored from
// Unity-Technologies/Graphics @ 6000.0/staging (Runtime/Lighting/AtmosphericScattering/Fog.cs,
// Runtime/Lighting/VolumetricLighting/LocalVolumetricFog.cs, Runtime/PostProcessing/Components/{Exposure,Tonemapping}.cs,
// Runtime/Sky/{VisualEnvironment,SkySettings}.cs, Runtime/Sky/PhysicallyBasedSky/PhysicallyBasedSky.cs,
// Runtime/RenderPipeline/Camera/HDAdditionalCameraData.cs, Runtime/RenderPipeline/HDRenderPipelineAsset.cs,
// Runtime/RenderPipeline/Settings/RenderPipelineSettings.cs, core Runtime/Common/GlobalDynamicResolutionSettings.cs).
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering
{
    public struct GlobalDynamicResolutionSettings
    {
        public bool enabled;
        public float maxPercentage;
        public float minPercentage;
        public bool enableDLSS;
    }
}

namespace UnityEngine.Rendering.HighDefinition
{
    public class Fog : VolumeComponent
    {
        public BoolParameter enabled;
        public FloatParameter baseHeight;
        public FloatParameter maximumHeight;
        public MinFloatParameter meanFreePath;
        public BoolParameter enableVolumetricFog;
        public ColorParameter albedo;
        public ClampedFloatParameter anisotropy;
    }

    public enum ExposureMode { Fixed = 0, Automatic = 1, AutomaticHistogram = 4, CurveMapping = 2, UsePhysicalCamera = 3 }

    public sealed class ExposureModeParameter : VolumeParameter<ExposureMode> { }

    public sealed class Exposure : VolumeComponent
    {
        public ExposureModeParameter mode;
        public FloatParameter fixedExposure;
    }

    public enum TonemappingMode { None, Neutral, ACES, Custom, External }

    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }

    public sealed class Tonemapping : VolumeComponent
    {
        public TonemappingModeParameter mode;
    }

    public enum SkyType { HDRI = 1, Procedural = 2, Gradient = 3, PhysicallyBased = 4 }

    public sealed class VisualEnvironment : VolumeComponent
    {
        public NoInterpIntParameter skyType;
    }

    public abstract class SkySettings : VolumeComponent { }

    public class PhysicallyBasedSky : SkySettings { }

    public struct LocalVolumetricFogArtistParameters
    {
        public Color albedo;
        public float meanFreePath;
        public Vector3 size;
    }

    public class LocalVolumetricFog : MonoBehaviour
    {
        public LocalVolumetricFogArtistParameters parameters;
    }

    public class HDAdditionalCameraData : MonoBehaviour { }

    public struct RenderPipelineSettings
    {
        public bool supportVolumetrics;
        public GlobalDynamicResolutionSettings dynamicResolutionSettings;
    }

    public class HDRenderPipelineAsset : RenderPipelineAsset
    {
        public RenderPipelineSettings currentPlatformRenderPipelineSettings { get; set; }
        public override RenderPipeline CreatePipeline() { return null; }
    }
}
