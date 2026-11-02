using System.IO;
using Hushlands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Hushlands.Editor
{
    /// <summary>
    /// Idempotent T0 setup: configs, HDRP asset, project settings, boot scene, build list.
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Hushlands.Editor.ProjectBootstrap.Run
    /// </summary>
    public static class ProjectBootstrap
    {
        [MenuItem("Hushlands/Setup/Run Project Bootstrap")]
        public static void Run()
        {
            EnsureFolder(HushPaths.ConfigResources);
            EnsureFolder(HushPaths.RenderingFolder);
            EnsureFolder(Path.GetDirectoryName(HushPaths.BootScene));

            ProjectTargetsConfig targets = EnsureAsset<ProjectTargetsConfig>(HushPaths.ProjectTargetsAsset);
            BootLookConfig look = EnsureAsset<BootLookConfig>(HushPaths.BootLookAsset);

            ApplyProjectSettings(targets);
            HDRenderPipelineAsset hdrp = EnsureHdrpAsset(look);
            GraphicsSettings.defaultRenderPipeline = hdrp;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = hdrp;
            }

            VolumeProfile profile = EnsureBootVolumeProfile(look, targets);
            EnsureBootScene(targets, look, profile);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(HushPaths.BootScene, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Hushlands] Bootstrap complete.");
        }

        internal static void ApplyProjectSettings(ProjectTargetsConfig targets)
        {
            Time.fixedDeltaTime = targets.fixedTimestep;
            Time.maximumDeltaTime = targets.maximumAllowedTimestep;
            var timeManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TimeManager.asset"));
            timeManager.FindProperty("Fixed Timestep").floatValue = targets.fixedTimestep;
            timeManager.FindProperty("Maximum Allowed Timestep").floatValue = targets.maximumAllowedTimestep;
            timeManager.ApplyModifiedPropertiesWithoutUndo();

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.productName = HushPaths.ProductName;
            PlayerSettings.companyName = HushPaths.CompanyName;
            PlayerSettings.defaultScreenWidth = targets.targetWidth;
            PlayerSettings.defaultScreenHeight = targets.targetHeight;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        }

        private static HDRenderPipelineAsset EnsureHdrpAsset(BootLookConfig look)
        {
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(HushPaths.HdrpAsset);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, HushPaths.HdrpAsset);
            }
            var so = new SerializedObject(asset);
            SetBool(so, "m_RenderPipelineSettings.supportVolumetrics", true);
            SetBool(so, "m_RenderPipelineSettings.dynamicResolutionSettings.enabled", true);
            SetBool(so, "m_RenderPipelineSettings.dynamicResolutionSettings.enableDLSS", true);
            SetFloat(so, "m_RenderPipelineSettings.dynamicResolutionSettings.minPercentage", look.minScreenPercent);
            SetFloat(so, "m_RenderPipelineSettings.dynamicResolutionSettings.maxPercentage", look.maxScreenPercent);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static VolumeProfile EnsureBootVolumeProfile(BootLookConfig look, ProjectTargetsConfig targets)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HushPaths.BootVolumeProfile);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, HushPaths.BootVolumeProfile);
            }

            if (!profile.TryGet(out Fog fog)) fog = profile.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(look.fogMeanFreePath);
            fog.baseHeight.Override(look.fogBaseHeight);
            fog.maximumHeight.Override(look.fogMaximumHeight);
            fog.anisotropy.Override(look.fogAnisotropy);
            fog.albedo.Override(targets.mistBlue);

            if (!profile.TryGet(out Exposure exposure)) exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(look.fixedExposureEV);

            foreach (VolumeComponent component in profile.components)
            {
                if (!AssetDatabase.IsSubAsset(component)) AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void EnsureBootScene(ProjectTargetsConfig targets, BootLookConfig look, VolumeProfile profile)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = look.fieldOfView;
            cameraGo.transform.SetPositionAndRotation(look.cameraPosition, Quaternion.Euler(look.cameraEuler));
            cameraGo.AddComponent<HDAdditionalCameraData>();
            cameraGo.AddComponent<FrameTimeCapture>().Configure(targets, false);

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = look.sunTint;
            sun.lightUnit = LightUnit.Lux;
            sun.intensity = look.sunIlluminanceLux;
            sunGo.transform.rotation = Quaternion.Euler(look.sunPitch, look.sunYaw, 0f);

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Slope Placeholder";

            EditorSceneManager.SaveScene(scene, HushPaths.BootScene);
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) throw new System.InvalidOperationException("HDRP property missing: " + path);
            p.boolValue = value;
        }

        private static void SetFloat(SerializedObject so, string path, float value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) throw new System.InvalidOperationException("HDRP property missing: " + path);
            p.floatValue = value;
        }
    }
}
