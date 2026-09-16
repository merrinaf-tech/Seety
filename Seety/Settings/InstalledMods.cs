using System;
using Game.SceneFlow;

namespace Seety.Settings
{
    /// <summary>
    /// What else is loaded, where it changes what Seety may safely do.
    ///
    /// Only one question is asked here, and it is asked because two mods writing the same field
    /// cannot both be right. Zone Color Changer owns the zone palette: it rewrites every
    /// ZonePrefab's colour, including the alpha, from its own configuration. Seety's zone
    /// transparency option writes that same alpha. Whichever runs last wins, silently, and the
    /// player sees a setting that does nothing or a palette that is not the one they chose.
    ///
    /// So the option disables itself instead of competing. Zone Color Changer does this job
    /// properly and configurably; there is nothing to gain by fighting it for the field.
    /// </summary>
    public static class InstalledMods
    {
        /// <summary>
        /// The mod's folder name as the manager reports it. Matched as a substring because the
        /// name carries a platform suffix on a subscribed copy and none on a local build.
        /// </summary>
        private const string ZoneColorChangerName = "ZoneColorChanger";

        private static bool? _zoneColorChanger;

        /// <summary>
        /// True when Zone Color Changer is loaded. Answered once and remembered: the set of
        /// loaded mods cannot change without a restart, and this is read by the options page
        /// every time it draws.
        /// </summary>
        public static bool ZoneColorChangerLoaded
        {
            get
            {
                if (!_zoneColorChanger.HasValue)
                {
                    // Null means "asked too early to tell", so the next caller asks again rather
                    // than inheriting a guess. Only a real answer is remembered.
                    _zoneColorChanger = Detect(ZoneColorChangerName);
                }

                return _zoneColorChanger.GetValueOrDefault(false);
            }
        }

        /// <summary>Inverted, for SettingsUIDisableByCondition - which asks what to disable.</summary>
        public static bool ZoneColorChangerAbsent()
        {
            return !ZoneColorChangerLoaded;
        }

        /// <summary>True when the option must be greyed out. See ZoneColorChangerLoaded.</summary>
        public static bool ZoneTransparencyUnavailable()
        {
            return ZoneColorChangerLoaded;
        }

        private static bool? Detect(string name)
        {
            try
            {
                GameManager manager = GameManager.instance;
                if (manager == null || manager.modManager == null)
                {
                    // Asked before the game finished starting. Not an error, and not a "no":
                    // returned unanswered so the next caller asks again.
                    return null;
                }

                foreach (var info in manager.modManager)
                {
                    if (info.asset != null && info.asset.name != null &&
                        info.asset.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Mod.Log.Info("Seety: " + name + " is loaded; its options defer to it.");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                // A failed look is not a reason to fail the options page. Assume it is absent:
                // the worse outcome of guessing wrong is a palette conflict the player can undo,
                // against an option they can never reach.
                Mod.Log.Warn("Seety: could not read the mod list (" + e.Message + ").");
                return false;
            }
        }
    }
}
