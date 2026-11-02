using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    [Serializable]
    public struct PickupEntry
    {
        public PickupId pickup;
        public float durationSeconds;
        public int value;

        public PickupEntry(PickupId pickup, float durationSeconds, int value)
        {
            this.pickup = pickup;
            this.durationSeconds = durationSeconds;
            this.value = value;
        }
    }

    [Serializable]
    public sealed class PickupSettings : ISettings
    {
        public PickupEntry[] entries =
        {
            new PickupEntry(PickupId.SpiceCoin, 0f, 1),
            new PickupEntry(PickupId.MistLantern, 8f, 0),
            new PickupEntry(PickupId.WindFeather, 5f, 0),
            new PickupEntry(PickupId.TeaFlask, 6f, 0),
            new PickupEntry(PickupId.MagnetBell, 10f, 0),
            new PickupEntry(PickupId.GoldenMistbell, 0f, 50),
        };
        public float magnetRadius = 6f;
        public float magnetPullSpeed = 14f;
        /// <summary>Rider touches a pickup within this distance of its centre.</summary>
        public float collectRadius = 0.9f;
        /// <summary>Wind feather: added to the cruise target speed (still capped at maxSpeed, so fairness holds).</summary>
        public float windSpeedBonus = 2.5f;
        /// <summary>Tea flask: score multiplier while active.</summary>
        public float teaScoreMultiplier = 2f;
        /// <summary>Mist lantern: mist density scale around the rider while active.</summary>
        public float lanternMistScale = 0.3f;
        /// <summary>Golden mistbell: score bonus on top of its coin value.</summary>
        public int mistbellScoreBonus = 500;
        /// <summary>Collected pickups shrink away over this long.</summary>
        public float collectAnimSeconds = 0.25f;
        /// <summary>Placeholder visual size multipliers (timed pickups, golden mistbell) relative to a coin.</summary>
        public float timedVisualScale = 1.3f;
        public float mistbellVisualScale = 1.8f;

        public PickupEntry Get(PickupId id)
        {
            foreach (PickupEntry p in entries)
            {
                if (p.pickup == id) return p;
            }
            throw new KeyNotFoundException("Pickup not configured: " + id);
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(PickupSettings);
            var keys = new List<PickupId>();
            foreach (PickupEntry p in entries)
            {
                keys.Add(p.pickup);
                SettingsCheck.NonNegative(e, o, p.pickup + ".durationSeconds", p.durationSeconds);
                SettingsCheck.NonNegative(e, o, p.pickup + ".value", p.value);
            }
            SettingsCheck.CoversEnum(e, o, keys);
            SettingsCheck.Positive(e, o, nameof(magnetRadius), magnetRadius);
            SettingsCheck.Positive(e, o, nameof(magnetPullSpeed), magnetPullSpeed);
            SettingsCheck.Positive(e, o, nameof(collectRadius), collectRadius);
            SettingsCheck.Ordered(e, o, nameof(collectRadius), collectRadius, nameof(magnetRadius), magnetRadius);
            SettingsCheck.NonNegative(e, o, nameof(windSpeedBonus), windSpeedBonus);
            if (teaScoreMultiplier < 1f) e.Add(o + ".teaScoreMultiplier must be >= 1");
            SettingsCheck.Range(e, o, nameof(lanternMistScale), lanternMistScale, 0f, 1f);
            SettingsCheck.NonNegative(e, o, nameof(mistbellScoreBonus), mistbellScoreBonus);
            SettingsCheck.Positive(e, o, nameof(collectAnimSeconds), collectAnimSeconds);
            SettingsCheck.Positive(e, o, nameof(timedVisualScale), timedVisualScale);
            SettingsCheck.Positive(e, o, nameof(mistbellVisualScale), mistbellVisualScale);
            foreach (PickupEntry p in entries)
            {
                bool timed = p.pickup != PickupId.SpiceCoin && p.pickup != PickupId.GoldenMistbell;
                if (timed && !(p.durationSeconds > 0f)) e.Add(o + ": " + p.pickup + " needs a duration");
            }
        }
    }

    [Serializable]
    public sealed class ScoreSettings : ISettings
    {
        public float pointsPerMeter = 1f;
        public int pointsPerFlip = 100;
        public int pointsPerDucklingGroup = 250;
        public int pointsPerCoin = 10;

        public void Validate(List<string> e)
        {
            const string o = nameof(ScoreSettings);
            SettingsCheck.NonNegative(e, o, nameof(pointsPerMeter), pointsPerMeter);
            SettingsCheck.NonNegative(e, o, nameof(pointsPerFlip), pointsPerFlip);
            SettingsCheck.NonNegative(e, o, nameof(pointsPerDucklingGroup), pointsPerDucklingGroup);
            SettingsCheck.NonNegative(e, o, nameof(pointsPerCoin), pointsPerCoin);
        }
    }

    [Serializable]
    public struct VehicleEntry
    {
        public VehicleId vehicle;
        public float frictionScale;
        public float dragScale;
        public float jumpScale;
        public float flipScale;
        public bool unlockedByDefault;
        /// <summary>Unlocks when lifetime spice coins reach this (earned, never spent: no shop, D-046).</summary>
        public int unlockLifetimeCoins;
        public string color;
        public float deckLengthScale;
        public float deckWidthScale;

        public VehicleEntry(VehicleId vehicle, float friction, float drag, float jump, float flip, bool unlocked, int coins,
            string color, float deckLength, float deckWidth)
        {
            this.vehicle = vehicle;
            frictionScale = friction;
            dragScale = drag;
            jumpScale = jump;
            flipScale = flip;
            unlockedByDefault = unlocked;
            unlockLifetimeCoins = coins;
            this.color = color;
            deckLengthScale = deckLength;
            deckWidthScale = deckWidth;
        }
    }

    [Serializable]
    public sealed class VehicleSettings : ISettings
    {
        public VehicleId defaultVehicle = VehicleId.WoodenHillSled;
        public VehicleEntry[] entries =
        {
            new VehicleEntry(VehicleId.WoodenHillSled, 1f, 1f, 1f, 1f, true, 0, "#6B3A24", 1f, 1f),
            new VehicleEntry(VehicleId.TeaCrateBoard, 0.95f, 1.05f, 1.05f, 1.1f, false, 150, "#8D3F2D", 0.9f, 1.1f),
            new VehicleEntry(VehicleId.BananaLeafToboggan, 0.85f, 0.95f, 0.95f, 0.9f, false, 300, "#2E6B2A", 1.3f, 0.8f),
            new VehicleEntry(VehicleId.BambooSled, 1f, 0.9f, 1.1f, 1f, false, 500, "#B9A15A", 1.1f, 0.9f),
            new VehicleEntry(VehicleId.GliderSkid, 1.05f, 1f, 1.2f, 1.15f, false, 800, "#4A6B82", 1.2f, 1.3f),
            new VehicleEntry(VehicleId.RainUmbrellaSlider, 0.9f, 1.1f, 1.25f, 0.95f, false, 1200, "#D4AF37", 0.8f, 1f),
        };

        public VehicleEntry Get(VehicleId id)
        {
            foreach (VehicleEntry v in entries)
            {
                if (v.vehicle == id) return v;
            }
            throw new KeyNotFoundException("Vehicle not configured: " + id);
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(VehicleSettings);
            var keys = new List<VehicleId>();
            foreach (VehicleEntry v in entries)
            {
                keys.Add(v.vehicle);
                SettingsCheck.Positive(e, o, v.vehicle + ".frictionScale", v.frictionScale);
                SettingsCheck.Positive(e, o, v.vehicle + ".dragScale", v.dragScale);
                SettingsCheck.Positive(e, o, v.vehicle + ".jumpScale", v.jumpScale);
                SettingsCheck.Positive(e, o, v.vehicle + ".flipScale", v.flipScale);
                SettingsCheck.NonNegative(e, o, v.vehicle + ".unlockLifetimeCoins", v.unlockLifetimeCoins);
                SettingsCheck.Positive(e, o, v.vehicle + ".deckLengthScale", v.deckLengthScale);
                SettingsCheck.Positive(e, o, v.vehicle + ".deckWidthScale", v.deckWidthScale);
                if (!Palette.TryParse(v.color, out _, out _, out _)) e.Add(o + ": bad colour for " + v.vehicle);
                if (v.unlockedByDefault != (v.unlockLifetimeCoins == 0)) e.Add(o + ": " + v.vehicle + " unlockedByDefault must match zero coins");
            }
            SettingsCheck.CoversEnum(e, o, keys);
            bool defaultFound = false;
            foreach (VehicleEntry v in entries)
            {
                if (v.vehicle == defaultVehicle)
                {
                    defaultFound = true;
                    if (!v.unlockedByDefault) e.Add(o + ": default vehicle must be unlocked by default");
                }
            }
            if (!defaultFound) e.Add(o + ": default vehicle missing");
        }
    }
}
