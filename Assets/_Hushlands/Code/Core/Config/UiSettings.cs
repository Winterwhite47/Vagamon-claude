using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>All UI text, layout and colour (T13). Text is calm and checked like failure lines.</summary>
    [Serializable]
    public sealed class UiSettings : ISettings
    {
        // --- Text ---
        public string title = "Hushlands";
        public string tagline = "Beyond the mist, someone is waiting for you to come home.";
        public string ride = "Ride";
        public string rideAgain = "Ride again";
        public string garage = "Garage";
        public string goals = "Goals";
        public string settings = "Settings";
        public string resume = "Resume";
        public string toTitle = "Back to the ridge";
        public string back = "Back";
        public string pauseTitle = "A quiet moment";
        public string summaryTitle = "Home for now";
        public string summaryScore = "Score";
        public string summaryDistance = "Distance";
        public string summaryCoins = "Spice coins";
        public string summaryRescued = "Little ones brought home";
        public string summaryWaiting = "Still waiting for you";
        public string newBest = "A new best";
        public string locked = "Unlocks at {0} lifetime spice";
        public string selected = "Riding";
        public string reducedMotion = "Reduced motion";
        public string textSize = "Text size";
        public string highContrast = "High contrast";
        public string gentleTiming = "Gentle tap timing";
        public string autoAssists = "Free assists after three tries";
        public string masterVolume = "Volume";
        public string on = "On";
        public string off = "Off";
        public string controlsHint = "Tap to jump, hold to tuck or flip";

        // --- Layout (reference 3840 x 2160) ---
        public float titleSize = 140f;
        public float headingSize = 88f;
        public float bodySize = 56f;
        public float hintSize = 40f;
        public float rowSpacing = 96f;
        public float menuTop = -120f;
        public float marginX = 240f;
        public float slidePixels = 120f;
        public float lineHeight = 1.5f;
        public float panelWidthFraction = 0.42f;
        public float referenceWidth = 3840f;
        public float referenceHeight = 2160f;
        public float[] textScales = { 1f, 1.25f, 1.5f };
        public float[] volumeSteps = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        /// <summary>Gentle timing multiplies the tap/hold threshold (longer taps still jump).</summary>
        public float gentleHoldScale = 1.6f;

        // --- Colour ---
        public string text = "#F4F1EA";
        public string textMuted = "#C9D3D9";
        public string focus = LookSettings.Gold;
        public string panel = "#1C2A33";
        public float panelAlpha = 0.55f;
        public string contrastText = "#FFFFFF";
        public string contrastPanel = "#000000";
        public float contrastPanelAlpha = 0.85f;
        /// <summary>WCAG ratios: normal text must reach 4.5:1 on its panel, high contrast 7:1.</summary>
        public float minContrast = 4.5f;
        public float minHighContrast = 7f;

        public IEnumerable<string> AllText()
        {
            yield return title; yield return tagline; yield return ride; yield return rideAgain; yield return garage;
            yield return goals; yield return settings; yield return resume; yield return toTitle; yield return back;
            yield return pauseTitle; yield return summaryTitle; yield return summaryScore; yield return summaryDistance;
            yield return summaryCoins; yield return summaryRescued; yield return summaryWaiting; yield return newBest;
            yield return string.Format(System.Globalization.CultureInfo.InvariantCulture, locked, 1200);
            yield return selected; yield return reducedMotion; yield return textSize; yield return highContrast;
            yield return gentleTiming; yield return autoAssists; yield return masterVolume; yield return on; yield return off;
            yield return controlsHint;
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(UiSettings);
            var m = new MessageSettings();
            foreach (string t in AllText())
            {
                if (!m.IsClean(t)) e.Add(o + ": text not clean or too long: '" + t + "'");
            }
            foreach (KeyValuePair<string, float> kv in new[]
                     {
                         new KeyValuePair<string, float>(nameof(titleSize), titleSize),
                         new KeyValuePair<string, float>(nameof(headingSize), headingSize),
                         new KeyValuePair<string, float>(nameof(bodySize), bodySize),
                         new KeyValuePair<string, float>(nameof(hintSize), hintSize),
                         new KeyValuePair<string, float>(nameof(rowSpacing), rowSpacing),
                         new KeyValuePair<string, float>(nameof(marginX), marginX),
                     })
            {
                SettingsCheck.Positive(e, o, kv.Key, kv.Value);
            }
            SettingsCheck.NonNegative(e, o, nameof(slidePixels), slidePixels);
            SettingsCheck.Range(e, o, nameof(lineHeight), lineHeight, 1f, 3f);
            SettingsCheck.Range(e, o, nameof(panelWidthFraction), panelWidthFraction, 0.2f, 1f);
            SettingsCheck.Positive(e, o, nameof(referenceWidth), referenceWidth);
            SettingsCheck.Positive(e, o, nameof(referenceHeight), referenceHeight);
            if (textScales == null || textScales.Length == 0 || textScales[0] != 1f) e.Add(o + ": textScales must start at 1");
            if (volumeSteps == null || volumeSteps.Length < 2) e.Add(o + ": need volume steps");
            if (gentleHoldScale < 1f) e.Add(o + ".gentleHoldScale must be >= 1");
            foreach (string hex in new[] { text, textMuted, focus, panel, contrastText, contrastPanel })
            {
                if (!Palette.TryParse(hex, out _, out _, out _)) e.Add(o + ": bad colour " + hex);
            }
            SettingsCheck.Range(e, o, nameof(panelAlpha), panelAlpha, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(contrastPanelAlpha), contrastPanelAlpha, 0f, 1f);
            if (Contrast.Ratio(text, panel) < minContrast) e.Add(o + ": text on panel below " + minContrast + ":1");
            if (Contrast.Ratio(textMuted, panel) < minContrast) e.Add(o + ": muted text on panel below " + minContrast + ":1");
            if (Contrast.Ratio(focus, panel) < minContrast) e.Add(o + ": focus colour on panel below " + minContrast + ":1");
            if (Contrast.Ratio(contrastText, contrastPanel) < minHighContrast) e.Add(o + ": high contrast below " + minHighContrast + ":1");
        }
    }
}
