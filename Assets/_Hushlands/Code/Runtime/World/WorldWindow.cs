using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Keeps only the backdrop near the rider switched on (terrain tiles, scatter buckets, water, mist volumes), so the
    /// 5 km journey costs about what one slice costs. Gameplay objects (track, hazards, nests, pickups) are never culled.
    /// </summary>
    public sealed class WorldWindow : MonoBehaviour
    {
        private readonly List<GameObject> _items = new List<GameObject>();
        private WindowCuller _culler;
        private Transform _rider;

        public int Count => _culler != null ? _culler.Count : 0;
        public int OnCount => _culler != null ? _culler.OnCount : 0;

        public void Initialize(LookSettings look)
        {
            _culler = new WindowCuller(look.windowAheadMeters, look.windowBehindMeters, look.windowHysteresisMeters);
        }

        /// <summary>The window follows this transform's x.</summary>
        public void Follow(Transform rider)
        {
            _rider = rider;
            Refresh();
        }

        /// <summary>The object spans [min, max] along the run; it stays off until the window reaches it.</summary>
        public void Register(GameObject item, double min, double max)
        {
            _culler.Add(min, max);
            _items.Add(item);
            item.SetActive(false);
        }

        /// <summary>Applies the window at the rider's position now (start, and after a retry teleport).</summary>
        public void Refresh()
        {
            if (_culler == null || _rider == null) return;
            _culler.Update(_rider.position.x, Switch);
        }

        private void Switch(int index, bool on)
        {
            if (_items[index] != null) _items[index].SetActive(on);
        }

        private void LateUpdate()
        {
            Refresh();
        }
    }
}
