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
        /// What clicking this row does, as understood by the UI: "passenger:X" or "cargo:X" to
        /// pre-select mode X in vanilla's transportation overview, empty for nothing.
        ///
        /// X has to be the exact name of a Game.Prefabs.TransportType member -
        /// setSelectedPassengerType/setSelectedCargoType store whatever string they are given
        /// verbatim, with no validation, and vanilla's own panel only recognises a line type by
        /// matching that string against Enum.GetName(typeof(TransportType), ...). A row's display
        /// label is not that name for every mode - cargo trucks are "Car" underneath, since
        /// TransportType has no separate truck member - so the two must not be the same string.
        ///
        /// Only the pre-selection is possible. Vanilla exposes those two triggers, but the panel's
        /// own open/closed state lives in the game's React UI and has no binding, so no mod can
        /// open it without reaching into vanilla's UI internals.
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
        // Every passenger label here happens to already match its TransportType member name
        // (Bus, Tram, Subway, Train, Taxi, Ferry, Ship, Airplane all exist verbatim on the enum),
        // which is what let the mismatch below hide for as long as it did.
        private readonly List<TransportMode> _passengers = new List<TransportMode>
        {
            new TransportMode("Bus",      "Media/Game/Icons/Bus.svg",      StatisticType.PassengerCountBus, "passenger:Bus"),
            new TransportMode("Tram",     "Media/Game/Icons/Tram.svg",     StatisticType.PassengerCountTram, "passenger:Tram"),
            new TransportMode("Subway",   "Media/Game/Icons/Subway.svg",   StatisticType.PassengerCountSubway, "passenger:Subway"),
            new TransportMode("Train",    "Media/Game/Icons/Train.svg",    StatisticType.PassengerCountTrain, "passenger:Train"),
            new TransportMode("Taxi",     "Media/Game/Icons/Taxi.svg",     StatisticType.PassengerCountTaxi, "passenger:Taxi"),
            new TransportMode("Ferry",    "Media/Game/Icons/Ship.svg",     StatisticType.PassengerCountFerry, "passenger:Ferry"),
            new TransportMode("Ship",     "Media/Game/Icons/Ship.svg",     StatisticType.PassengerCountShip, "passenger:Ship"),
            new TransportMode("Airplane", "Media/Game/Icons/Airplane.svg", StatisticType.PassengerCountAirplane, "passenger:Airplane")
        };

        // Cargo is where the labels and the underlying TransportType diverge. TransportType has
        // no member for a truck - Car is the only ground-vehicle value it defines - so "Cargo
        // trucks" resolves to Car, not to anything matching its own name. Train, Ship and Airplane
        // are shared with the passenger list and unambiguous.
        private readonly List<TransportMode> _cargo = new List<TransportMode>
        {
            new TransportMode("Cargo trucks",   "Media/Game/Icons/CargoTruck.svg",    StatisticType.CargoCountTruck, "cargo:Car"),
            new TransportMode("Cargo trains",   "Media/Game/Icons/CargoTrain.svg",    StatisticType.CargoCountTrain, "cargo:Train"),
            new TransportMode("Cargo ships",    "Media/Game/Icons/CargoShip.svg",     StatisticType.CargoCountShip, "cargo:Ship"),
            new TransportMode("Cargo aircraft", "Media/Game/Icons/CargoAirplane.svg", StatisticType.CargoCountAirplane, "cargo:Airplane")
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
