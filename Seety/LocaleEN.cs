using System.Collections.Generic;
using Colossal;
using Seety.Settings;

namespace Seety
{
    /// <summary>en-US strings. Additional languages will arrive as JSON, not as more classes.</summary>
    public class LocaleEN : IDictionarySource
    {
        private readonly SeetySettings _settings;

        public LocaleEN(SeetySettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _settings.GetSettingsLocaleID(), Mod.Name },
                { _settings.GetOptionTabLocaleID(SeetySettings.MainSection), "Main" },

                { _settings.GetOptionGroupLocaleID(SeetySettings.DisplayGroup), "Display" },
                { _settings.GetOptionGroupLocaleID(SeetySettings.FundsGroup), "Funds" },

                {
                    _settings.GetOptionLabelLocaleID(nameof(SeetySettings.ShowStrip)),
                    "Show the strip"
                },
                {
                    _settings.GetOptionDescLocaleID(nameof(SeetySettings.ShowStrip)),
                    "Show or hide the whole bar. Click any entry on it to open the matching in-game info view."
                },

                {
                    _settings.GetOptionLabelLocaleID(nameof(SeetySettings.HighlightProblems)),
                    "Highlight problems"
                },
                {
                    _settings.GetOptionDescLocaleID(nameof(SeetySettings.HighlightProblems)),
                    "Colour an entry amber or red when it crosses a sensible limit. The numbers themselves never change."
                },

                {
                    _settings.GetOptionLabelLocaleID(nameof(SeetySettings.FundsAmount)),
                    "Amount"
                },
                {
                    _settings.GetOptionDescLocaleID(nameof(SeetySettings.FundsAmount)),
                    "How much to add to the city treasury. A negative number takes money away instead."
                },

                {
                    _settings.GetOptionLabelLocaleID(nameof(SeetySettings.AddFunds)),
                    "Apply to treasury"
                },
                {
                    _settings.GetOptionDescLocaleID(nameof(SeetySettings.AddFunds)),
                    "This is the one control in Seety that changes your city rather than reporting on it."
                }
            };
        }

        public void Unload()
        {
        }
    }
}
