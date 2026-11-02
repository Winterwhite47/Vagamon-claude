using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Surface table with a vehicle's friction scale applied.</summary>
    public sealed class ScaledSurfaces : ISurfaceProvider
    {
        private readonly ISurfaceProvider _inner;
        private readonly float _friction;
        private readonly float _drag;

        public ScaledSurfaces(ISurfaceProvider inner, float frictionScale, float dragScale)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _friction = frictionScale;
            _drag = dragScale;
        }

        public float FrictionFor(SurfaceId surface)
        {
            return _inner.FrictionFor(surface) * _friction;
        }

        public float DragFor(SurfaceId surface)
        {
            return _inner.DragFor(surface) * _drag;
        }
    }

    /// <summary>Rider, speed and surface settings for one vehicle (copies; the shared config is never mutated).</summary>
    public sealed class VehicleLoadout
    {
        public VehicleId Vehicle { get; private set; }
        public RiderSettings Rider { get; private set; }
        public SpeedSettings Speed { get; private set; }
        public ISurfaceProvider Surfaces { get; private set; }

        public static VehicleLoadout Build(VehicleEntry v, RiderSettings rider, SpeedSettings speed, SurfaceSettings surfaces)
        {
            RiderSettings r = rider.WithJumpScale(v.jumpScale);
            r.flipDegreesPerSecond *= v.flipScale;
            SpeedSettings sp = speed.Copy();
            sp.baseDrag *= v.dragScale;
            return new VehicleLoadout
            {
                Vehicle = v.vehicle,
                Rider = r,
                Speed = sp,
                Surfaces = new ScaledSurfaces(new SurfaceTable(surfaces), v.frictionScale, v.dragScale),
            };
        }
    }

    /// <summary>
    /// Garage: vehicles unlock as lifetime spice coins grow (earned, never spent). Selection persists through
    /// ISaveService. The default sled is always available and every vehicle can clear every stretch.
    /// </summary>
    public sealed class Garage
    {
        public const string KeySelected = "garage.selected";
        public const string KeyLifetimeCoins = "profile.lifetimeCoins";

        private readonly VehicleSettings _s;
        private readonly ISaveService _save;

        public Garage(VehicleSettings settings, ISaveService save)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            Selected = _save.TryLoad(KeySelected, out int sel) && Enum.IsDefined(typeof(VehicleId), sel) &&
                       IsUnlocked((VehicleId)sel)
                ? (VehicleId)sel
                : settings.defaultVehicle;
        }

        public VehicleId Selected { get; private set; }
        public event Action<VehicleId> Unlocked;

        public long LifetimeCoins => _save.TryLoad(KeyLifetimeCoins, out long c) ? c : 0L;

        public bool IsUnlocked(VehicleId id)
        {
            VehicleEntry v = _s.Get(id);
            return v.unlockedByDefault || LifetimeCoins >= v.unlockLifetimeCoins;
        }

        public IReadOnlyList<VehicleId> UnlockedVehicles()
        {
            var list = new List<VehicleId>();
            foreach (VehicleEntry v in _s.entries)
            {
                if (IsUnlocked(v.vehicle)) list.Add(v.vehicle);
            }
            return list;
        }

        /// <summary>Coins needed until the next locked vehicle, or 0 when all are unlocked.</summary>
        public long CoinsToNextUnlock()
        {
            long best = long.MaxValue;
            foreach (VehicleEntry v in _s.entries)
            {
                if (!IsUnlocked(v.vehicle)) best = Math.Min(best, v.unlockLifetimeCoins - LifetimeCoins);
            }
            return best == long.MaxValue ? 0L : best;
        }

        public bool Select(VehicleId id)
        {
            if (!IsUnlocked(id)) return false;
            Selected = id;
            _save.Save(KeySelected, (int)id);
            return true;
        }

        /// <summary>Adds coins earned in a run to the lifetime total and reports new unlocks.</summary>
        public void AddLifetimeCoins(int coins)
        {
            if (coins <= 0) return;
            var before = new HashSet<VehicleId>(UnlockedVehicles());
            _save.Save(KeyLifetimeCoins, LifetimeCoins + coins);
            foreach (VehicleId id in UnlockedVehicles())
            {
                if (!before.Contains(id)) Unlocked?.Invoke(id);
            }
        }
    }
}
