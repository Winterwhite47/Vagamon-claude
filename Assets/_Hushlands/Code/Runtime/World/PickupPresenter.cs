using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Placeholder pickup visuals that follow PickupSystem (magnet pull) and shrink away when collected.</summary>
    public sealed class PickupPresenter : MonoBehaviour
    {
        private readonly List<Transform> _views = new List<Transform>();
        private readonly List<float> _shrink = new List<float>();
        private PickupSystem _system;
        private PickupSettings _s;
        private float _size;

        public int ViewCount => _views.Count;

        public void Initialize(PickupSystem system, PickupSettings settings, SliceAssets assets, float diameter)
        {
            _system = system;
            _s = settings;
            _size = diameter;
            foreach (PickupInstance p in system.Items)
            {
                GameObject g = SliceBuilder.Primitive(Shape(p.Kind), "Pickup_" + p.Kind + "_" + p.Index, transform,
                    assets.Get(MaterialFor(p.Kind)));
                g.transform.position = new Vector3((float)p.X, (float)p.Y, 0f);
                g.transform.localScale = Vector3.one * (diameter * Scale(p.Kind, settings));
                _views.Add(g.transform);
                _shrink.Add(1f);
            }
        }

        public static string MaterialFor(PickupId id)
        {
            switch (id)
            {
                case PickupId.MistLantern: return "Lantern";
                case PickupId.WindFeather: return "Feather";
                case PickupId.TeaFlask: return "Tea";
                case PickupId.MagnetBell: return "Bell";
                default: return "Pickup";
            }
        }

        private static PrimitiveType Shape(PickupId id)
        {
            return id == PickupId.SpiceCoin || id == PickupId.GoldenMistbell ? PrimitiveType.Sphere : PrimitiveType.Capsule;
        }

        private static float Scale(PickupId id, PickupSettings s)
        {
            if (id == PickupId.GoldenMistbell) return s.mistbellVisualScale;
            return id == PickupId.SpiceCoin ? 1f : s.timedVisualScale;
        }

        private void LateUpdate()
        {
            if (_system == null) return;
            IReadOnlyList<PickupInstance> items = _system.Items;
            float rate = Time.deltaTime / Mathf.Max(_s.collectAnimSeconds, 1e-3f);
            for (int i = 0; i < items.Count; i++)
            {
                Transform v = _views[i];
                if (v == null || !v.gameObject.activeSelf) continue;
                PickupInstance p = items[i];
                v.position = new Vector3((float)p.X, (float)p.Y, 0f);
                if (!p.Collected) continue;
                _shrink[i] = Mathf.Max(0f, _shrink[i] - rate);
                v.localScale = Vector3.one * (_size * Scale(p.Kind, _s) * _shrink[i]);
                if (_shrink[i] <= 0f) v.gameObject.SetActive(false);
            }
        }
    }
}
