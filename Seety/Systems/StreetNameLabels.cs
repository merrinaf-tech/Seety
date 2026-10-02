using System;
using Game.Rendering;
using Game.UI;
using HarmonyLib;
using Seety.Settings;
using Unity.Entities;

namespace Seety.Systems
{
    /// <summary>
    /// Which street names the game draws on the map: all of them, only the ones the player gave a
    /// street, or none.
    ///
    /// The game takes every label's text from NameSystem.GetRenderedLabelName, and only two systems
    /// call it: AggregateMeshSystem for streets and AreaBufferSystem for districts. A prefix on that
    /// method answers with an empty string for a street whose name should not show, and an empty
    /// label draws nothing. Districts are left alone by checking the entity is a street - a
    /// Game.Net.Aggregate - before touching it.
    ///
    /// Nothing is written to the save. Every name stays where it is; the game only stops drawing it,
    /// and switching back draws it again.
    ///
    /// This is the one place Seety patches the game. If the method moves in an update the patch is
    /// not applied, Available stays false, and the options page and the traffic window grey the
    /// control out instead of offering a switch that does nothing.
    /// </summary>
    public static class StreetNameLabels
    {
        private const string HarmonyId = "com.fabiozsche.seety";

        private static Harmony _harmony;
        private static EntityManager _entities;
        private static bool _hasEntities;

        public static StreetNameMode Mode { get; private set; } = StreetNameMode.ShowAll;

        /// <summary>Whether the patch is in place, so the setting can do anything at all.</summary>
        public static bool Available { get; private set; }

        /// <summary>For SettingsUIDisableByCondition: true greys the option out.</summary>
        public static bool Unavailable()
        {
            return !Available;
        }

        public static void Apply(StreetNameMode mode)
        {
            Mode = mode;
            try
            {
                var target = AccessTools.Method(typeof(NameSystem), "GetRenderedLabelName", new[] { typeof(Entity) });
                if (target == null)
                {
                    Mod.Log.Warn("NameSystem.GetRenderedLabelName not found; street names cannot be hidden.");
                    return;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(target, prefix: new HarmonyMethod(typeof(StreetNameLabels), nameof(Prefix)));
                Available = true;
            }
            catch (Exception e)
            {
                Mod.Log.Warn("Could not patch street labels; street names cannot be hidden: " + e.Message);
            }
        }

        public static void Remove()
        {
            if (_harmony != null)
            {
                try
                {
                    _harmony.UnpatchAll(HarmonyId);
                }
                catch (Exception e)
                {
                    Mod.Log.Warn("Could not remove the street label patch: " + e.Message);
                }

                _harmony = null;
            }

            Available = false;
            _hasEntities = false;
        }

        /// <summary>Switches mode and has the game rebuild every street label with it.</summary>
        public static void SetMode(StreetNameMode mode)
        {
            if (Mode == mode)
            {
                return;
            }

            Mode = mode;
            RefreshLabels();
        }

        /// <summary>
        /// AggregateMeshSystem.OnDictionaryChanged is what the game runs when the language changes:
        /// it marks every street label for rebuilding, which is exactly what a new mode needs. It is
        /// private, so it is called by name; if it is ever missing, the new mode shows as labels are
        /// rebuilt for other reasons, rather than at once.
        /// </summary>
        private static void RefreshLabels()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                var system = world == null ? null : world.GetExistingSystemManaged<AggregateMeshSystem>();
                var method = AccessTools.Method(typeof(AggregateMeshSystem), "OnDictionaryChanged");
                if (system != null && method != null)
                {
                    method.Invoke(system, null);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn("Could not refresh street labels: " + e.Message);
            }
        }

        private static bool Prefix(NameSystem __instance, Entity entity, ref string __result)
        {
            var mode = Mode;
            if (mode == StreetNameMode.ShowAll || !IsStreet(entity))
            {
                return true;
            }

            if (mode == StreetNameMode.CustomOnly && __instance.TryGetCustomName(entity, out _))
            {
                return true;
            }

            __result = string.Empty;
            return false;
        }

        private static bool IsStreet(Entity entity)
        {
            if (!_hasEntities)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    return false;
                }

                _entities = world.EntityManager;
                _hasEntities = true;
            }

            return _entities.Exists(entity) && _entities.HasComponent<Game.Net.Aggregate>(entity);
        }
    }
}
