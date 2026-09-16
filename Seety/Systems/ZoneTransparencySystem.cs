using System;
using System.Collections.Generic;
using System.Reflection;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Seety.Systems
{
    /// <summary>
    /// Fades the zoning cells drawn along the roads, and puts them back.
    ///
    /// This is the second thing in Seety that writes rather than reads, and the only one that
    /// writes outside the options page. It is worth being precise about what it touches: a
    /// ZonePrefab's colour is presentation, read by the renderer when it builds the cell overlay.
    /// Nothing in the simulation looks at it. No cell changes type, no lot changes size, no
    /// building decides anything differently. Turning the option off restores the exact values
    /// the game shipped, because they are captured before the first write.
    ///
    /// The mechanism is the one Zone Color Changer established: write the alpha channel of
    /// ZonePrefab.m_Color and m_Edge, then tell ZoneSystem its colours are stale. There is no
    /// public API for the second half, so it is a private field set by reflection - see Restale.
    /// </summary>
    public partial class ZoneTransparencySystem : GameSystemBase
    {
        /// <summary>
        /// What the alpha is multiplied by when the option is on.
        ///
        /// A multiplier rather than a fixed alpha, so the game keeps its own relationships: the
        /// unzoned cells are already fainter than the zoned ones, and the edge is drawn
        /// differently from the fill. Half is enough to read the ground through the grid while
        /// leaving the grid legible enough to zone with - the point is a quieter overlay, not an
        /// absent one.
        ///
        /// Deliberately not configurable. A slider here would be Zone Color Changer with fewer
        /// features; this is one switch that does one thing.
        /// </summary>
        private const float TransparencyFactor = 0.5f;

        private PrefabSystem _prefabs;
        private ZoneSystem _zones;
        private EntityQuery _zoneQuery;

        /// <summary>ZoneSystem.m_UpdateColors. Private, and the only way to ask for a refresh.</summary>
        private FieldInfo _updateColours;

        /// <summary>
        /// The colours as the game shipped them, by prefab entity. Captured once, before this
        /// system has written anything, so "off" can restore rather than approximate.
        /// </summary>
        private readonly Dictionary<Entity, KeyValuePair<Color, Color>> _original =
            new Dictionary<Entity, KeyValuePair<Color, Color>>();

        private bool _captured;
        private bool _applied;

        protected override void OnCreate()
        {
            base.OnCreate();

            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            _zones = World.GetOrCreateSystemManaged<ZoneSystem>();

            _updateColours = typeof(ZoneSystem).GetField(
                "m_UpdateColors", BindingFlags.Instance | BindingFlags.NonPublic);

            if (_updateColours == null)
            {
                // A game update renamed or removed it. Say so once, here, rather than writing
                // colours that never reach the screen and reading as a broken option.
                Mod.Log.Warn(
                    "Seety: ZoneSystem.m_UpdateColors is gone, so zone transparency cannot " +
                    "refresh the overlay. The option will do nothing this session.");
            }

            _zoneQuery = GetEntityQuery(
                ComponentType.ReadOnly<ZoneData>(),
                ComponentType.ReadOnly<PrefabData>());

            RequireForUpdate(_zoneQuery);
            Enabled = false;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // The prefabs exist from here on, and not in the main menu. A fresh load means fresh
            // prefab instances, so anything captured from the last city is stale.
            _original.Clear();
            _captured = false;
            _applied = false;

            Enabled = !mode.HasFlag(GameMode.MainMenu);
        }

        /// <summary>
        /// Runs once per load, to apply a setting that was chosen before this city existed.
        /// After that the system sleeps and the options page drives it directly.
        /// </summary>
        protected override void OnUpdate()
        {
            Capture();
            Apply(Wanted());
            Enabled = false;
        }

        /// <summary>Called when the player turns the option on or off.</summary>
        public void SetTransparent(bool transparent)
        {
            if (!_captured)
            {
                // Toggled from the main menu, before any prefab is loaded. Nothing to write yet;
                // OnUpdate applies the stored setting when a city opens.
                return;
            }

            Apply(transparent && Wanted());
        }

        /// <summary>
        /// Whether the cells should be faded right now.
        ///
        /// The stored setting is not the last word, because Zone Color Changer can arrive after
        /// it was chosen. Greying the option out then would leave a switch stuck on with no way
        /// to reach it, so the answer is computed rather than remembered: with that mod loaded
        /// this one stands down, whatever the saved value says.
        /// </summary>
        private static bool Wanted()
        {
            return Mod.Settings != null
                && Mod.Settings.ZoneTransparency
                && !Settings.InstalledMods.ZoneColorChangerLoaded;
        }

        private void Capture()
        {
            if (_captured)
            {
                return;
            }

            NativeArray<Entity> entities = _zoneQuery.ToEntityArray(Allocator.Temp);
            NativeArray<PrefabData> data = _zoneQuery.ToComponentDataArray<PrefabData>(Allocator.Temp);

            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    ZonePrefab prefab = _prefabs.GetPrefab<ZonePrefab>(data[i]);
                    if (prefab == null)
                    {
                        continue;
                    }

                    _original[entities[i]] = new KeyValuePair<Color, Color>(prefab.m_Color, prefab.m_Edge);
                }

                _captured = true;
            }
            catch (Exception e)
            {
                Mod.Log.Warn("Seety: could not read the zone colours (" + e.Message + ").");
            }
            finally
            {
                entities.Dispose();
                data.Dispose();
            }
        }

        private void Apply(bool transparent)
        {
            if (!_captured || transparent == _applied)
            {
                return;
            }

            NativeArray<Entity> entities = _zoneQuery.ToEntityArray(Allocator.Temp);
            NativeArray<PrefabData> data = _zoneQuery.ToComponentDataArray<PrefabData>(Allocator.Temp);

            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    KeyValuePair<Color, Color> shipped;
                    if (!_original.TryGetValue(entities[i], out shipped))
                    {
                        // A zone that appeared after the capture - a theme loaded later, say.
                        // Left alone: without a recorded original there is nothing to restore to,
                        // and a one-way change is worse than an inconsistent one.
                        continue;
                    }

                    ZonePrefab prefab = _prefabs.GetPrefab<ZonePrefab>(data[i]);
                    if (prefab == null)
                    {
                        continue;
                    }

                    prefab.m_Color = WithAlpha(shipped.Key, transparent);
                    prefab.m_Edge = WithAlpha(shipped.Value, transparent);
                }

                _applied = transparent;
                Restale();
            }
            catch (Exception e)
            {
                Mod.Log.Warn("Seety: could not write the zone colours (" + e.Message + ").");
            }
            finally
            {
                entities.Dispose();
                data.Dispose();
            }
        }

        private static Color WithAlpha(Color shipped, bool transparent)
        {
            return transparent
                ? new Color(shipped.r, shipped.g, shipped.b, shipped.a * TransparencyFactor)
                : shipped;
        }

        /// <summary>
        /// Marks ZoneSystem's colours as needing to be sent to the renderer again.
        ///
        /// Without this the prefabs hold the new values and the screen keeps the old ones until
        /// something else happens to invalidate them.
        /// </summary>
        private void Restale()
        {
            if (_updateColours == null || _zones == null)
            {
                return;
            }

            _updateColours.SetValue(_zones, true);
        }

        /// <summary>
        /// Puts the shipped colours back when the mod is unloaded.
        ///
        /// The prefabs outlive this system - they belong to the loaded asset database, not to
        /// Seety - so leaving them faded would leave a mod's fingerprint on a session it is no
        /// longer part of.
        /// </summary>
        protected override void OnDestroy()
        {
            Apply(false);
            base.OnDestroy();
        }
    }
}
