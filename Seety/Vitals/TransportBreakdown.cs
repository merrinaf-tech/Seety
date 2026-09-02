using System.Collections.Generic;
using Game.City;
using Game.Simulation;

namespace Seety.Vitals
{
    /// <summary>One transport mode and how much it carried.</summary>
    public sealed class TransportMode
    {
        public TransportMode(string id, string icon, StatisticType statistic, string action = "")
        {
            Id = id;
            Icon = icon;
            Statistic = statistic;
            Action = action;
        }

        public string Id { get; }
        public string Icon { get; }
        public StatisticType Statistic { get; }

        /// <summary>
        /// What clicking this row does, as understood by the UI: "passenger" or "cargo" to
        /// pre-select the mode in vanilla's transportation overview, empty for nothing.
        ///
        /// Only the pre-selection is possible. Vanilla exposes
        /// transportationOverview.setSelectedPassengerType and .setSelectedCargoType as triggers,
        /// but the panel's own open/closed state lives in the game's React UI and has no binding,
        /// so no mod can open it without reaching into vanilla's UI internals.
        /// </summary>
        public string Action { get; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Passengers and freight, split by mode.
    ///
    /// One row in the strip with everything behind it, rather than a dozen rows: the total answers
    /// "is anyone using my network", and the list answers "using what".
    ///
    /// These come from CityStatisticsSystem, which is correct here and not a relapse into the
    /// mistake VitalReader warns about. `TransportInfoviewUISystem.BindSummaries` builds vanilla's
    /// own per-mode summaries from exactly these statistics, so mirroring them is mirroring
    /// vanilla. They are also inherently per-period counts, which is what that system is for.
    /// </summary>
    public sealed class TransportBreakdown
    {
        private readonly List<TransportMode> _passengers = new List<TransportMode>
        {
            new TransportMode("Bus",      "Media/Game/Icons/Bus.svg",      StatisticType.PassengerCountBus, "passenger"),
            new TransportMode("Tram",     "Media/Game/Icons/Tram.svg",     StatisticType.PassengerCountTram, "passenger"),
            new TransportMode("Subway",   "Media/Game/Icons/Subway.svg",   StatisticType.PassengerCountSubway, "passenger"),
            new TransportMode("Train",    "Media/Game/Icons/Train.svg",    StatisticType.PassengerCountTrain, "passenger"),
            new TransportMode("Taxi",     "Media/Game/Icons/Taxi.svg",     StatisticType.PassengerCountTaxi, "passenger"),
            new TransportMode("Ferry",    "Media/Game/Icons/Ship.svg",     StatisticType.PassengerCountFerry, "passenger"),
            new TransportMode("Ship",     "Media/Game/Icons/Ship.svg",     StatisticType.PassengerCountShip, "passenger"),
            new TransportMode("Airplane", "Media/Game/Icons/Airplane.svg", StatisticType.PassengerCountAirplane, "passenger")
        };

        private readonly List<TransportMode> _cargo = new List<TransportMode>
        {
            new TransportMode("Cargo trucks",   "Media/Game/Icons/CargoTruck.svg",    StatisticType.CargoCountTruck, "cargo"),
            new TransportMode("Cargo trains",   "Media/Game/Icons/CargoTrain.svg",    StatisticType.CargoCountTrain, "cargo"),
            new TransportMode("Cargo ships",    "Media/Game/Icons/CargoShip.svg",     StatisticType.CargoCountShip, "cargo"),
            new TransportMode("Cargo aircraft", "Media/Game/Icons/CargoAirplane.svg", StatisticType.CargoCountAirplane, "cargo")
        };

        /// <summary>Passengers across every mode. The headline number on the strip.</summary>
        public int PassengerTotal { get; private set; }

        public IReadOnlyList<TransportMode> Passengers
        {
            get { return _passengers; }
        }

        public IReadOnlyList<TransportMode> Cargo
        {
            get { return _cargo; }
        }

        public void Refresh(CityStatisticsSystem statistics)
        {
            PassengerTotal = 0;

            if (statistics == null)
            {
                return;
            }

            foreach (var mode in _passengers)
            {
                mode.Count = statistics.GetStatisticValue(mode.Statistic);
                PassengerTotal += mode.Count;
            }

            foreach (var mode in _cargo)
            {
                mode.Count = statistics.GetStatisticValue(mode.Statistic);
            }
        }
    }
}
