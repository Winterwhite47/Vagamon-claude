using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Hushlands
{
    /// <summary>
    /// Placeholder hazard visuals: a dust-and-earth wall at the landslide front (with a terracotta dust volume),
    /// ground slabs over collapse zones that crack (stone) during the warning and vanish when open, and squall
    /// effects (denser global fog within the mist-fairness check, scarf gusts).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class HazardPresenter : MonoBehaviour
    {
        private RunSession _session;
        private SliceRefs _refs;
        private HazardSettings _h;
        private ClothSettings _cloth;
        private Transform _wall;
        private readonly List<MeshRenderer> _slabs = new List<MeshRenderer>();
        private Material _intact;
        private Material _cracked;

        public int SlabCount => _slabs.Count;
        public float WallX => _wall != null ? _wall.position.x : float.NaN;

        public void Initialize(RunSession session, SliceRefs refs, HazardSettings hazards, ClothSettings cloth, LookSettings look,
            TrackSettings track, SliceAssets assets)
        {
            _session = session;
            _refs = refs;
            _h = hazards;
            _cloth = cloth;
            _intact = assets.Get("Track");
            _cracked = assets.Get("Stone");

            _wall = new GameObject("LandslideFront").transform;
            _wall.SetParent(transform, false);
            GameObject earth = SliceBuilder.Primitive(PrimitiveType.Cube, "Earth", _wall, assets.Get("Track"));
            earth.transform.localPosition = new Vector3(-look.mistVolumeDepth * 0.25f, look.mistVolumeHeight * 0.25f, 0f);
            earth.transform.localScale = new Vector3(look.mistVolumeDepth * 0.5f, look.mistVolumeHeight * 0.5f, look.mistVolumeDepth * 0.5f);
            var dust = new GameObject("Dust").AddComponent<LocalVolumetricFog>();
            dust.transform.SetParent(_wall, false);
            dust.transform.localPosition = new Vector3(0f, look.mistVolumeHeight * 0.5f, 0f);
            Palette.TryParse(LookSettings.Terracotta, out float r, out float g, out float b);
            dust.parameters.albedo = new Color(r, g, b, 1f);
            dust.parameters.meanFreePath = look.mistMeanFreePathAtFull;
            dust.parameters.size = new Vector3(look.mistVolumeDepth * 0.5f, look.mistVolumeHeight, look.mistVolumeDepth);

            foreach (CollapseController.Zone z in session.Collapse.Zones)
            {
                double mid = (z.Start + z.End) * 0.5;
                float angle = refs.Slope.AngleAt(mid);
                GameObject slab = SliceBuilder.Primitive(PrimitiveType.Cube, "CollapseSlab", transform, _intact);
                slab.transform.SetPositionAndRotation(new Vector3((float)mid, refs.Slope.HeightAt(mid), 0f),
                    Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg));
                slab.transform.localScale = new Vector3((float)(z.End - z.Start), track.collapseSlabThickness, look.trackStripHalfWidth * 2f);
                _slabs.Add(slab.GetComponent<MeshRenderer>());
            }
        }

        private void LateUpdate()
        {
            if (_session == null) return;
            LandslideChaser slide = _session.Landslide;
            _wall.gameObject.SetActive(slide.Active);
            double front = slide.Front;
            _wall.position = new Vector3((float)front, _refs.Slope.HeightAt(front), 0f);

            IReadOnlyList<CollapseController.Zone> zones = _session.Collapse.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                CollapseState st = zones[i].State;
                _slabs[i].gameObject.SetActive(st != CollapseState.Open);
                _slabs[i].sharedMaterial = st == CollapseState.Warning ? _cracked : _intact;
            }

            SquallDirector squall = _session.Squall;
            if (_refs.Fog != null) _refs.Fog.meanFreePath.Override(_refs.FogBaseMeanFreePath * squall.FogScale);
            if (_refs.Scarf != null && _refs.Scarf.Solver != null)
            {
                float k = squall.Intensity;
                float bx = _cloth.windX + _refs.Look.windExtraX;
                float bz = _cloth.windZ + _refs.Look.windExtraZ;
                _refs.Scarf.Solver.SetWind(
                    Mathf.Lerp(bx, _h.squallWindX, k),
                    Mathf.Lerp(_cloth.windY, _h.squallWindY, k),
                    Mathf.Lerp(bz, _h.squallWindZ, k));
            }
        }
    }
}
