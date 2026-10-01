using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Json;
using Game.Modding;
using Game.Settings;
using Seety.Vitals;

namespace Seety.Settings
{
    /// <summary>
    /// The stored state behind the strip: which vitals are switched on, where the bar sits, and
    /// the one control that writes to the city.
    ///
    /// Four tabs: the bar itself, which readings it shows, what Seety changes in the game's own
    /// interface, and the treasury control on its own.
    ///
    /// Readings are chosen here, one checkbox each, since 2026-10-01. They used to be chosen on the
    /// strip, in a configuration mode behind a gear button; the gear went so a vertical bar splits
    /// into two even columns, and the mode went with it. Moving the bar is now governed by
    /// LockPosition instead.
    ///
    /// Per-vital threshold numbers were declined on 2026-09-01 and a custom row order on
    /// 2026-09-04. Do not re-propose either.
    /// </summary>
    [FileLocation("ModsSettings/Seety/Seety")]
    [SettingsUITabOrder(BarSection, ReadingsSection, GameSection, FundsSection)]
    [SettingsUIGroupOrder(DisplayGroup, ReadingsGroup, GameGroup, FundsGroup)]
    public class SeetySettings : ModSetting
    {
        public const string BarSection = "Bar";
        public const string ReadingsSection = "Readings";
        public const string GameSection = "Game";
        public const string FundsSection = "Funds";
        public const string DisplayGroup = "DisplayGroup";
        public const string ReadingsGroup = "ReadingsGroup";
        public const string GameGroup = "GameGroup";
        public const string FundsGroup = "FundsGroup";

        private bool _showStrip = true;
        private bool _highlightProblems = true;
        private bool _iconOutline = true;
        private bool _gameButtonStyle;
        private bool _verticalStrip;
        private bool _lockPosition = true;
        private bool _darkGameButtons;
        private bool _buildingReasons;
        private bool _toolbarTrends;
        private bool _zoneTransparency;

        // Default resting place: clear of the vanilla button row along the top edge, so the strip
        // does not land on top of the game's own controls the first time it appears.
        public const int DefaultStripX = 10;
        public const int DefaultStripY = 90;

        private bool _seededDefaults;

        private int _stripX = DefaultStripX;
        private int _stripY = DefaultStripY;

        /// <summary>Ids of vitals the player switched off, comma separated. Empty means "all on".</summary>
        private string _disabled = string.Empty;

        public SeetySettings(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(BarSection, DisplayGroup)]
        public bool ShowStrip
        {
            get { return _showStrip; }
            set
            {
                if (_showStrip == value)
                {
                    return;
                }

                _showStrip = value;
                Mod.OnShowStripChanged(value);
            }
        }

        /// <summary>
        /// Stack the readings in a column instead of a row. Each reading is drawn exactly as in the
        /// row; only the direction they run in changes.
        /// </summary>
        [SettingsUISection(BarSection, DisplayGroup)]
        public bool VerticalStrip
        {
            get { return _verticalStrip; }
            set
            {
                if (_verticalStrip == value)
                {
                    return;
                }

                _verticalStrip = value;
                Mod.OnVerticalStripChanged(value);
            }
        }

        /// <summary>
        /// Keep the bar where it is: while on, pressing on it never moves it.
        ///
        /// On by default on purpose. When the bar could be dragged at any time it got moved by
        /// accident while panning the camera or using a tool. Switch it off, drag the bar, switch
        /// it back on.
        /// </summary>
        [SettingsUISection(BarSection, DisplayGroup)]
        public bool LockPosition
        {
            get { return _lockPosition; }
            set
            {
                if (_lockPosition == value)
                {
                    return;
                }

                _lockPosition = value;
                Mod.OnLockPositionChanged(value);
            }
        }

        /// <summary>
        /// Colour a vital when it crosses its threshold. Off turns the strip back into a plain
        /// readout - the numbers are unchanged either way.
        /// </summary>
        [SettingsUISection(BarSection, DisplayGroup)]
        public bool HighlightProblems
        {
            get { return _highlightProblems; }
            set
            {
                if (_highlightProblems == value)
                {
                    return;
                }

                _highlightProblems = value;
                Mod.OnVitalsChanged();
            }
        }

        /// <summary>
        /// Print the change beside the population and money figures on the vanilla bottom bar.
        ///
        /// The game already knows this number - it publishes it as toolbarBottom.populationDelta
        /// and toolbarBottom.moneyDelta, and its own tooltip shows it when you hover. All this
        /// does is stop it being hidden behind a hover. Nothing is computed here, so the figure
        /// cannot disagree with the one the game shows.
        ///
        /// Off by default, and the only thing in Seety that draws outside its own bar: it extends
        /// the shared vanilla stat field rather than adding to the strip. The optional UI hook
        /// is guarded so a missing module after a game update does not stop other mods loading.
        /// </summary>
        [SettingsUISection(GameSection, GameGroup)]
        public bool ToolbarTrends
        {
            get { return _toolbarTrends; }
            set
            {
                if (_toolbarTrends == value)
                {
                    return;
                }

                _toolbarTrends = value;
                Mod.OnToolbarTrendsChanged(value);
            }
        }

        /// <summary>
        /// Draw a white edge around each icon on the bar.
        ///
        /// On by default, because the bar sits over the city and a dark icon on a dark building is
        /// the case that made the outline necessary. It is a matter of taste rather than of
        /// legibility everywhere else, so it can be switched off for a flatter look; nothing about
        /// the readings changes either way.
        /// </summary>
        [SettingsUISection(BarSection, DisplayGroup)]
        public bool IconOutline
        {
            get { return _iconOutline; }
            set
            {
                if (_iconOutline == value)
                {
                    return;
                }

                _iconOutline = value;
                Mod.OnIconOutlineChanged(value);
            }
        }

        /// <summary>
        /// Draw the bar as a row of the game's own floating buttons instead of one dark panel.
        ///
        /// Off by default so nobody's bar changes under them. The buttons take their colour, size,
        /// corners and hover states from the game's floating-icon-button classes, so they follow
        /// the vanilla row beside them, theme included. Only the drawing changes.
        /// </summary>
        [SettingsUISection(BarSection, DisplayGroup)]
        public bool GameButtonStyle
        {
            get { return _gameButtonStyle; }
            set
            {
                if (_gameButtonStyle == value)
                {
                    return;
                }

                _gameButtonStyle = value;
                Mod.OnGameButtonStyleChanged(value);
            }
        }

        /// <summary>
        /// Draw the game's blue floating buttons in the dark blue of the bottom bar.
        ///
        /// Off by default. This restyles the game's own floating-button class, so it reaches the
        /// vanilla button rows and every mod that uses the game's button, not only Seety. Nothing is
        /// saved: switching it off removes the style and the game's own colours return.
        /// </summary>
        [SettingsUISection(GameSection, GameGroup)]
        public bool DarkGameButtons
        {
            get { return _darkGameButtons; }
            set
            {
                if (_darkGameButtons == value)
                {
                    return;
                }

                _darkGameButtons = value;
                Mod.OnDarkGameButtonsChanged(value);
            }
        }

        /// <summary>
        /// Under the cursor, what is holding a building back: the game's own notifications on it
        /// and the efficiency factors that cost something. See BuildingReasonsTooltipSystem.
        ///
        /// Off by default, so a player who installs Seety for the bar does not meet a new tooltip
        /// on every building. Read every frame by the tooltip system, so it needs no handler.
        /// </summary>
        [SettingsUISection(GameSection, GameGroup)]
        public bool BuildingReasons
        {
            get { return _buildingReasons; }
            set { _buildingReasons = value; }
        }

        /// <summary>
        /// Fade the zoning cells drawn along the roads.
        ///
        /// Off by default. This is the only option in Seety that changes something outside
        /// Seety's own furniture, so it starts in the state where the game looks as it shipped;
        /// a player who wants the quieter grid asks for it.
        ///
        /// Disabled while Zone Color Changer is loaded. Both write the same field on the same
        /// prefabs, and whichever writes last wins without either noticing - see InstalledMods.
        /// </summary>
        [SettingsUISection(GameSection, GameGroup)]
        [SettingsUIDisableByCondition(typeof(InstalledMods), nameof(InstalledMods.ZoneTransparencyUnavailable))]
        public bool ZoneTransparency
        {
            get { return _zoneTransparency; }
            set
            {
                if (_zoneTransparency == value)
                {
                    return;
                }

                _zoneTransparency = value;
                Mod.OnZoneTransparencyChanged(value);
            }
        }

        /// <summary>
        /// Where the player dragged the strip, in the UI's own units. Hidden from the options
        /// page on purpose: it is set by dragging the thing itself, and a pair of numeric fields
        /// would be a worse way to do the same job.
        /// </summary>
        [SettingsUIHidden]
        public int StripX
        {
            get { return _stripX; }
            set { _stripX = value; }
        }

        [SettingsUIHidden]
        public int StripY
        {
            get { return _stripY; }
            set { _stripY = value; }
        }

        /// <summary>
        /// Whether the off-by-default rows have been switched off yet. Without this, adding a
        /// vital to the catalogue would silently switch it on for everyone who already plays,
        /// because the stored setting only lists what is OFF.
        /// </summary>
        [SettingsUIHidden]
        public bool SeededDefaults
        {
            get { return _seededDefaults; }
            set { _seededDefaults = value; }
        }

        /// <summary>Called once after the settings are loaded. Idempotent.</summary>
        public void SeedDefaults()
        {
            if (_seededDefaults)
            {
                return;
            }

            var off = new List<string>();
            foreach (var vital in VitalCatalog.All())
            {
                if (!vital.DefaultOn)
                {
                    off.Add(vital.Id);
                }
            }

            _disabled = string.Join(",", off.ToArray());
            _seededDefaults = true;
            ApplyAndSave();
        }

        [SettingsUIHidden]
        public string DisabledVitals
        {
            get { return _disabled; }
            set { _disabled = value ?? string.Empty; }
        }

        public bool IsVitalEnabled(string id)
        {
            if (string.IsNullOrEmpty(_disabled) || string.IsNullOrEmpty(id))
            {
                return true;
            }

            foreach (var part in _disabled.Split(','))
            {
                if (string.Equals(part.Trim(), id, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public void SetVitalEnabled(string id, bool enabled)
        {
            var ids = new List<string>();
            foreach (var vital in VitalCatalog.All())
            {
                var on = vital.Id == id ? enabled : IsVitalEnabled(vital.Id);
                if (!on)
                {
                    ids.Add(vital.Id);
                }
            }

            // No ApplyAndSave here: the only caller is a checkbox on the options page, and the page
            // saves after every change it makes, as it does for every other option.
            _disabled = string.Join(",", ids.ToArray());
            Mod.OnVitalsChanged();
        }

        /// <summary>
        /// The reading ids in the order their checkboxes appear on the options page, which is
        /// declaration order. Sorted by metadata token because reflection does not promise that
        /// order otherwise.
        /// </summary>
        public static IEnumerable<string> CheckboxOrder()
        {
            var properties = new List<System.Reflection.PropertyInfo>();
            foreach (var property in typeof(SeetySettings).GetProperties())
            {
                if (property.Name.StartsWith("Reading", StringComparison.Ordinal))
                {
                    properties.Add(property);
                }
            }

            properties.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            foreach (var property in properties)
            {
                yield return property.Name.Substring("Reading".Length).ToLowerInvariant();
            }
        }

        /// <summary>
        /// The catalogue ids that have no checkbox below, for the log. Each checkbox is named
        /// "Reading" plus its id, so a vital added to the catalogue without one is caught at load
        /// instead of being impossible to switch off.
        /// </summary>
        public static IEnumerable<string> ReadingsWithoutCheckbox()
        {
            foreach (var vital in VitalCatalog.All())
            {
                var property = typeof(SeetySettings).GetProperty("Reading" + vital.Id,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.IgnoreCase);
                if (property == null)
                {
                    yield return vital.Id;
                }
            }
        }

        private int _fundsAmount;

        /// <summary>
        /// The one field on this page that writes to the save. Everything else on the bar only
        /// reads and points the camera; this changes the treasury. No confirmation dialog on the
        /// button below - clicking a button under a section called "Funds" after typing an amount
        /// is already the deliberate step, and the description on this page already says what it
        /// does. See DESIGN.md for why this exists at all.
        ///
        /// SettingsUITextInput does not render here - it is not used anywhere in the base game
        /// either, and an int field with only that attribute produced no widget at all. A slider
        /// is the form every other numeric option in this framework actually uses, and it comes
        /// with a directly-typable value next to the handle, which is what "type a number" needed.
        /// </summary>
        [SettingsUISection(FundsSection, FundsGroup)]
        [SettingsUISlider(min = -10000000f, max = 10000000f, step = 10000f)]
        public int FundsAmount
        {
            get { return _fundsAmount; }
            set { _fundsAmount = value; }
        }

        /// <summary>
        /// Adds FundsAmount to the city treasury. A negative amount subtracts, since
        /// PlayerMoney.Add already accepts one - no separate control needed for that.
        /// </summary>
        [SettingsUISection(FundsSection, FundsGroup)]
        [SettingsUIButton]
        public bool AddFunds
        {
            set { Mod.OnAddFunds(_fundsAmount); }
        }

        // One checkbox per reading, in the order the bar draws them: the game's infoview menu,
        // then bars before counts. That order comes from the game at runtime, so this one is copied
        // from the "Bar order" line in the log (2026-10-01), with post and environment quality moved
        // ahead of the schools as the bar moves them - see PlaceBeforeSchools. A mismatch is logged
        // as a warning.
        //
        // They all read and write DisabledVitals, which stays the one stored value: [Exclude] keeps
        // the checkboxes out of the settings file, so a player who chose readings on the old strip
        // keeps exactly that choice.
        //
        // Labels are the readings' own titles - see tools/locale. Named "Reading" plus the vital id,
        // which ReadingsWithoutCheckbox relies on.

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingParking
        {
            get { return IsVitalEnabled("parking"); }
            set { SetVitalEnabled("parking", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingTraffic
        {
            get { return IsVitalEnabled("traffic"); }
            set { SetVitalEnabled("traffic", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingElectricity
        {
            get { return IsVitalEnabled("electricity"); }
            set { SetVitalEnabled("electricity", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingWater
        {
            get { return IsVitalEnabled("water"); }
            set { SetVitalEnabled("water", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingSewage
        {
            get { return IsVitalEnabled("sewage"); }
            set { SetVitalEnabled("sewage", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingHealth
        {
            get { return IsVitalEnabled("health"); }
            set { SetVitalEnabled("health", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingDeathcare
        {
            get { return IsVitalEnabled("deathcare"); }
            set { SetVitalEnabled("deathcare", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingCemetery
        {
            get { return IsVitalEnabled("cemetery"); }
            set { SetVitalEnabled("cemetery", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingGarbage
        {
            get { return IsVitalEnabled("garbage"); }
            set { SetVitalEnabled("garbage", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingFire
        {
            get { return IsVitalEnabled("fire"); }
            set { SetVitalEnabled("fire", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingCrimeProb
        {
            get { return IsVitalEnabled("crimeprob"); }
            set { SetVitalEnabled("crimeprob", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingPost
        {
            get { return IsVitalEnabled("post"); }
            set { SetVitalEnabled("post", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingPollution
        {
            get { return IsVitalEnabled("pollution"); }
            set { SetVitalEnabled("pollution", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingElementary
        {
            get { return IsVitalEnabled("elementary"); }
            set { SetVitalEnabled("elementary", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingHighSchool
        {
            get { return IsVitalEnabled("highschool"); }
            set { SetVitalEnabled("highschool", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingCollege
        {
            get { return IsVitalEnabled("college"); }
            set { SetVitalEnabled("college", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingUniversity
        {
            get { return IsVitalEnabled("university"); }
            set { SetVitalEnabled("university", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingHomelessness
        {
            get { return IsVitalEnabled("homelessness"); }
            set { SetVitalEnabled("homelessness", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingDemand
        {
            get { return IsVitalEnabled("demand"); }
            set { SetVitalEnabled("demand", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingHappiness
        {
            get { return IsVitalEnabled("happiness"); }
            set { SetVitalEnabled("happiness", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingUnemployment
        {
            get { return IsVitalEnabled("unemployment"); }
            set { SetVitalEnabled("unemployment", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingProblems
        {
            get { return IsVitalEnabled("problems"); }
            set { SetVitalEnabled("problems", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingTransport
        {
            get { return IsVitalEnabled("transport"); }
            set { SetVitalEnabled("transport", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingTourists
        {
            get { return IsVitalEnabled("tourists"); }
            set { SetVitalEnabled("tourists", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingLandValue
        {
            get { return IsVitalEnabled("landvalue"); }
            set { SetVitalEnabled("landvalue", value); }
        }

        [SettingsUISection(ReadingsSection, ReadingsGroup)]
        [Exclude]
        public bool ReadingWorkers
        {
            get { return IsVitalEnabled("workers"); }
            set { SetVitalEnabled("workers", value); }
        }

        public override void SetDefaults()
        {
            _showStrip = true;
            _highlightProblems = true;
            _iconOutline = true;
            _gameButtonStyle = false;
            _verticalStrip = false;
            _lockPosition = true;
            _darkGameButtons = false;
            _buildingReasons = false;
            _toolbarTrends = false;
            _zoneTransparency = false;
            _seededDefaults = false;
            _stripX = DefaultStripX;
            _stripY = DefaultStripY;
            _disabled = string.Empty;
            _fundsAmount = 0;
        }
    }
}
