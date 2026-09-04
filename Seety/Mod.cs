using System;
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
        public const string Version = "1.0.0";

        private const string LocaleId = "en-US";

        public static readonly ILog Log = LogManager.GetLogger(Id).SetShowsErrorsInUI(false);

        public static SeetySettings Settings { get; private set; }

        private static SeetyUISystem _uiSystem;
        private static LocaleEN _locale;
        private static bool _ready;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Seety " + Version + " loading.");

            Settings = new SeetySettings(this);
            Settings.RegisterInOptionsUI();

            _locale = new LocaleEN(Settings);
            GameManager.instance.localizationManager.AddSource(LocaleId, _locale);

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

            if (_locale != null)
            {
                try
                {
                    if (GameManager.instance != null && GameManager.instance.localizationManager != null)
                    {
                        GameManager.instance.localizationManager.RemoveSource(LocaleId, _locale);
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("Could not remove the localisation source: " + e.Message);
                }

                _locale = null;
            }

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
