using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Placeholder nests (base, ducklings, cover) beside the track and a covered basket on the sled.
    /// Ducklings are only ever children of a nest or the basket and have no physics, so they cannot be shown falling.
    /// Covers and lid shut on the crash step; DucklingSafetyAudit runs every frame and logs any violation as an error.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class DucklingPresenter : MonoBehaviour
    {
        private sealed class NestView
        {
            public int Group;
            public Transform Root;
            public GameObject Cover;
            public List<GameObject> Ducks = new List<GameObject>();
        }

        private readonly List<NestView> _nests = new List<NestView>();
        private readonly List<GameObject> _basketDucks = new List<GameObject>();
        private readonly HashSet<Transform> _safeParents = new HashSet<Transform>();
        private DucklingNestService _service;
        private RunFlow _flow;
        private IRider _rider;
        private DucklingSettings _s;
        private GameObject _lid;
        private Transform _basket;

        public int NestCount => _nests.Count;
        public int Violations { get; private set; }
        public DucklingView LastView { get; private set; }

        public void Initialize(DucklingNestService service, DucklingLoop loop, RunFlow flow, IRider rider, Transform sled,
            ISlopeSpline slope, DucklingSettings settings, TrackSettings track, SliceAssets assets)
        {
            _service = service;
            _flow = flow;
            _rider = rider;
            _s = settings;
            Material duck = assets.Get("Duckling");
            foreach (NestSite site in loop.Sites)
            {
                var v = new NestView { Group = site.Group };
                v.Root = new GameObject("Nest_" + site.Group).transform;
                v.Root.SetParent(transform, false);
                v.Root.position = new Vector3((float)site.X, slope.HeightAt(site.X), track.propSideOffset);
                GameObject bowl = SliceBuilder.Primitive(PrimitiveType.Cylinder, "Bowl", v.Root, assets.Get("Nest"));
                bowl.transform.localScale = new Vector3(track.nestDiameter, track.nestHeight, track.nestDiameter);
                v.Cover = SliceBuilder.Primitive(PrimitiveType.Sphere, "Cover", v.Root, assets.Get("Wood"));
                v.Cover.transform.localScale = new Vector3(track.nestDiameter, track.nestDiameter * 0.5f, track.nestDiameter);
                for (int i = 0; i < settings.ducklingsPerGroup; i++)
                {
                    float a = i * Mathf.PI * 2f / settings.ducklingsPerGroup;
                    GameObject d = SliceBuilder.Primitive(PrimitiveType.Sphere, "Duckling", v.Root, duck);
                    d.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (track.nestDiameter * 0.25f) +
                                                Vector3.up * track.nestHeight;
                    d.transform.localScale = Vector3.one * settings.ducklingSize;
                    v.Ducks.Add(d);
                }
                _safeParents.Add(v.Root);
                _nests.Add(v);
            }

            _basket = new GameObject("Basket").transform;
            _basket.SetParent(sled, false);
            _basket.localPosition = new Vector3(-track.sledWidth * 0.5f, track.sledDeckThickness + settings.ducklingSize, 0f);
            GameObject box = SliceBuilder.Primitive(PrimitiveType.Cube, "Box", _basket, assets.Get("Wood"));
            box.transform.localScale = new Vector3(track.sledWidth * 0.6f, settings.ducklingSize, track.sledWidth * 0.8f);
            _lid = SliceBuilder.Primitive(PrimitiveType.Cube, "Lid", _basket, assets.Get("Wood"));
            _lid.transform.localPosition = Vector3.up * settings.ducklingSize;
            _lid.transform.localScale = new Vector3(track.sledWidth * 0.62f, settings.ducklingSize * 0.3f, track.sledWidth * 0.82f);
            for (int i = 0; i < settings.basketMaxShown; i++)
            {
                GameObject d = SliceBuilder.Primitive(PrimitiveType.Sphere, "BasketDuckling", _basket, duck);
                d.transform.localPosition = new Vector3((i % 3 - 1) * settings.ducklingSize, settings.ducklingSize * 0.8f,
                    (i / 3 - 0.5f) * settings.ducklingSize);
                d.transform.localScale = Vector3.one * settings.ducklingSize;
                _basketDucks.Add(d);
            }
            _safeParents.Add(_basket);
            Refresh();
        }

        private void LateUpdate()
        {
            if (_service != null) Refresh();
        }

        public void Refresh()
        {
            bool closed = _service.NestCoversClosed;
            var view = new DucklingView { NestCoversVisible = true, BasketLidVisible = closed ? true : _lid.activeSelf };
            foreach (NestView v in _nests)
            {
                bool showDucks = !closed && _service.StatusOf(v.Group) != DucklingGroupStatus.Rescued;
                v.Cover.SetActive(closed);
                if (closed && !v.Cover.activeSelf) view.NestCoversVisible = false;
                foreach (GameObject d in v.Ducks)
                {
                    d.SetActive(showDucks);
                    if (showDucks) view.DrawnInNests++;
                    if (d.activeInHierarchy && !_safeParents.Contains(d.transform.parent)) view.DrawnOutsideSafePlaces++;
                }
            }

            _lid.SetActive(closed);
            view.BasketLidVisible = !closed || _lid.activeSelf;
            int rescued = _service.Count(DucklingGroupStatus.Rescued) * _s.ducklingsPerGroup;
            int show = closed ? 0 : Mathf.Min(rescued, _basketDucks.Count);
            for (int i = 0; i < _basketDucks.Count; i++)
            {
                GameObject d = _basketDucks[i];
                d.SetActive(i < show);
                if (i < show) view.DrawnInBasket++;
                if (d.activeInHierarchy && !_safeParents.Contains(d.transform.parent)) view.DrawnOutsideSafePlaces++;
            }

            LastView = view;
            List<string> problems = DucklingSafetyAudit.Check(_service, _flow.Phase, _rider.State, view);
            if (problems.Count > 0)
            {
                Violations += problems.Count;
                Debug.LogError("[Hushlands] Duckling safety: " + string.Join("; ", problems));
            }
        }
    }
}
