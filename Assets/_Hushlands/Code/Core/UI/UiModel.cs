using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>WCAG relative-luminance contrast ratio between two hex colours.</summary>
    public static class Contrast
    {
        public static double Ratio(string a, string b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            double hi = Math.Max(la, lb);
            double lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        public static double Luminance(string hex)
        {
            Palette.TryParse(hex, out float r, out float g, out float b);
            return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
        }

        private static double Lin(double c)
        {
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }

    public enum UiScreen
    {
        None = 0,
        Title = 1,
        Riding = 2,
        Pause = 3,
        Summary = 4,
        Garage = 5,
        Goals = 6,
        Settings = 7,
    }

    public enum UiCommand
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
        Submit = 5,
        Back = 6,
        Pause = 7,
    }

    /// <summary>Smooth, video-like easing (cubic in-out). Continuous, monotonic, 0 to 1.</summary>
    public static class Ease
    {
        public static float InOutCubic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }
    }

    /// <summary>
    /// Screen flow with crossfading transitions over the live world. Each change blends the old screen out and the new
    /// one in over the UiMotion duration (shorter in reduced motion) with an eased slide (none in reduced motion).
    /// Back returns along a stack. Gameplay pausing is reported through <see cref="GameplayPaused"/>.
    /// </summary>
    public sealed class UiFlow
    {
        private readonly UiMotionSettings _motion;
        private readonly UiSettings _ui;
        private readonly Stack<UiScreen> _back = new Stack<UiScreen>();
        private float _t;
        private float _duration;

        public UiFlow(UiMotionSettings motion, UiSettings ui, UiScreen start)
        {
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            Current = start;
            Previous = UiScreen.None;
            _t = _duration = 0f;
        }

        public UiScreen Current { get; private set; }
        public UiScreen Previous { get; private set; }
        public bool ReducedMotion { get; set; }
        public bool Transitioning => _t < _duration;
        public float Progress => _duration <= 0f ? 1f : Ease.InOutCubic(_t / _duration);
        public bool GameplayPaused => Current == UiScreen.Pause;
        public event Action<UiScreen, UiScreen> Changed;

        public void Go(UiScreen next, bool remember)
        {
            if (next == Current) return;
            if (remember) _back.Push(Current);
            else _back.Clear();
            Previous = Current;
            Current = next;
            _t = 0f;
            _duration = _motion.TransitionSeconds(ReducedMotion);
            Changed?.Invoke(Previous, Current);
        }

        public bool Back()
        {
            if (_back.Count == 0) return false;
            UiScreen to = _back.Pop();
            Previous = Current;
            Current = to;
            _t = 0f;
            _duration = _motion.TransitionSeconds(ReducedMotion);
            Changed?.Invoke(Previous, Current);
            return true;
        }

        public void Tick(float unscaledDt)
        {
            if (_t < _duration) _t = Math.Min(_duration, _t + unscaledDt);
        }

        /// <summary>Opacity of a screen right now (outgoing fades out while incoming fades in).</summary>
        public float Alpha(UiScreen s)
        {
            float p = Progress;
            if (s == Current) return p;
            if (s == Previous && Transitioning) return 1f - p;
            return 0f;
        }

        /// <summary>Horizontal slide in reference pixels (0 when settled or in reduced motion).</summary>
        public float Offset(UiScreen s)
        {
            if (ReducedMotion) return 0f;
            float p = Progress;
            if (s == Current) return (1f - p) * _ui.slidePixels;
            if (s == Previous && Transitioning) return -p * _ui.slidePixels;
            return 0f;
        }
    }

    public enum MenuAction
    {
        Ride = 0,
        RideAgain = 1,
        Garage = 2,
        Goals = 3,
        Settings = 4,
        Resume = 5,
        ToTitle = 6,
        Back = 7,
        Toggle = 8,
        Cycle = 9,
        Select = 10,
    }

    public sealed class UiMenuItem
    {
        public string Label;
        public MenuAction Action;
        public int Value;
        public bool Enabled = true;
        public Func<string> Detail;
    }

    /// <summary>Vertical menu: Up/Down move focus (wrapping, skipping disabled rows); Left/Right/Submit act.</summary>
    public sealed class UiMenu
    {
        public UiMenu(IEnumerable<UiMenuItem> items)
        {
            Items = new List<UiMenuItem>(items);
            Focus = FirstEnabled();
        }

        public List<UiMenuItem> Items { get; }
        public int Focus { get; private set; }
        public UiMenuItem Focused => Focus >= 0 && Focus < Items.Count ? Items[Focus] : null;

        public void Move(int dir)
        {
            if (Items.Count == 0) return;
            for (int i = 0; i < Items.Count; i++)
            {
                Focus = (Focus + dir + Items.Count) % Items.Count;
                if (Items[Focus].Enabled) return;
            }
        }

        public void SetFocus(int index)
        {
            if (index >= 0 && index < Items.Count && Items[index].Enabled) Focus = index;
        }

        private int FirstEnabled()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Enabled) return i;
            }
            return -1;
        }
    }

    /// <summary>Player accessibility and comfort options, persisted in the save.</summary>
    public sealed class AccessibilityOptions
    {
        public const string Prefix = "options.";
        private readonly ISaveService _save;
        private readonly UiSettings _ui;

        public AccessibilityOptions(ISaveService save, UiSettings ui, UiMotionSettings motion)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            ReducedMotion = Load(nameof(ReducedMotion), motion.reducedMotionDefault);
            TextScaleIndex = Clamp(LoadInt(nameof(TextScaleIndex), 0), ui.textScales.Length);
            HighContrast = Load(nameof(HighContrast), false);
            GentleTiming = Load(nameof(GentleTiming), false);
            AutoAssists = Load(nameof(AutoAssists), true);
            VolumeIndex = Clamp(LoadInt(nameof(VolumeIndex), ui.volumeSteps.Length - 1), ui.volumeSteps.Length);
        }

        public bool ReducedMotion { get; private set; }
        public int TextScaleIndex { get; private set; }
        public bool HighContrast { get; private set; }
        public bool GentleTiming { get; private set; }
        public bool AutoAssists { get; private set; }
        public int VolumeIndex { get; private set; }
        public float TextScale => _ui.textScales[TextScaleIndex];
        public float Volume => _ui.volumeSteps[VolumeIndex];
        public event Action Changed;

        /// <summary>Hold threshold after gentle timing (taps may be held a little longer and still jump).</summary>
        public float HoldThreshold(float baseSeconds)
        {
            return GentleTiming ? baseSeconds * _ui.gentleHoldScale : baseSeconds;
        }

        public void SetReducedMotion(bool v) { ReducedMotion = v; Store(nameof(ReducedMotion), v); }
        public void SetHighContrast(bool v) { HighContrast = v; Store(nameof(HighContrast), v); }
        public void SetGentleTiming(bool v) { GentleTiming = v; Store(nameof(GentleTiming), v); }
        public void SetAutoAssists(bool v) { AutoAssists = v; Store(nameof(AutoAssists), v); }

        public void CycleTextScale(int dir)
        {
            TextScaleIndex = (TextScaleIndex + dir + _ui.textScales.Length) % _ui.textScales.Length;
            _save.Save(Prefix + nameof(TextScaleIndex), TextScaleIndex);
            Changed?.Invoke();
        }

        public void StepVolume(int dir)
        {
            VolumeIndex = Math.Max(0, Math.Min(_ui.volumeSteps.Length - 1, VolumeIndex + dir));
            _save.Save(Prefix + nameof(VolumeIndex), VolumeIndex);
            Changed?.Invoke();
        }

        private void Store(string key, bool v)
        {
            _save.Save(Prefix + key, v);
            Changed?.Invoke();
        }

        private bool Load(string key, bool fallback)
        {
            return _save.TryLoad(Prefix + key, out bool v) ? v : fallback;
        }

        private int LoadInt(string key, int fallback)
        {
            return _save.TryLoad(Prefix + key, out int v) ? v : fallback;
        }

        private static int Clamp(int v, int count)
        {
            return v < 0 ? 0 : (v >= count ? count - 1 : v);
        }
    }
}
