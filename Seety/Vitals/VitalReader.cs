using Game.City;
using Game.Notifications;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;

namespace Seety.Vitals
{
    /// <summary>
    /// Reads the current value of a vital.
    ///
    /// The rule here has one line: **mirror whatever source the vanilla UI uses for that exact
    /// figure**. Not "always read components" - per figure. Every entry in the strip opens a
    /// vanilla panel when clicked, so any entry that disagrees with the panel behind it makes the
    /// mod look broken even when the number is defensible.
    ///
    /// Mostly that means components, because v0.1 read everything through
    /// CityStatisticsSystem.GetStatisticValueLong and that was wrong twice over:
    ///
    ///   * it returns buffer[last].m_TotalValue - the last entry of a sampled time series, and
    ///     that system only samples every 8192 frames, so the strip lagged behind the HUD;
    ///   * m_TotalValue is a TOTAL. Wellbeing and Health are sums over citizens, not averages,
    ///     so formatting them as a percentage produced readings like "14616%".
    ///
    /// Tourists are the exception, and only because vanilla itself is the exception:
    /// TourismInfoviewUISystem binds `tourismInfo.tourismRate` from
    /// GetStatisticValue(StatisticType.TouristCount). The live equivalent exists
    /// (Tourism.m_CurrentTourists, which TourismSystem assigns from m_TouristCitizenCount) and
    /// would be more current - but it would not match the panel the entry opens, so it loses.
    /// </summary>
    public sealed class VitalReader
    {
        private readonly EntityManager _entities;
        private readonly CitySystem _city;
        private readonly CountHouseholdDataSystem _households;
        private readonly CityStatisticsSystem _statistics;
        private readonly WaterStatisticsSystem _water;

        public VitalReader(EntityManager entities, CitySystem city, CountHouseholdDataSystem households,
            CityStatisticsSystem statistics, WaterStatisticsSystem water)
        {
            _water = water;
            _entities = entities;
            _city = city;
            _households = households;
            _statistics = statistics;
        }

        /// <summary>
        /// The current value, or 0 when the city is not loaded yet. Never throws: a vital that
        /// cannot be read is shown as zero rather than taking the strip down.
        /// </summary>
        public float Read(VitalSource source)
        {
            switch (source)
            {
                case VitalSource.Happiness:
                    return TryGetPopulation().m_AverageHappiness;

                case VitalSource.Health:
                    return TryGetPopulation().m_AverageHealth;

                case VitalSource.Unemployment:
                    return _households == null ? 0f : _households.UnemploymentRate;

                case VitalSource.Homelessness:
                    return _households == null ? 0f : _households.HomelessnessRate;

                case VitalSource.Workers:
                    return _households == null ? 0f : _households.CityWorkerCount;

                case VitalSource.Tourists:
                    // The one statistic Seety reads, because it is the one vanilla reads here.
                    return _statistics == null ? 0f : _statistics.GetStatisticValue(StatisticType.TouristCount);

                case VitalSource.Problems:
                    return ProblemCount;

                case VitalSource.WaterServed:
                    return _water == null
                        ? 0f
                        : Served(_water.fulfilledFreshConsumption, _water.freshConsumption);

                case VitalSource.SewageServed:
                    return _water == null
                        ? 0f
                        : Served(_water.fulfilledSewageConsumption, _water.sewageConsumption);

                case VitalSource.Vanilla:
                    // Computed in the UI, which is the only side that can subscribe to vanilla's
                    // bindings and thereby make those systems produce a value at all.
                    return 0f;

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// How much of a demand was actually met. A city asking for nothing is fully served, which
        /// keeps a brand new city off the red before anyone has turned on a tap.
        /// </summary>
        private static float Served(int fulfilled, int demanded)
        {
            if (demanded <= 0)
            {
                return 100f;
            }

            var share = 100f * fulfilled / demanded;
            return share > 100f ? 100f : share;
        }

        /// <summary>Active notification icons at Problem severity or worse, as of the last refresh.</summary>
        public int ProblemCount { get; private set; }

        /// <summary>
        /// Severity of the worst active notification.
        ///
        /// Problems is the one vital whose colour does not come from a threshold of ours. The
        /// game already grades every notification on its own scale (IconPriority: Info 10,
        /// Problem 50, Warning 100, MajorProblem 150, Error 200, FatalProblem 250), and its
        /// judgement of what counts as serious is better than any number we could invent.
        /// </summary>
        public VitalLevel ProblemLevel { get; private set; }

        /// <summary>
        /// Recounts the city's notifications. Called once per refresh, and only when the
        /// problems vital is actually switched on, because unlike every other reading here this
        /// one walks a collection rather than fetching a field.
        /// </summary>
        public void RefreshProblems(EntityQuery iconQuery)
        {
            ProblemCount = 0;
            ProblemLevel = VitalLevel.Normal;

            if (iconQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var worst = IconPriority.Min;

            using (var icons = iconQuery.ToComponentDataArray<Icon>(Allocator.Temp))
            {
                for (var i = 0; i < icons.Length; i++)
                {
                    var priority = icons[i].m_Priority;

                    // Info-level icons are things like "this is under construction". Counting
                    // them would turn a healthy city into a wall of numbers.
                    if (priority < IconPriority.Problem)
                    {
                        continue;
                    }

                    ProblemCount++;

                    if (priority > worst)
                    {
                        worst = priority;
                    }
                }
            }

            if (worst >= IconPriority.MajorProblem)
            {
                ProblemLevel = VitalLevel.Critical;
            }
            else if (worst >= IconPriority.Problem)
            {
                ProblemLevel = VitalLevel.Warning;
            }
        }

        /// <summary>
        /// Whether the city has anyone living in it yet.
        ///
        /// This matters for thresholds, not for the numbers. An empty city reports 0% happiness
        /// and 0% health - vanilla's own averages divide by the citizen count and return 0 when
        /// it is zero - and judging those against a "low is bad" threshold would paint the whole
        /// strip red before the first house is built. Nothing is wrong; there is simply nothing
        /// to report yet.
        /// </summary>
        public bool HasCitizens
        {
            get { return TryGetPopulation().m_Population > 0; }
        }

        /// <summary>
        /// Population carries the city-wide averages the HUD shows. Both m_AverageHappiness and
        /// m_AverageHealth are already on a 0-100 scale, so no conversion is needed.
        /// </summary>
        private Population TryGetPopulation()
        {
            var city = City();
            if (city == Entity.Null || !_entities.HasComponent<Population>(city))
            {
                return default(Population);
            }

            return _entities.GetComponentData<Population>(city);
        }

        /// <summary>The city entity does not exist until a save is loaded.</summary>
        private Entity City()
        {
            return _city == null ? Entity.Null : _city.City;
        }
    }
}
