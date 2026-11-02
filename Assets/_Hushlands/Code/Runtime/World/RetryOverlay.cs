using Hushlands.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hushlands
{
    /// <summary>
    /// Near-zero HUD failure overlay over the live world: the calm line and hint fade in during FailureHold, and a mist
    /// wipe peaks exactly when the rider is moved (no visible teleport). Reduced motion shortens fades.
    /// Placeholder typography until T13.
    /// </summary>
    public sealed class RetryOverlay : MonoBehaviour
    {
        private CanvasGroup _text;
        private Image _wipe;
        private Text _line;
        private Text _hint;
        private RunFlow _flow;
        private UiMotionSettings _motion;
        private FailureSettings _failure;
        private bool _reduced;
        private float _textAlpha;

        public float TextAlpha => _textAlpha;
        public bool ReducedMotion { set => _reduced = value; }
        public float WipeAlpha => _wipe != null ? _wipe.color.a : 0f;
        public string Line => _line != null ? _line.text : string.Empty;

        public void Initialize(RunFlow flow, UiMotionSettings motion, FailureSettings failure, string mistHex, bool reducedMotion)
        {
            _flow = flow;
            _motion = motion;
            _failure = failure;
            _reduced = reducedMotion;
            BuildCanvas(mistHex);
            _flow.MessageShown += m =>
            {
                _line.text = m.Text;
                _hint.text = m.Hint;
            };
        }

        private void BuildCanvas(string mistHex)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);

            Palette.TryParse(mistHex, out float r, out float g, out float b);
            _wipe = Child("MistWipe", transform).AddComponent<Image>();
            Stretch(_wipe.rectTransform);
            _wipe.color = new Color(r, g, b, 0f);
            _wipe.raycastTarget = false;

            GameObject group = Child("Message", transform);
            Stretch(group.GetComponent<RectTransform>());
            _text = group.AddComponent<CanvasGroup>();
            _text.alpha = 0f;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _line = Label(group.transform, font, 72, new Vector2(0f, 60f));
            _hint = Label(group.transform, font, 44, new Vector2(0f, -60f));
        }

        private static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text Label(Transform parent, Font font, int size, Vector2 offset)
        {
            Text t = Child("Label", parent).AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(3000f, 200f);
            rt.anchoredPosition = offset;
            return t;
        }

        private void Update()
        {
            if (_flow == null) return;
            float fade = _motion.TransitionSeconds(_reduced);
            float target = _flow.Phase == FlowPhase.FailureHold ? 1f : 0f;
            _textAlpha = Mathf.MoveTowards(_textAlpha, target, Time.unscaledDeltaTime / Mathf.Max(fade, 1e-3f));
            _text.alpha = _textAlpha;

            float wipe = 0f;
            if (_flow.Phase == FlowPhase.Retrying)
            {
                float u = _flow.PhaseTime / Mathf.Max(_failure.retryTransitionSeconds, 1e-3f);
                float peak = _failure.retryTeleportFraction;
                wipe = u <= peak ? u / Mathf.Max(peak, 1e-3f) : (1f - u) / Mathf.Max(1f - peak, 1e-3f);
                wipe = Mathf.Clamp01(wipe);
            }
            Color c = _wipe.color;
            c.a = wipe;
            _wipe.color = c;
        }
    }
}
