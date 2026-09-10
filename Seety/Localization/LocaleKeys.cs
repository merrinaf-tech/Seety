namespace Seety.Localization
{
    /// <summary>
    /// Every string id Seety publishes, in one place.
    ///
    /// Two shapes. Ids beginning <c>Seety.</c> are asked for by the UI through cs2/l10n's
    /// <c>translate(id, fallback)</c>, which is why every call site passes the English text as
    /// the fallback: a missing translation degrades to English rather than to a blank row.
    /// Ids beginning <c>option.</c> are symbolic slots for the options page - see
    /// <see cref="LocaleSource"/>.
    ///
    /// A vital's title is looked up as <c>Seety.VITAL[id]</c> and its short label as
    /// <c>Seety.LABEL[id]</c>, built from the vital's own stable id, so adding a row to the
    /// catalogue means adding two entries per language and nothing else.
    /// </summary>
    public static class LocaleKeys
    {
        public static string VitalTitle(string vitalId)
        {
            return "Seety.VITAL[" + vitalId + "]";
        }

        public static string VitalLabel(string vitalId)
        {
            return "Seety.LABEL[" + vitalId + "]";
        }
    }
}
