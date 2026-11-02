using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Editor-built HDRP materials (from LookSettings.materials), looked up by name at runtime.</summary>
    [CreateAssetMenu(menuName = "Hushlands/World/Slice Assets", fileName = "SliceAssets")]
    public sealed class SliceAssets : ScriptableObject
    {
        public const string ResourcePath = "Hushlands/SliceAssets";

        [Serializable]
        public struct Entry
        {
            public string name;
            public Material material;
        }

        public List<Entry> materials = new List<Entry>();

        public static SliceAssets Load()
        {
            return Resources.Load<SliceAssets>(ResourcePath);
        }

        public Material Get(string name)
        {
            foreach (Entry e in materials)
            {
                if (e.name == name) return e.material;
            }
            throw new KeyNotFoundException("Slice material missing: " + name + " (run Hushlands/Setup/Run Project Bootstrap)");
        }
    }
}
