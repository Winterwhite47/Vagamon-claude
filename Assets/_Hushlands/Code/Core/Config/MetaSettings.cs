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
        public int unlockCost;

        public VehicleEntry(VehicleId vehicle, float friction, float drag, float jump, float flip, bool unlocked, int cost)
        {
            this.vehicle = vehicle;
            frictionScale = friction;
            dragScale = drag;
            jumpScale = jump;
            flipScale = flip;
            unlockedByDefault = unlocked;
            unlockCost = cost;
        }
    }

    [Serializable]
    public sealed class VehicleSettings : ISettings
    {
        public VehicleId defaultVehicle = VehicleId.WoodenHillSled;
        public VehicleEntry[] entries =
        {
            new VehicleEntry(VehicleId.WoodenHillSled, 1f, 1f, 1f, 1f, true, 0),
            new VehicleEntry(VehicleId.TeaCrateBoard, 0.95f, 1.05f, 1.05f, 1.1f, false, 400),
            new VehicleEntry(VehicleId.BananaLeafToboggan, 0.85f, 0.95f, 0.95f, 0.9f, false, 650),
            new VehicleEntry(VehicleId.BambooSled, 1f, 0.9f, 1.1f, 1f, false, 900),
            new VehicleEntry(VehicleId.GliderSkid, 1.05f, 1f, 1.2f, 1.15f, false, 1300),
            new VehicleEntry(VehicleId.RainUmbrellaSlider, 0.9f, 1.1f, 1.25f, 0.95f, false, 1800),
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
                SettingsCheck.NonNegative(e, o, v.vehicle + ".unlockCost", v.unlockCost);
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
