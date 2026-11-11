using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Shows humor beats as small placeholder props behind the track, a little ahead of the rider: a kettle that
    /// puffs, a signpost that tips its hat, a cairn that snores. Original procedural stand-ins for authored art.
    /// </summary>
    public sealed class HumorPresenter : MonoBehaviour
    {
        private HumorDirector _humor;
        private DirectorSettings _s;
        private SliceRefs _refs;
        private SliceAssets _assets;
        private GameObject _prop;
        private Transform _moving;
        private Vector3 _movingBase;
        private string _id;

        /// <summary>Beats shown so far.</summary>
        public int Shown { get; private set; }
        public bool Showing => _prop != null;
        public string LastLine { get; private set; }

        public void Initialize(HumorDirector humor, DirectorSettings settings, SliceRefs refs, SliceAssets assets)
        {
            _humor = humor;
            _s = settings;
            _refs = refs;
            _assets = assets;
            humor.BeatStarted += Show;
            humor.BeatEnded += Hide;
        }

        private void OnDestroy()
        {
            if (_humor == null) return;
            _humor.BeatStarted -= Show;
            _humor.BeatEnded -= Hide;
        }

        private void Show(HumorBeat beat)
        {
            Hide();
            Shown++;
            LastLine = beat.Line;
            _id = beat.Id;
            float k = _s.humorPropScale;
            double x = beat.Distance;
            double z = _s.humorDepthMeters;
            _prop = new GameObject("Humor_" + beat.Id);
            _prop.transform.SetParent(transform, false);
            _prop.transform.position = new Vector3((float)x, (float)_refs.Terrain.Height(x, z), (float)z);
            if (beat.Id == "HatTip")
            {
                GameObject post = SliceBuilder.Primitive(PrimitiveType.Cylinder, "Post", _prop.transform, _assets.Get("Wood"));
                post.transform.localScale = new Vector3(0.12f * k, 0.8f * k, 0.12f * k);
                post.transform.localPosition = new Vector3(0f, 0.8f * k, 0f);
                GameObject hat = SliceBuilder.Primitive(PrimitiveType.Sphere, "Hat", _prop.transform, _assets.Get("Scarf"));
                hat.transform.localScale = new Vector3(0.6f * k, 0.22f * k, 0.6f * k);
                _movingBase = new Vector3(0f, 1.7f * k, 0f);
                _moving = hat.transform;
            }
            else if (beat.Id == "SnoozingCairn")
            {
                GameObject low = SliceBuilder.Primitive(PrimitiveType.Sphere, "Stone", _prop.transform, _assets.Get("Stone"));
                low.transform.localScale = new Vector3(0.9f * k, 0.55f * k, 0.8f * k);
                low.transform.localPosition = new Vector3(0f, 0.25f * k, 0f);
                GameObject top = SliceBuilder.Primitive(PrimitiveType.Sphere, "Stone", _prop.transform, _assets.Get("Stone"));
                top.transform.localScale = new Vector3(0.6f * k, 0.4f * k, 0.55f * k);
                _movingBase = new Vector3(0f, 0.65f * k, 0f);
                _moving = top.transform;
            }
            else
            {
                GameObject kettle = SliceBuilder.Primitive(PrimitiveType.Sphere, "Kettle", _prop.transform, _assets.Get("Bell"));
                kettle.transform.localScale = new Vector3(0.6f * k, 0.5f * k, 0.6f * k);
                kettle.transform.localPosition = new Vector3(0f, 0.3f * k, 0f);
                GameObject puff = SliceBuilder.Primitive(PrimitiveType.Sphere, "Puff", _prop.transform, _assets.Get("Feather"));
                puff.transform.localScale = Vector3.one * (0.3f * k);
                _movingBase = new Vector3(0.35f * k, 0.7f * k, 0f);
                _moving = puff.transform;
            }
            _moving.localPosition = _movingBase;
        }

        private void Hide()
        {
            if (_prop != null) Destroy(_prop);
            _prop = null;
            _moving = null;
        }

        private void LateUpdate()
        {
            if (_prop == null || _moving == null || _humor == null) return;
            float phase = _humor.Elapsed * _s.humorCycleHz;
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            float k = _s.humorPropScale;
            if (_id == "HatTip")
            {
                // The hat lifts and tilts toward the rider, then settles.
                _moving.localPosition = _movingBase + new Vector3(0f, Mathf.Max(0f, wave) * 0.25f * k, 0f);
                _moving.localRotation = Quaternion.Euler(0f, 0f, Mathf.Max(0f, wave) * 25f);
            }
            else if (_id == "SnoozingCairn")
            {
                // The top stone rises and falls like slow breathing.
                _moving.localPosition = _movingBase + new Vector3(0f, (wave * 0.5f + 0.5f) * 0.08f * k, 0f);
            }
            else
            {
                // A steam puff rises from the spout and starts again.
                float rise = phase - Mathf.Floor(phase);
                _moving.localPosition = _movingBase + new Vector3(0f, rise * 0.9f * k, 0f);
                _moving.localScale = Vector3.one * ((0.2f + 0.35f * rise) * k);
            }
        }
    }
}
