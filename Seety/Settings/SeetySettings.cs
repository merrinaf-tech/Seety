using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Seety.Vitals;

namespace Seety.Settings
{
    /// <summary>
    /// Which vitals the strip shows.
    ///
    /// v0.1 keeps this as one toggle per vital, which is honest about what the mod can do today.
    /// Ordering and thresholds arrive with v0.2, when the list becomes a proper editable set
    /// rather than a fixed catalogue.
    /// </summary>
    [FileLocation("ModsSettings/Seety/Seety")]
    [SettingsUIGroupOrder(DisplayGroup, FundsGroup, AboutGroup)]
    [SettingsUIShowGroupName(DisplayGroup, FundsGroup, AboutGroup)]
    public class SeetySettings : ModSetting
    {
        public const string MainSection = "Main";
        public const string DisplayGroup = "DisplayGroup";
        public const string FundsGroup = "FundsGroup";
        public const string AboutGroup = "AboutGroup";

        private bool _showStrip = true;
        private bool _highlightProblems = true;

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

        [SettingsUISection(MainSection, DisplayGroup)]
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
        /// Colour a vital when it crosses its threshold. Off turns the strip back into a plain
        /// readout - the numbers are unchanged either way.
        /// </summary>
        [SettingsUISection(MainSection, DisplayGroup)]
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

            _disabled = string.Join(",", ids.ToArray());
            ApplyAndSave();
            Mod.OnVitalsChanged();
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
        [SettingsUISection(MainSection, FundsGroup)]
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
        [SettingsUISection(MainSection, FundsGroup)]
        [SettingsUIButton]
        public bool AddFunds
        {
            set { Mod.OnAddFunds(_fundsAmount); }
        }

        // There is deliberately no list of vitals here any more.
        //
        // Thirty checkboxes on an options page is a list nobody reads, and it asks the player to
        // pick by name something they would recognise by sight. The gear on the strip does the
        // same job by letting them click the thing itself - see DESIGN.md. The stored state still
        // lives in DisabledVitals; only the page full of toggles is gone.








        // Service and hazard rows. All off on a fresh install - see Vital.DefaultOn.
























        public override void SetDefaults()
        {
            _showStrip = true;
            _highlightProblems = true;
            _seededDefaults = false;
            _stripX = DefaultStripX;
            _stripY = DefaultStripY;
            _disabled = string.Empty;
            _fundsAmount = 0;
        }
    }
}
