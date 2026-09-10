using System;
using System.Collections.Generic;
using Colossal;
using Seety.Settings;

namespace Seety.Localization
{
    /// <summary>
    /// Registers one language table with the game.
    ///
    /// Two kinds of key live in a table. Most are plain ids the UI asks for by name through
    /// cs2/l10n's <c>translate</c> - those are passed through untouched. The options page is the
    /// exception: its ids can only be produced by <c>ModSetting.GetOption*LocaleID</c>, which
    /// needs the live settings object, so those are written in the tables as symbolic slots like
    /// <c>option.label.ShowStrip</c> and resolved here.
    ///
    /// Keeping the tables free of game types is deliberate: it means a table is just data, and a
    /// missing or misspelled slot is reported rather than silently dropping a string.
    /// </summary>
    public sealed class LocaleSource : IDictionarySource
    {
        /// <summary>Prefix marking a key that has to be resolved against the settings object.</summary>
        private const string OptionPrefix = "option.";

        private readonly SeetySettings _settings;
        private readonly LocaleTable _table;

        public LocaleSource(SeetySettings settings, LocaleTable table)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            _settings = settings;
            _table = table;
        }

        /// <summary>The game locale id this source is registered under, e.g. "it-IT".</summary>
        public string LocaleId
        {
            get { return _table.LocaleId; }
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>(_table.Entries.Count);

            foreach (var pair in _table.Entries)
            {
                var id = ResolveId(pair.Key);
                if (id == null)
                {
                    // Silently dropping it would leave the option blank in that language with
                    // nothing to say why.
                    Mod.Log.Warn("Unknown locale slot '" + pair.Key + "' in " + _table.LocaleId + "; skipped.");
                    continue;
                }

                entries[id] = pair.Value;
            }

            return entries;
        }

        /// <summary>A symbolic slot turned into the id the game actually looks up, or null.</summary>
        private string ResolveId(string slot)
        {
            if (!slot.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                // A plain UI key. The TSX asks for exactly this string.
                return slot;
            }

            var rest = slot.Substring(OptionPrefix.Length);

            if (rest == "title")
            {
                return _settings.GetSettingsLocaleID();
            }

            var split = rest.IndexOf('.');
            if (split <= 0 || split == rest.Length - 1)
            {
                return null;
            }

            var kind = rest.Substring(0, split);
            var name = rest.Substring(split + 1);

            switch (kind)
            {
                case "tab":
                    return _settings.GetOptionTabLocaleID(name);
                case "group":
                    return _settings.GetOptionGroupLocaleID(name);
                case "label":
                    return _settings.GetOptionLabelLocaleID(name);
                case "desc":
                    return _settings.GetOptionDescLocaleID(name);
                default:
                    return null;
            }
        }

        public void Unload()
        {
        }
    }

    /// <summary>One language: its game locale id, and every string in it.</summary>
    public sealed class LocaleTable
    {
        public LocaleTable(string localeId, Dictionary<string, string> entries)
        {
            LocaleId = localeId;
            Entries = entries;
        }

        public string LocaleId { get; }

        public Dictionary<string, string> Entries { get; }
    }
}
