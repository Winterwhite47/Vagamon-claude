using System.Linq;
using Hushlands.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Hushlands.Tests
{
    /// <summary>T0 acceptance checks against project state after ProjectBootstrap.Run.</summary>
    public sealed class ProjectSetupTests
    {
        private ProjectTargetsConfig _targets;

        [OneTimeSetUp]
        public void Setup()
        {
            _targets = AssetDatabase.LoadAssetAtPath<ProjectTargetsConfig>(HushPaths.ProjectTargetsAsset);
        }

        [Test]
        public void Configs_ExistInResources()
        {
            Assert.IsNotNull(_targets, "ProjectTargetsConfig missing; run Hushlands/Setup/Run Project Bootstrap");
            Assert.IsNotNull(ProjectTargetsConfig.Load());
            Assert.IsNotNull(BootLookConfig.Load());
        }

        [Test]
        public void Canon_Defaults()
        {
            var fresh = ScriptableObject.CreateInstance<ProjectTargetsConfig>();
            Assert.AreEqual(0.01f, fresh.fixedTimestep, 1e-6f);
            Assert.AreEqual(3840, fresh.targetWidth);
            Assert.AreEqual(2160, fresh.targetHeight);
            Assert.AreEqual(60, fresh.targetFrameRate);
            Assert.AreEqual(16.6f, fresh.frameBudgetMs, 1e-4f);
            Assert.AreEqual("#0B3B17", "#" + ColorUtility.ToHtmlStringRGB(fresh.emerald));
            Assert.AreEqual("#8D3F2D", "#" + ColorUtility.ToHtmlStringRGB(fresh.terracotta));
            Assert.AreEqual("#4A6B82", "#" + ColorUtility.ToHtmlStringRGB(fresh.mistBlue));
            Assert.AreEqual("#D4AF37", "#" + ColorUtility.ToHtmlStringRGB(fresh.gold));
            Object.DestroyImmediate(fresh);
        }

        [Test]
        public void FixedTimestep_MatchesConfig()
        {
            Assert.AreEqual(_targets.fixedTimestep, Time.fixedDeltaTime, 1e-6f);
        }

        [Test]
        public void Hdrp_IsActive_WithVolumetricsAndUpscaling()
        {
            var hdrp = GraphicsSettings.defaultRenderPipeline as HDRenderPipelineAsset;
            Assert.IsNotNull(hdrp, "HDRP is not the default render pipeline");
            RenderPipelineSettings s = hdrp.currentPlatformRenderPipelineSettings;
            Assert.IsTrue(s.supportVolumetrics);
            Assert.IsTrue(s.dynamicResolutionSettings.enabled);
        }

        [Test]
        public void Player_TargetsWindows4KLinear()
        {
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
            Assert.AreEqual(_targets.targetWidth, PlayerSettings.defaultScreenWidth);
            Assert.AreEqual(_targets.targetHeight, PlayerSettings.defaultScreenHeight);
            Assert.AreEqual(UnityEngine.Rendering.GraphicsDeviceType.Direct3D12,
                PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64).First());
        }

        [Test]
        public void BootScene_InBuildSettings()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.path == HushPaths.BootScene && s.enabled));
        }

        [Test]
        public void CoreAssembly_HasNoEngineReference()
        {
            var core = typeof(Hushlands.Core.IRider).Assembly;
            Assert.IsFalse(core.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEngine")));
        }
    }
}
