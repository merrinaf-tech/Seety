namespace Seety.Vitals
{
    /// <summary>
    /// Where a vital's number comes from.
    ///
    /// Every entry here is a LIVE reading, taken from the exact component or system the vanilla
    /// UI reads for the same figure. Seety deliberately does NOT use CityStatisticsSystem - see
    /// the note on <see cref="VitalReader"/> for why that produced a strip which disagreed with
    /// the game's own HUD.
    /// </summary>
    public enum VitalSource
    {
        /// <summary>Population.m_AverageHappiness on the city entity. Already 0-100.</summary>
        Happiness,

        /// <summary>Population.m_AverageHealth on the city entity. Already 0-100.</summary>
        Health,

        /// <summary>CountHouseholdDataSystem.UnemploymentRate. Already 0-100.</summary>
        Unemployment,

        /// <summary>CountHouseholdDataSystem.HomelessnessRate. Already 0-100.</summary>
        Homelessness,

        /// <summary>CountHouseholdDataSystem.CityWorkerCount.</summary>
        Workers,

        /// <summary>StatisticType.TouristCount - the source vanilla's own tourism panel uses.</summary>
        Tourists,

        /// <summary>
        /// How many notification icons the city is currently showing, counting only those at
        /// IconPriority.Problem or worse. This is the alert half of the mod arriving as just
        /// another row in the same list - which is the entire point of the design.
        /// </summary>
        Problems,

        /// <summary>
        /// The number comes from one of vanilla's own UI bindings and is computed in the UI, not
        /// here. See <see cref="VanillaBinding"/> for why this cannot be done C#-side.
        /// </summary>
        Vanilla,

        /// <summary>
        /// Total public transport passengers, with a breakdown per mode behind it. Read from the
        /// same PassengerCount* statistics vanilla's own transport summaries use.
        /// </summary>
        Transport
    }

    /// <summary>How a vanilla binding's raw contents turn into one number.</summary>
    public enum VanillaKind
    {
        /// <summary>
        /// Two plain numbers read as "how full is it": demand over supply. For containers that
        /// fill up - a landfill, a jail - where a HIGH reading is the bad one.
        /// </summary>
        Ratio,

        /// <summary>
        /// Two plain numbers read as "how much of the need is met": supply over demand, capped at
        /// 100. For services that provide for a demand, where a LOW reading is the bad one.
        ///
        /// The first in-game test proved this is the direction players expect. Shown as
        /// utilisation, a city with no water pump at all read "Water 100%" - demand against zero
        /// capacity is indeed fully utilised, and completely misleading. As coverage the same city
        /// reads 0%: none of the need is met, which is exactly what is wrong with it.
        /// </summary>
        Coverage,

        /// <summary>
        /// An IndicatorValue. Which reading is meaningful depends on how vanilla built it, and
        /// that is legible at runtime without another flag:
        ///
        ///   * `new IndicatorValue(0, max, value)` - a level on its own 0..max scale, so the
        ///     percentage is current/max. Pollution works this way.
        ///   * `IndicatorValue.Calculate(supply, demand)` - headroom, clamped to -1..+1, where
        ///     negative means overloaded. Its min is -1, so a negative min identifies it, and
        ///     utilisation is (1 - current).
        /// </summary>
        Indicator,

        /// <summary>A plain number shown as it is, with no denominator. Land value, for one.</summary>
        Scalar,

        /// <summary>
        /// An array of flow ratios, one per road category, with the first repeated at the end to
        /// close vanilla's radar chart. The strip shows the average of the real entries.
        /// </summary>
        FlowArray,

        /// <summary>A 0..1 fraction shown as a percentage. Demand works this way.</summary>
        Fraction,

        /// <summary>
        /// All six demand bindings behind one row: the strip shows the highest of them, because
        /// the useful glance is "is anything short right now", and the window lists them.
        ///
        /// Six separate rows was the honest first cut but it ate a third of the strip for figures
        /// that are usually all low at once.
        /// </summary>
        DemandGroup
    }

    /// <summary>
    /// Points a vital at one of vanilla's own UI bindings.
    ///
    /// The service, hazard and infrastructure figures are computed by jobs that live inside
    /// vanilla's infoview UI systems and are written to private ValueBindings. There is no public
    /// accessor, and reflection would not help: those systems check `binding.active` and skip
    /// their work entirely unless something is subscribed. The values simply do not exist while
    /// nobody is watching.
    ///
    /// Subscribing is exactly what makes them exist. So these vitals are read in the UI, which
    /// can subscribe, rather than in C#, which cannot. This file stays the single source of truth
    /// for which vitals exist and where each one's number comes from; the UI only does the
    /// plumbing.
    /// </summary>
    public sealed class VanillaBinding
    {
        public VanillaBinding(string group, string supply, string demand, VanillaKind kind,
            string supply2 = null, string demand2 = null)
        {
            Group = group;
            Supply = supply;
            Demand = demand;
            Kind = kind;
            Supply2 = supply2 ?? string.Empty;
            Demand2 = demand2 ?? string.Empty;
        }

        /// <summary>Vanilla's binding group, e.g. "electricityInfo".</summary>
        public string Group { get; }

        /// <summary>The capacity binding. Empty for every kind but Ratio.</summary>
        public string Supply { get; }

        /// <summary>The binding carrying the number, or the numerator for Ratio.</summary>
        public string Demand { get; }

        public VanillaKind Kind { get; }

        /// <summary>
        /// An optional second pair, added to the first before the ratio is taken. Jail and prison
        /// are one thing to the player - "somewhere to put criminals" - and two rows with the same
        /// icon were just noise.
        /// </summary>
        public string Supply2 { get; }

        public string Demand2 { get; }
    }

    /// <summary>How worried the player should be about a vital right now.</summary>
    public enum VitalLevel
    {
        Normal = 0,
        Warning = 1,
        Critical = 2
    }

    /// <summary>
    /// One entry in the strip.
    ///
    /// This is the whole data model of the mod. A vital with no threshold is a statistic; a
    /// vital with one is a warning. Metrics and alerts are deliberately the same record - see
    /// DESIGN.md - which is why this mod needs one list and one settings page rather than two.
    /// </summary>
    public sealed class Vital
    {
        public Vital(string id, VitalSource source, string title, string label, string icon,
            string[] infoviews, VitalFormat format, VitalThreshold threshold = null,
            VanillaBinding binding = null, bool defaultOn = true,
            Game.City.StatisticType? history = null, string historyLabel = null, string badge = null,
            bool invert = false, string factors = null)
        {
            Factors = factors ?? string.Empty;
            Invert = invert;
            Badge = badge ?? string.Empty;
            History = history;
            HistoryLabel = historyLabel;
            Binding = binding;
            DefaultOn = defaultOn;
            Id = id;
            Source = source;
            Title = title;
            Label = label;
            Icon = icon;
            Infoviews = infoviews ?? EmptyNames;
            Format = format;
            Threshold = threshold;
        }

        private static readonly string[] EmptyNames = new string[0];

        /// <summary>Stable key used by the settings and by the UI. Never localise this.</summary>
        public string Id { get; }

        /// <summary>Which live reading this shows.</summary>
        public VitalSource Source { get; }

        /// <summary>
        /// Full name, shown as a tooltip on hover. Once the strip became icons, this is the only
        /// thing that says what a row actually is.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Very short caption. Now a fallback rather than the primary display: the strip shows
        /// <see cref="Icon"/> and drops back to this text if the image fails to load.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// A vanilla game icon, as a path relative to the GameUI root
        /// (e.g. "Media/Game/Icons/Happy.svg"). Vanilla icons only - shipping our own set is a
        /// separate job, and borrowing another mod's is not an option.
        /// </summary>
        public string Icon { get; }

        /// <summary>
        /// A short rank drawn over the icon, or empty. Used where several rows share one icon
        /// because they are the same kind of thing at different levels - the four school tiers.
        /// </summary>
        public string Badge { get; }

        /// <summary>
        /// Show 100 minus the reading, so that a full green bar always means good.
        ///
        /// Half these figures are naturally negative - pollution, crime, cells occupied, schools
        /// full. Left as they are, the strip mixes two opposite conventions: a tall bar means
        /// "plenty of water" in one cell and "choking on smog" in the next, and the player has to
        /// remember which is which for every icon. Inverting the negative ones makes the rule
        /// absolute and unlearnable-wrong: **more is always better**.
        ///
        /// The threshold direction flips with it, so the colours stay correct.
        /// </summary>
        public bool Invert { get; }

        /// <summary>
        /// A vanilla binding in the same group carrying the top five reasons this figure is where
        /// it is, as {factor, weight} pairs. Set on the demand rows: the number says the demand is
        /// flat, this says why.
        ///
        /// Vanilla already sorts and truncates the list, so nothing is computed here.
        /// </summary>
        public string Factors { get; }

        /// <summary>
        /// Candidate names of the vanilla InfoviewPrefab to open when clicked, best first.
        ///
        /// A list rather than one name because the prefab names live in the packed asset
        /// database and cannot be enumerated outside a running game: the first candidate that
        /// actually resolves wins, and a vital where none resolve is simply not clickable. The
        /// resolved names are written to the log on first use, so the guesswork ends after one
        /// session rather than shipping a click that silently does nothing.
        /// </summary>
        public string[] Infoviews { get; }

        public VitalFormat Format { get; }

        /// <summary>Null means "this is a plain statistic, never a warning".</summary>
        public VitalThreshold Threshold { get; }

        /// <summary>Set only when <see cref="Source"/> is Vanilla. Null otherwise.</summary>
        public VanillaBinding Binding { get; }

        /// <summary>
        /// Whether a fresh install shows this one. The service and hazard rows are off to begin
        /// with: they are all genuinely useful, but twenty-one of them at once is a wall, not a
        /// glance. They are one tick away in the options.
        /// </summary>
        public bool DefaultOn { get; }

        /// <summary>
        /// The recorded series to chart when this row is expanded, if there is one.
        ///
        /// This is the legitimate use of CityStatisticsSystem: a sampled history is exactly what
        /// that system is for. But the series is not always the same quantity as the row - the
        /// row may show a rate where the statistic counts heads - so the chart is labelled with
        /// <see cref="HistoryLabel"/>, the statistic's own name, and never presented as the
        /// history of the number above it.
        ///
        /// Null where no series can be honestly matched. Happiness and health are the notable
        /// omissions: CitizenHappinessSystem enqueues WellbeingLevel and HealthLevel as a sum
        /// over citizens, so the series is a total while the row is an average.
        /// </summary>
        public Game.City.StatisticType? History { get; }

        /// <summary>What the charted series actually counts. Shown above the chart.</summary>
        public string HistoryLabel { get; }
    }

    /// <summary>How a raw value is turned into the short string shown in the strip.</summary>
    public enum VitalFormat
    {
        /// <summary>Plain count, abbreviated past a thousand.</summary>
        Number,

        /// <summary>Value is already 0-100 and is shown with a % sign.</summary>
        Percentage
    }

    /// <summary>
    /// Turns a metric into a warning. This is the piece that makes the design thesis real: the
    /// strip has one list, and an entry becomes an alert purely by having one of these.
    ///
    /// Thresholds are expressed in the vital's own unit, which is why every vital carrying one
    /// is a percentage: a raw count cannot be judged without knowing the size of the city, but
    /// "8% of citizens are homeless" means the same thing in a village and in a metropolis.
    /// </summary>
    public sealed class VitalThreshold
    {
        public VitalThreshold(float warning, float critical, bool lowIsBad)
        {
            Warning = warning;
            Critical = critical;
            LowIsBad = lowIsBad;
        }

        public float Warning { get; }

        public float Critical { get; }

        /// <summary>True when a LOW value is the bad one (happiness), false when a HIGH one is (unemployment).</summary>
        public bool LowIsBad { get; }

        public VitalLevel Evaluate(float value)
        {
            if (LowIsBad)
            {
                if (value <= Critical)
                {
                    return VitalLevel.Critical;
                }

                return value <= Warning ? VitalLevel.Warning : VitalLevel.Normal;
            }

            if (value >= Critical)
            {
                return VitalLevel.Critical;
            }

            return value >= Warning ? VitalLevel.Warning : VitalLevel.Normal;
        }
    }
}
