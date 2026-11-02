using Hushlands.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Hushlands.Tests
{
    public sealed class ConfigAssetTests
    {
        [Test]
        public void ConfigSet_Exists_AllLinked_AllValid()
        {
            var set = AssetDatabase.LoadAssetAtPath<HushConfigSet>(HushPaths.ConfigSetAsset);
            Assert.IsNotNull(set, "Run Hushlands/Setup/Run Project Bootstrap");
            Assert.IsNotNull(HushConfigSet.Load());
            var errors = set.ValidateAll();
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Services_BuildFromConfig()
        {
            var set = HushConfigSet.Load();
            var services = new HushServices(set, new Hushlands.Core.ConstantSlope(1000, 0.2f, Hushlands.Core.SurfaceId.Grass));
            Assert.IsNotNull(services.Rider);
            Assert.IsNotNull(services.Camera);
            Assert.AreEqual(set.speed.Settings.minSpeed, services.Speed.MinSpeed);
        }
    }
}
