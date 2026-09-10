using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Seety.Settings;
using Seety.Systems;

namespace Seety
{
    /// <summary>
    /// Seety - one strip that shows the state of your city, where every element is a way into
    /// the game rather than a readout.
    ///
    /// Read-only towards the simulation: it reports city statistics and, when the player clicks,
    /// switches the active infoview. The one exception is SeetySettings.AddFunds, an explicit,
    /// confirmed action on the options page - not something the bar itself ever does. See the
    /// note on that property, and in DESIGN.md, for why it exists despite the rule above.
    /// </summary>
    public class Mod : IMod
    {
        public const string Id = "Seety";
        public const string Name = "Seety";
        /// <summary>
        /// Kept in step with ModVersion in PublishConfiguration.xml, the csproj's Version and
        /// UI/mod.json, which is what the .mjs banner is stamped from. Four places, and the log
        /// line below is the only one a player ever sees - so a stale value here means every
        /// bug report names the wrong build.
        /// </summary>
        public const string Version = "1.0.4";


        public static readonly ILog Log = LogManager.GetLogger(Id).SetShowsErrorsInUI(false);

        public static SeetySettings Settings { get; private set; }

        private static SeetyUISystem _uiSystem;

        /// <summary>One source per language, kept so they can be removed again on unload.</summary>
        private static readonly List<Localization.LocaleSource> _locales = new List<Localization.LocaleSource>();

        private static bool _ready;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Seety " + Version + " loading.");

            Settings = new SeetySettings(this);
            Settings.RegisterInOptionsUI();

            AddLocaleSources();

            AssetDatabase.global.LoadSettings(Id, Settings, new SeetySettings(this));

            // Must run after loading: it decides which rows a first-time player sees, and it
            // must not undo choices an existing player already made.
            Settings.SeedDefaults();

            updateSystem.UpdateAt<SeetyUISystem>(SystemUpdatePhase.UIUpdate);

            _ready = true;
            Log.Info("Seety loaded.");
        }

        public void OnDispose()
        {
            Log.Info("Seety disposing.");
            _ready = false;
            _uiSystem = null;

            RemoveLocaleSources();

            if (Settings != null)
            {
                try
                {
                    Settings.UnregisterInOptionsUI();
                }
                catch (Exception e)
                {
                    Log.Warn("Could not unregister the options page: " + e.Message);
                }

                Settings = null;
            }

            Log.Info("Seety disposed.");
        }

        /// <summary>
        /// Registers every language table with the game.
        ///
        /// One source per locale, all added up front: the game hands each source the locale it
        /// was registered under and asks only the active one for entries, so registering twelve
        /// costs twelve dictionary builds once and nothing after that. A table that fails to
        /// register must not take the other eleven with it, hence the per-language catch.
        /// </summary>
        private static void AddLocaleSources()
        {
            var manager = GameManager.instance == null ? null : GameManager.instance.localizationManager;
            if (manager == null)
            {
                Log.Warn("No localisation manager; Seety will read in English.");
                return;
            }

            foreach (var table in Localization.LocaleTables.All())
            {
                try
                {
                    var source = new Localization.LocaleSource(Settings, table);
                    manager.AddSource(source.LocaleId, source);
                    _locales.Add(source);
                }
                catch (Exception e)
                {
                    Log.Warn("Could not register " + table.LocaleId + ": " + e.Message);
                }
            }

            Log.Info("Registered " + _locales.Count + " languages.");
        }

        private static void RemoveLocaleSources()
        {
            var manager = GameManager.instance == null ? null : GameManager.instance.localizationManager;

            foreach (var source in _locales)
            {
                try
                {
                    if (manager != null)
                    {
                        manager.RemoveSource(source.LocaleId, source);
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("Could not remove " + source.LocaleId + ": " + e.Message);
                }
            }

            _locales.Clear();
        }

        /// <summary>Registered by the UI system so the settings can talk to it.</summary>
        internal static void RegisterUISystem(SeetyUISystem system)
        {
            _uiSystem = system;
        }

        internal static void OnShowStripChanged(bool visible)
        {
            if (!_ready || _uiSystem == null)
            {
                return;
            }

            _uiSystem.SetVisible(visible);
        }

        internal static void OnVitalsChanged()
        {
            if (!_ready || _uiSystem == null)
            {
                return;
            }

            _uiSystem.RebuildActiveVitals();
        }

        internal static void OnAddFunds(int amount)
        {
            if (!_ready || _uiSystem == null)
            {
                return;
            }

            _uiSystem.AddFunds(amount);
        }
    }
}
