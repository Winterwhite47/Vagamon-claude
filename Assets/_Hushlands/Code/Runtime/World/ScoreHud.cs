using Hushlands.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hushlands
{
    /// <summary>Near-zero HUD: score and coins appear when they change and fade out after hudIdleSeconds. Placeholder until T13.</summary>
    public sealed class ScoreHud : MonoBehaviour
    {
        private ScoreService _score;
        private UiMotionSettings _motion;
        private CanvasGroup _group;
        private Text _label;
        private float _idle;
        private bool _reduced;

        public float Alpha => _group != null ? _group.alpha : 0f;
        /// <summary>UI layer multiplier (0 outside the Riding screen).</summary>
        public float Visibility { get; set; } = 1f;
        public bool ReducedMotion { set => _reduced = value; }
        public string Text => _label != null ? _label.text : string.Empty;

        public void Initialize(ScoreService score, UiMotionSettings motion, bool reducedMotion)
        {
            _score = score;
            _motion = motion;
            _reduced = reducedMotion;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            var go = new GameObject("Score", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 48;
            _label.alignment = TextAnchor.UpperRight;
            _label.color = Color.white;
            _label.raycastTarget = false;
            RectTransform rt = _label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-96f, -72f);
            rt.sizeDelta = new Vector2(1200f, 120f);
            _score.Changed += _ => Show();
            Show();
            _idle = _motion.hudIdleSeconds;
        }

        private void Show()
        {
            _label.text = _score.Score.ToString("N0") + "    " + _score.SpiceCoins + " spice";
            _idle = 0f;
        }

        private void Update()
        {
            if (_score == null) return;
            _idle += Time.unscaledDeltaTime;
            float target = _idle < _motion.hudIdleSeconds ? 1f : 0f;
            float fade = _motion.TransitionSeconds(_reduced);
            _group.alpha = Mathf.MoveTowards(_group.alpha, target * Visibility, Time.unscaledDeltaTime / Mathf.Max(fade, 1e-3f));
        }
    }
}
