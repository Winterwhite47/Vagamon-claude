using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Hushlands.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hushlands
{
    /// <summary>
    /// Motion-graphics UI over the live world: Title, Riding (near-zero HUD), Pause, Summary, Garage, Goals, Settings.
    /// Screens crossfade and glide (UiFlow); reduced motion keeps fades only. Placeholder procedural layout and the
    /// legacy runtime font until authored typography; every string, size and colour comes from UiSettings.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class UiDirector : MonoBehaviour
    {
        private sealed class ScreenView
        {
            public UiScreen Screen;
            public CanvasGroup Group;
            public RectTransform Content;
            public Image Panel;
            public UiMenu Menu;
            public readonly List<Text> Rows = new List<Text>();
        }

        private readonly Dictionary<UiScreen, ScreenView> _views = new Dictionary<UiScreen, ScreenView>();
        private readonly List<string> _completedGoals = new List<string>();
        private readonly List<string> _unlocked = new List<string>();
        private UiSettings _ui;
        private VehicleSettings _vehicles;
        private UiMotionSettings _motion;
        private AccessibilityOptions _options;
        private ProgressionService _progression;
        private RunDirector _run;
        private IGameControl _game;
        private UiInput _input;
        private ScoreHud _hud;
        private Font _font;
        private Image _wipe;
        private bool _vehicleChanged;
        private bool _reloading;

        public UiFlow Flow { get; private set; }
        public UiInput Input => _input;
        public float WipeAlpha => _wipe != null ? _wipe.color.a : 0f;

        public void Initialize(UiSettings ui, UiMotionSettings motion, VehicleSettings vehicles, AccessibilityOptions options,
            ProgressionService progression, RunDirector run, IGameControl game, ScoreHud hud, UiScreen start, bool coverOnStart)
        {
            _ui = ui;
            _vehicles = vehicles;
            _motion = motion;
            _options = options;
            _progression = progression;
            _run = run;
            _game = game;
            _hud = hud;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _input = gameObject.AddComponent<UiInput>();
            Flow = new UiFlow(motion, ui, start) { ReducedMotion = options.ReducedMotion };
            Flow.Changed += (a, b) => Rebuild(b);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(_ui.referenceWidth, _ui.referenceHeight);

            foreach (UiScreen s in new[] { UiScreen.Title, UiScreen.Riding, UiScreen.Pause, UiScreen.Summary, UiScreen.Garage, UiScreen.Goals, UiScreen.Settings })
                _views[s] = MakeView(s);

            _wipe = Child("MistWipe", transform).AddComponent<Image>();
            Stretch(_wipe.rectTransform);
            Palette.TryParse(LookSettings.MistBlue, out float r, out float g, out float b);
            _wipe.color = new Color(r, g, b, coverOnStart ? 1f : 0f);
            _wipe.raycastTarget = false;

            progression.Goals.Completed += st => _completedGoals.Add(st.Text);
            progression.Garage.Unlocked += id => _unlocked.Add(Names.Pretty(id.ToString()));
            run.RunEnded += _ => Flow.Go(UiScreen.Summary, false);
            options.Changed += () =>
            {
                Flow.ReducedMotion = _options.ReducedMotion;
                _game.ApplyOptions();
                Rebuild(Flow.Current);
            };
            Rebuild(start);
            if (coverOnStart) StartCoroutine(FadeWipe(0f));
        }

        public void Inject(UiCommand cmd)
        {
            _input.Inject(cmd);
        }

        // ---------- layout ----------

        private ScreenView MakeView(UiScreen s)
        {
            var v = new ScreenView { Screen = s };
            GameObject root = Child(s.ToString(), transform);
            Stretch(root.GetComponent<RectTransform>());
            v.Group = root.AddComponent<CanvasGroup>();
            v.Group.alpha = 0f;
            v.Group.blocksRaycasts = false;
            v.Panel = Child("Panel", root.transform).AddComponent<Image>();
            RectTransform pr = v.Panel.rectTransform;
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(_ui.panelWidthFraction, 1f);
            pr.offsetMin = pr.offsetMax = Vector2.zero;
            v.Panel.raycastTarget = false;
            v.Content = Child("Content", root.transform).GetComponent<RectTransform>();
            Stretch(v.Content);
            return v;
        }

        private void Rebuild(UiScreen screen)
        {
            if (!_views.TryGetValue(screen, out ScreenView v)) return;
            for (int i = v.Content.childCount - 1; i >= 0; i--) Destroy(v.Content.GetChild(i).gameObject);
            v.Rows.Clear();
            bool hc = _options.HighContrast;
            Palette.TryParse(hc ? _ui.contrastPanel : _ui.panel, out float pr, out float pg, out float pb);
            v.Panel.color = new Color(pr, pg, pb, hc ? _ui.contrastPanelAlpha : _ui.panelAlpha);
            v.Panel.enabled = screen != UiScreen.Riding;

            float y = -_ui.marginX * 0.5f;
            switch (screen)
            {
                case UiScreen.Title:
                    y = Label(v, _ui.title, _ui.titleSize, y, false);
                    y = Label(v, _ui.tagline, _ui.bodySize, y, true);
                    break;
                case UiScreen.Pause:
                    y = Label(v, _ui.pauseTitle, _ui.headingSize, y, false);
                    break;
                case UiScreen.Summary:
                    y = Label(v, _ui.summaryTitle, _ui.headingSize, y, false);
                    y = SummaryLines(v, y);
                    break;
                case UiScreen.Garage:
                    y = Label(v, _ui.garage, _ui.headingSize, y, false);
                    break;
                case UiScreen.Goals:
                    y = Label(v, _ui.goals, _ui.headingSize, y, false);
                    foreach (GoalStatus g in _progression.Goals.Active(_run.Session != null ? _run.Session.Stats : null))
                        y = Label(v, g.Text + "   " + Mathf.Min((float)g.Progress, g.Target).ToString("0", CultureInfo.InvariantCulture) + " / " + g.Target, _ui.bodySize, y, true);
                    break;
                case UiScreen.Settings:
                    y = Label(v, _ui.settings, _ui.headingSize, y, false);
                    break;
            }

            v.Menu = new UiMenu(BuildMenu(screen));
            y += _ui.menuTop * _options.TextScale;
            foreach (UiMenuItem item in v.Menu.Items)
            {
                Text t = MakeText(v.Content, RowText(item), _ui.bodySize, y);
                v.Rows.Add(t);
                y -= _ui.rowSpacing * _options.TextScale;
            }
            if (screen == UiScreen.Title) Label(v, _ui.controlsHint, _ui.hintSize, y - _ui.rowSpacing, true);
            PaintFocus(v);
        }

        private float SummaryLines(ScreenView v, float y)
        {
            RunSummary s = _run.Summary;
            CultureInfo c = CultureInfo.InvariantCulture;
            if (_progression.Profile.NewBest) y = Label(v, _ui.newBest, _ui.bodySize, y, false);
            y = Label(v, _ui.summaryScore + "   " + s.Score.ToString("N0", c), _ui.bodySize, y, true);
            y = Label(v, _ui.summaryDistance + "   " + s.Distance.ToString("0", c) + " m", _ui.bodySize, y, true);
            y = Label(v, _ui.summaryCoins + "   " + s.Coins, _ui.bodySize, y, true);
            y = Label(v, _ui.summaryRescued + "   " + s.Rescues, _ui.bodySize, y, true);
            if (_progression.Profile.GroupsWaiting > 0)
                y = Label(v, _ui.summaryWaiting + "   " + _progression.Profile.GroupsWaiting, _ui.bodySize, y, true);
            foreach (string g in _completedGoals) y = Label(v, g, _ui.hintSize, y, true);
            foreach (string u in _unlocked) y = Label(v, u, _ui.hintSize, y, true);
            return y;
        }

        private List<UiMenuItem> BuildMenu(UiScreen s)
        {
            var items = new List<UiMenuItem>();
            switch (s)
            {
                case UiScreen.Title:
                    items.Add(new UiMenuItem { Label = _ui.ride, Action = MenuAction.Ride });
                    items.Add(new UiMenuItem { Label = _ui.garage, Action = MenuAction.Garage });
                    items.Add(new UiMenuItem { Label = _ui.goals, Action = MenuAction.Goals });
                    items.Add(new UiMenuItem { Label = _ui.settings, Action = MenuAction.Settings });
                    break;
                case UiScreen.Pause:
                    items.Add(new UiMenuItem { Label = _ui.resume, Action = MenuAction.Resume });
                    items.Add(new UiMenuItem { Label = _ui.settings, Action = MenuAction.Settings });
                    items.Add(new UiMenuItem { Label = _ui.toTitle, Action = MenuAction.ToTitle });
                    break;
                case UiScreen.Summary:
                    items.Add(new UiMenuItem { Label = _ui.rideAgain, Action = MenuAction.RideAgain });
                    items.Add(new UiMenuItem { Label = _ui.garage, Action = MenuAction.Garage });
                    items.Add(new UiMenuItem { Label = _ui.toTitle, Action = MenuAction.ToTitle });
                    break;
                case UiScreen.Garage:
                    Garage garage = _progression.Garage;
                    foreach (VehicleEntry entry in _vehicles.entries)
                    {
                        VehicleEntry e = entry;
                        items.Add(new UiMenuItem
                        {
                            Label = Names.Pretty(e.vehicle.ToString()), Action = MenuAction.Select, Value = (int)e.vehicle,
                            Detail = () => garage.Selected == e.vehicle ? _ui.selected
                                : (garage.IsUnlocked(e.vehicle) ? "" : string.Format(CultureInfo.InvariantCulture, _ui.locked, e.unlockLifetimeCoins)),
                        });
                    }
                    items.Add(new UiMenuItem { Label = _ui.back, Action = MenuAction.Back });
                    break;
                case UiScreen.Goals:
                    items.Add(new UiMenuItem { Label = _ui.back, Action = MenuAction.Back });
                    break;
                case UiScreen.Settings:
                    items.Add(Toggle(_ui.reducedMotion, 0, () => _options.ReducedMotion));
                    items.Add(new UiMenuItem { Label = _ui.textSize, Action = MenuAction.Cycle, Value = 1,
                        Detail = () => Mathf.RoundToInt(_options.TextScale * 100f) + "%" });
                    items.Add(Toggle(_ui.highContrast, 2, () => _options.HighContrast));
                    items.Add(Toggle(_ui.gentleTiming, 3, () => _options.GentleTiming));
                    items.Add(Toggle(_ui.autoAssists, 4, () => _options.AutoAssists));
                    items.Add(new UiMenuItem { Label = _ui.masterVolume, Action = MenuAction.Cycle, Value = 5,
                        Detail = () => Mathf.RoundToInt(_options.Volume * 100f) + "%" });
                    items.Add(new UiMenuItem { Label = _ui.back, Action = MenuAction.Back });
                    break;
            }
            return items;
        }

        private UiMenuItem Toggle(string label, int id, System.Func<bool> value)
        {
            return new UiMenuItem { Label = label, Action = MenuAction.Toggle, Value = id, Detail = () => value() ? _ui.on : _ui.off };
        }

        private static string RowText(UiMenuItem item)
        {
            string d = item.Detail != null ? item.Detail() : null;
            return string.IsNullOrEmpty(d) ? item.Label : item.Label + "   " + d;
        }

        private float Label(ScreenView v, string text, float size, float y, bool muted)
        {
            MakeText(v.Content, text, size, y).color = Colour(muted ? (_options.HighContrast ? _ui.contrastText : _ui.textMuted) : (_options.HighContrast ? _ui.contrastText : _ui.text));
            return y - size * _ui.lineHeight * _options.TextScale;
        }

        private Text MakeText(RectTransform parent, string text, float size, float y)
        {
            Text t = Child("Text", parent).AddComponent<Text>();
            t.font = _font;
            t.fontSize = Mathf.RoundToInt(size * _options.TextScale);
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.color = Colour(_options.HighContrast ? _ui.contrastText : _ui.text);
            t.raycastTarget = false;
            t.text = text;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(_ui.marginX, y);
            rt.sizeDelta = new Vector2(_ui.referenceWidth * _ui.panelWidthFraction - _ui.marginX, size * _ui.lineHeight * _options.TextScale);
            return t;
        }

        private void PaintFocus(ScreenView v)
        {
            if (v.Menu == null) return;
            for (int i = 0; i < v.Rows.Count; i++)
            {
                bool f = i == v.Menu.Focus;
                v.Rows[i].text = RowText(v.Menu.Items[i]);
                v.Rows[i].color = Colour(f ? _ui.focus : (_options.HighContrast ? _ui.contrastText : _ui.text));
            }
        }

        private static Color Colour(string hex)
        {
            Palette.TryParse(hex, out float r, out float g, out float b);
            return new Color(r, g, b, 1f);
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
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------- per frame ----------

        private void Update()
        {
            if (Flow == null) return;
            Flow.Tick(Time.unscaledDeltaTime);
            foreach (ScreenView v in _views.Values)
            {
                v.Group.alpha = Flow.Alpha(v.Screen);
                v.Content.anchoredPosition = new Vector2(Flow.Offset(v.Screen), 0f);
            }
            if (_hud != null) _hud.Visibility = Flow.Alpha(UiScreen.Riding);
            if (_reloading) return;

            ScreenView cur = _views[Flow.Current];
            HandleMouse(cur);
            while (_input.TryNext(out UiCommand cmd)) Handle(cur, cmd);
        }

        private void HandleMouse(ScreenView v)
        {
            if (v.Menu == null || Flow.Current == UiScreen.Riding) return;
            for (int i = 0; i < v.Rows.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(v.Rows[i].rectTransform, _input.MousePosition, null)) continue;
                if (i != v.Menu.Focus)
                {
                    v.Menu.SetFocus(i);
                    PaintFocus(v);
                }
                if (_input.ConsumeClick()) Activate(v, v.Menu.Focused, 1);
                return;
            }
            _input.ConsumeClick();
        }

        private void Handle(ScreenView v, UiCommand cmd)
        {
            if (Flow.Current == UiScreen.Riding)
            {
                if (cmd == UiCommand.Back || cmd == UiCommand.Pause) PauseGame();
                return;
            }
            switch (cmd)
            {
                case UiCommand.Up:
                    v.Menu.Move(-1);
                    PaintFocus(v);
                    break;
                case UiCommand.Down:
                    v.Menu.Move(1);
                    PaintFocus(v);
                    break;
                case UiCommand.Left:
                    Activate(v, v.Menu.Focused, -1, true);
                    break;
                case UiCommand.Right:
                    Activate(v, v.Menu.Focused, 1, true);
                    break;
                case UiCommand.Submit:
                    Activate(v, v.Menu.Focused, 1);
                    break;
                case UiCommand.Back:
                case UiCommand.Pause:
                    GoBack();
                    break;
            }
        }

        private void Activate(ScreenView v, UiMenuItem item, int dir, bool valueOnly = false)
        {
            if (item == null) return;
            if (valueOnly && item.Action != MenuAction.Cycle && item.Action != MenuAction.Toggle) return;
            switch (item.Action)
            {
                case MenuAction.Ride:
                    _game.StartRide();
                    Flow.Go(UiScreen.Riding, false);
                    break;
                case MenuAction.RideAgain:
                    Reload(true);
                    break;
                case MenuAction.Garage:
                    Flow.Go(UiScreen.Garage, true);
                    break;
                case MenuAction.Goals:
                    Flow.Go(UiScreen.Goals, true);
                    break;
                case MenuAction.Settings:
                    Flow.Go(UiScreen.Settings, true);
                    break;
                case MenuAction.Resume:
                    ResumeGame();
                    break;
                case MenuAction.ToTitle:
                    Reload(false);
                    break;
                case MenuAction.Back:
                    GoBack();
                    break;
                case MenuAction.Select:
                    if (_progression.Garage.Select((VehicleId)item.Value)) _vehicleChanged = true;
                    PaintFocus(v);
                    break;
                case MenuAction.Toggle:
                    ToggleOption(item.Value);
                    break;
                case MenuAction.Cycle:
                    if (item.Value == 1) _options.CycleTextScale(dir);
                    else _options.StepVolume(dir);
                    break;
            }
        }

        private void ToggleOption(int id)
        {
            switch (id)
            {
                case 0: _options.SetReducedMotion(!_options.ReducedMotion); break;
                case 2: _options.SetHighContrast(!_options.HighContrast); break;
                case 3: _options.SetGentleTiming(!_options.GentleTiming); break;
                default: _options.SetAutoAssists(!_options.AutoAssists); break;
            }
        }

        private void GoBack()
        {
            if (Flow.Current == UiScreen.Pause)
            {
                ResumeGame();
                return;
            }
            bool leavingGarage = Flow.Current == UiScreen.Garage;
            if (!Flow.Back()) return;
            // A new vehicle shows on the title only after rebuilding the world, hidden by the mist.
            if (leavingGarage && _vehicleChanged && Flow.Current == UiScreen.Title) Reload(false);
        }

        private void PauseGame()
        {
            _game.SetPaused(true);
            Flow.Go(UiScreen.Pause, true);
        }

        private void ResumeGame()
        {
            _game.SetPaused(false);
            Flow.Go(UiScreen.Riding, false);
        }

        private void Reload(bool autoRide)
        {
            if (_reloading) return;
            _reloading = true;
            StartCoroutine(ReloadRoutine(autoRide));
        }

        private IEnumerator ReloadRoutine(bool autoRide)
        {
            if (_run != null && !_run.Finished && Flow.Current != UiScreen.Title) _run.FinishRun();
            yield return FadeWipe(1f);
            SceneHandoff.CoverOnStart = true;
            SceneHandoff.AutoRide = autoRide;
            _game.Reload(autoRide);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private IEnumerator FadeWipe(float target)
        {
            float seconds = _options.ReducedMotion ? _motion.reducedMotionTransitionSeconds : _motion.mistCrossfadeSeconds;
            Color c = _wipe.color;
            float start = c.a;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                c.a = Mathf.Lerp(start, target, Ease.InOutCubic(t / seconds));
                _wipe.color = c;
                yield return null;
            }
            c.a = target;
            _wipe.color = c;
        }
    }
}
