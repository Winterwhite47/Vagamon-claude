// API surface of UnityEditor used by Hushlands, mirrored from Unity-Technologies/UnityCsReference @ 6000.0:
// Modules/AssetDatabase/Editor/ScriptBindings/AssetDatabase.bindings.cs, Editor/Mono/EditorSceneManager.bindings.cs,
// Editor/Mono/EditorBuildSettings.bindings.cs, Editor/Mono/PlayerSettings.bindings.cs, Editor/Mono/BuildPipeline.bindings.cs,
// Editor/Mono/BuildPlayerWindowBuildMethods.cs, Editor/Mono/BuildPipeline/NamedBuildTarget.cs,
// Modules/BuildReportingEditor/Managed/BuildSummary.cs, Editor/Mono/{EditorUtility,EditorApplication,SerializedObject,
// SerializedProperty}.bindings.cs, Editor/Mono/Undo/Undo.bindings.cs, Editor/Mono/MenuItem.cs, Editor/Mono/BuildTarget.cs.
// Documented-only (wrapper file not located in the reference source): EditorSceneManager.SaveScene, NewSceneSetup,
// HDMaterial.ValidateMaterial (HDRP 17 Editor), InitializeOnLoadAttribute. Compile-time only; never shipped.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string assetPath) where T : Object { return null; }
        public static Object LoadMainAssetAtPath(string assetPath) { return null; }
        public static void CreateAsset(Object asset, string path) { }
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static bool IsValidFolder(string path) { return false; }
        public static bool IsSubAsset(Object obj) { return false; }
        public static void ImportAsset(string path) { }
        public static string CreateFolder(string parentFolder, string newFolderName) { return null; }
        public static void AddObjectToAsset(Object objectToAdd, Object assetObject) { }
        public static void AddObjectToAsset(Object objectToAdd, string path) { }
        public static Object[] LoadAllAssetsAtPath(string assetPath) { return null; }
    }

    public sealed class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public enum BuildTarget { StandaloneWindows64 = 19 }
    public enum BuildTargetGroup { Standalone = 1 }
    [Flags] public enum BuildOptions { None = 0 }
    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1 }

    public static class PlayerSettings
    {
        public static ColorSpace colorSpace { get; set; }
        public static int defaultScreenWidth { get; set; }
        public static int defaultScreenHeight { get; set; }
        public static FullScreenMode fullScreenMode { get; set; }
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static void SetGraphicsAPIs(BuildTarget platform, UnityEngine.Rendering.GraphicsDeviceType[] apis) { }
        public static UnityEngine.Rendering.GraphicsDeviceType[] GetGraphicsAPIs(BuildTarget platform) { return null; }
        public static void SetUseDefaultGraphicsAPIs(BuildTarget platform, bool automatic) { }
        public static void SetScriptingBackend(Build.NamedBuildTarget buildTarget, ScriptingImplementation backend) { }
    }

    public struct BuildPlayerOptions
    {
        public string[] scenes { get; set; }
        public string locationPathName { get; set; }
        public BuildTargetGroup targetGroup { get; set; }
        public BuildTarget target { get; set; }
        public BuildOptions options { get; set; }
    }

    public static class BuildPipeline
    {
        public static Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions buildPlayerOptions) { return null; }
    }

    public static class EditorUtility
    {
        public static void SetDirty(Object target) { }
    }

    public static class EditorApplication
    {
        public delegate void CallbackFunction();
        public static CallbackFunction delayCall;
        public static void Exit(int returnValue) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class InitializeOnLoadAttribute : Attribute { }

    public class SerializedProperty
    {
        public bool boolValue { get; set; }
        public float floatValue { get; set; }
        public int intValue { get; set; }
    }

    public class SerializedObject
    {
        public SerializedObject(Object obj) { }
        public SerializedProperty FindProperty(string propertyPath) { return null; }
        public bool ApplyModifiedPropertiesWithoutUndo() { return false; }
    }

    public static class Undo
    {
        public static void RecordObject(Object objectToUndo, string name) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
    }
}

namespace UnityEditor.Build
{
    public readonly struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Standalone = default;
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }

    public struct BuildSummary
    {
        public ulong totalSize { get; }
        public int totalErrors { get; }
        public int totalWarnings { get; }
        public BuildResult result { get; }
    }

    public sealed class BuildReport : Object
    {
        public BuildSummary summary { get; }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }
    public enum NewSceneMode { Single = 0, Additive = 1 }
    public enum OpenSceneMode { Single = 0, Additive = 1, AdditiveWithoutLoading = 2 }

    public static class EditorSceneManager
    {
        public static Scene OpenScene(string scenePath, OpenSceneMode mode = OpenSceneMode.Single) { return default; }
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode = NewSceneMode.Single) { return default; }
        public static bool SaveScene(Scene scene, string dstScenePath = "", bool saveAsCopy = false) { return false; }
    }
}

namespace UnityEditor.Rendering.HighDefinition
{
    public static class HDMaterial
    {
        public static void ValidateMaterial(Material material) { }
    }
}
