using Hushlands.Core;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Hushlands.Editor
{
    /// <summary>Builds slice materials (HDRP/Lit from LookSettings), the SliceAssets registry and the slice scene.</summary>
    public static class SliceMenu
    {
        [MenuItem("Hushlands/Slice/Build Meadow Sea Slice")]
        public static void BuildFromMenu()
        {
            BuildAll(null);
            EditorSceneManager.OpenScene(HushPaths.SliceScene);
        }

        public static void BuildAll(HushConfigSet set)
        {
            if (set == null) set = AssetDatabase.LoadAssetAtPath<HushConfigSet>(HushPaths.ConfigSetAsset);
            if (set == null || set.look == null) throw new System.InvalidOperationException("Config set missing; run bootstrap.");
            BuildMaterials(set.look.Settings);
            // One slice per tuned biome (T7 Meadow Sea, T14 one biome at a time).
            foreach (BiomeLook look in set.look.Settings.biomes)
            {
                if (look.tuned) BuildScene(look.biome);
            }
        }

        public static System.Collections.Generic.List<string> SliceScenes(HushConfigSet set)
        {
            var scenes = new System.Collections.Generic.List<string> { HushPaths.SliceSceneFor(BiomeId.MeadowSea) };
            foreach (BiomeLook look in set.look.Settings.biomes)
            {
                string path = HushPaths.SliceSceneFor(look.biome);
                if (look.tuned && !scenes.Contains(path)) scenes.Add(path);
            }
            return scenes;
        }

        public static SliceAssets BuildMaterials(LookSettings look)
        {
            ProjectBootstrap.EnsureFolder(HushPaths.MaterialsFolder);
            SliceAssets assets = ProjectBootstrap.EnsureAsset<SliceAssets>(HushPaths.SliceAssetsAsset);
            assets.materials.Clear();
            Shader lit = Shader.Find("HDRP/Lit");
            if (lit == null) throw new System.InvalidOperationException("HDRP/Lit shader not found");
            foreach (MaterialSpec spec in look.materials)
            {
                string path = HushPaths.MaterialsFolder + "/M_" + spec.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(lit) { name = "M_" + spec.name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                Palette.TryParse(spec.color, out float r, out float g, out float b);
                mat.SetColor("_BaseColor", new Color(r, g, b, 1f));
                mat.SetFloat("_Smoothness", spec.smoothness);
                mat.SetFloat("_Metallic", spec.metallic);
                HDMaterial.ValidateMaterial(mat);
                EditorUtility.SetDirty(mat);
                assets.materials.Add(new SliceAssets.Entry { name = spec.name, material = mat });
            }
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            return assets;
        }

        [MenuItem("Hushlands/Slice/Build Windmouth Ridge Slice")]
        public static void BuildWindmouthFromMenu()
        {
            BuildAll(null);
            EditorSceneManager.OpenScene(HushPaths.SliceSceneFor(BiomeId.WindmouthRidge));
        }

        [MenuItem("Hushlands/Slice/Build Pine Hollow Slice")]
        public static void BuildPineFromMenu()
        {
            BuildAll(null);
            EditorSceneManager.OpenScene(HushPaths.SliceSceneFor(BiomeId.PineHollow));
        }

        [MenuItem("Hushlands/Slice/Build Terrace Country Slice")]
        public static void BuildTerraceFromMenu()
        {
            BuildAll(null);
            EditorSceneManager.OpenScene(HushPaths.SliceSceneFor(BiomeId.TerraceCountry));
        }

        public static void BuildScene(BiomeId biome)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<HDAdditionalCameraData>();
            cameraGo.AddComponent<CinemachineBrain>();
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<AudioLowPassFilter>();
            MistLowPass lowPass = cameraGo.AddComponent<MistLowPass>();
            AudioMixBridge bridge = cameraGo.AddComponent<AudioMixBridge>();
            cameraGo.AddComponent<FrameTimeCapture>().Configure(AssetDatabase.LoadAssetAtPath<ProjectTargetsConfig>(HushPaths.ProjectTargetsAsset), false);

            var vcamGo = new GameObject("Gameplay Camera");
            CinemachineCamera vcam = vcamGo.AddComponent<CinemachineCamera>();
            CameraDirector director = vcamGo.AddComponent<CameraDirector>();
            director.AssignCamera(vcam);

            var slice = new GameObject("Slice");
            slice.AddComponent<SliceBootstrap>().Assign(director, lowPass, bridge, biome);

            EditorSceneManager.SaveScene(scene, HushPaths.SliceSceneFor(biome));
        }
    }
}
