using System.Collections.Generic;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace Seety.Vitals
{
    /// <summary>Which vehicles a cargo row counts. Cargo prefabs carry no TransportType of their own.</summary>
    public enum CargoKind
    {
        None,
        Truck,
        Train,
        Ship,
        Aircraft
    }

    /// <summary>
    /// Which query a passenger row is filled from.
    ///
    /// Taxis are not public transport as the game models it: they carry Game.Vehicles.Taxi and
    /// their capacity lives on TaxiData, so a query built on PublicTransport misses them entirely
    /// and the row read 0/0 with taxis visibly driving around.
    /// </summary>
    public enum PassengerSource
    {
        PublicTransport,
        Taxi
    }

    /// <summary>One transport mode and how much it is carrying right now.</summary>
    public sealed class TransportMode
    {
        public TransportMode(string id, string icon, string action, TransportType type,
            CargoKind cargo = CargoKind.None,
            PassengerSource source = PassengerSource.PublicTransport)
        {
            Id = id;
            Icon = icon;
            Action = action;
            Type = type;
            Cargo = cargo;
            Source = source;
        }

        public string Id { get; }
        public string Icon { get; }

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
        /// own open/closed state lives in the game's React UI and has no binding.
        /// </summary>
        public string Action { get; }

        /// <summary>Which TransportType a passenger vehicle must declare to count here.</summary>
        public TransportType Type { get; }

        /// <summary>For a cargo row, which kind of vehicle counts. None on a passenger row.</summary>
        public CargoKind Cargo { get; }

        /// <summary>Which query fills a passenger row. See PassengerSource.</summary>
        public PassengerSource Source { get; }

        /// <summary>
        /// People aboard, or units of freight aboard, at this instant.
        ///
        /// A live count: the length of each vehicle's Passenger buffer, or the sum of its
        /// Resources buffer. Not a sampled statistic - see the note on the class.
        /// </summary>
        public int Aboard { get; set; }

        /// <summary>What the vehicles currently running this mode could carry between them.</summary>
        public int Capacity { get; set; }
    }

    /// <summary>
    /// What the city's transport network is carrying right now, split by mode.
    ///
    /// One row in the strip with everything behind it: the total answers "is anyone using my
    /// network", and the list answers "using what".
    ///
    /// **These are live counts, and they used to be sampled statistics.** The old figures came
    /// from CityStatisticsSystem's PassengerCount* series, which is what vanilla's own transport
    /// summaries are built from - but that series is a rolling per-period tally, so a busy line
    /// read high at three in the morning with nobody aboard, and the window needed a footnote
    /// explaining that its number did not mean what it looked like it meant.
    ///
    /// It can simply be counted instead. A vehicle's <c>Game.Vehicles.Passenger</c> buffer holds
    /// one entry per person aboard and its length is therefore the answer; a cargo vehicle's
    /// <c>Game.Economy.Resources</c> buffer holds what it is carrying. Both are plain components
    /// on the vehicle, so nothing here can be caught by the sleeping-binding trap, and the number
    /// now means exactly what the row says it means.
    ///
    /// This also agrees more closely with the game's own transportation overview, not less: that
    /// panel lists each line with the passengers on it at this moment, and these are those
    /// numbers added up per mode.
    /// </summary>
    public sealed class TransportBreakdown
    {
        // Every passenger label here happens to already match its TransportType member name,
        // which is what let the cargo mismatch below hide for as long as it did.
        private readonly List<TransportMode> _passengers = new List<TransportMode>
        {
            new TransportMode("Bus",      "Media/Game/Icons/Bus.svg",      "passenger:Bus",      TransportType.Bus),
            new TransportMode("Tram",     "Media/Game/Icons/Tram.svg",     "passenger:Tram",     TransportType.Tram),
            new TransportMode("Subway",   "Media/Game/Icons/Subway.svg",   "passenger:Subway",   TransportType.Subway),
            new TransportMode("Train",    "Media/Game/Icons/Train.svg",    "passenger:Train",    TransportType.Train),
            new TransportMode("Taxi",     "Media/Game/Icons/Taxi.svg",     "passenger:Taxi",     TransportType.Taxi, CargoKind.None, PassengerSource.Taxi),
            new TransportMode("Ferry",    "Media/Game/Icons/Ship.svg",     "passenger:Ferry",    TransportType.Ferry),
            new TransportMode("Ship",     "Media/Game/Icons/Ship.svg",     "passenger:Ship",     TransportType.Ship),
            new TransportMode("Airplane", "Media/Game/Icons/Airplane.svg", "passenger:Airplane", TransportType.Airplane)
        };

        // Cargo is where the labels and the underlying TransportType diverge. TransportType has no
        // member for a truck - Car is the only ground-vehicle value it defines - so "Cargo trucks"
        // resolves to Car, not to anything matching its own name.
        //
        // The counting side needs a different discriminator again: a cargo prefab carries
        // CargoTransportVehicleData, which has a capacity but no transport type, so which row a
        // cargo vehicle belongs to is decided by what kind of vehicle it is.
        private readonly List<TransportMode> _cargo = new List<TransportMode>
        {
            new TransportMode("Cargo trucks",   "Media/Game/Icons/CargoTruck.svg",    "cargo:Car",      TransportType.Car,      CargoKind.Truck),
            new TransportMode("Cargo trains",   "Media/Game/Icons/CargoTrain.svg",    "cargo:Train",    TransportType.Train,    CargoKind.Train),
            new TransportMode("Cargo ships",    "Media/Game/Icons/CargoShip.svg",     "cargo:Ship",     TransportType.Ship,     CargoKind.Ship),
            new TransportMode("Cargo aircraft", "Media/Game/Icons/CargoAirplane.svg", "cargo:Airplane", TransportType.Airplane, CargoKind.Aircraft)
        };

        /// <summary>Everyone riding public transport at this instant. The headline on the strip.</summary>
        public int PassengerTotal { get; private set; }

        public IReadOnlyList<TransportMode> Passengers
        {
            get { return _passengers; }
        }

        public IReadOnlyList<TransportMode> Cargo
        {
            get { return _cargo; }
        }

        /// <summary>
        /// Recounts what is aboard.
        ///
        /// One pass over the transit vehicles, which is a far smaller set than the notification
        /// walk the problem list used to do every tick - bounded by how many vehicles are running,
        /// not by how much is wrong with the city.
        /// </summary>
        public void Refresh(EntityQuery passengerVehicles, EntityQuery taxis,
            EntityQuery cargoVehicles, EntityQuery deliveryTrucks, EntityManager entities)
        {
            foreach (var mode in _passengers)
            {
                mode.Aboard = 0;
                mode.Capacity = 0;
            }

            foreach (var mode in _cargo)
            {
                mode.Aboard = 0;
                mode.Capacity = 0;
            }

            CountPassengers(passengerVehicles, entities);
            CountTaxis(taxis, entities);
            CountCargo(cargoVehicles, entities);
            CountDeliveryTrucks(deliveryTrucks, entities);

            PassengerTotal = 0;
            foreach (var mode in _passengers)
            {
                PassengerTotal += mode.Aboard;
            }
        }

        private void CountPassengers(EntityQuery query, EntityManager entities)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var vehicle in vehicles)
                {
                    var prefab = entities.GetComponentData<PrefabRef>(vehicle).m_Prefab;
                    if (!entities.HasComponent<PublicTransportVehicleData>(prefab))
                    {
                        continue;
                    }

                    var data = entities.GetComponentData<PublicTransportVehicleData>(prefab);

                    foreach (var mode in _passengers)
                    {
                        if (mode.Type != data.m_TransportType)
                        {
                            continue;
                        }

                        // The buffer holds one entry per person aboard, so its length is the count.
                        if (entities.HasBuffer<Game.Vehicles.Passenger>(vehicle))
                        {
                            mode.Aboard += entities.GetBuffer<Game.Vehicles.Passenger>(vehicle, true).Length;
                        }

                        mode.Capacity += data.m_PassengerCapacity;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Taxis, which the game does not model as public transport - see PassengerSource.
        /// Passengers are counted from the same buffer; only the capacity lives elsewhere.
        /// </summary>
        private void CountTaxis(EntityQuery query, EntityManager entities)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            TransportMode row = null;
            foreach (var mode in _passengers)
            {
                if (mode.Source == PassengerSource.Taxi)
                {
                    row = mode;
                    break;
                }
            }

            if (row == null)
            {
                return;
            }

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var vehicle in vehicles)
                {
                    var prefab = entities.GetComponentData<PrefabRef>(vehicle).m_Prefab;
                    if (!entities.HasComponent<TaxiData>(prefab))
                    {
                        continue;
                    }

                    if (entities.HasBuffer<Game.Vehicles.Passenger>(vehicle))
                    {
                        row.Aboard += entities.GetBuffer<Game.Vehicles.Passenger>(vehicle, true).Length;
                    }

                    row.Capacity += entities.GetComponentData<TaxiData>(prefab).m_PassengerCapacity;
                }
            }
        }

        /// <summary>
        /// Delivery trucks, which are the "cargo trucks" row.
        ///
        /// They are not CargoTransport - that component is for vehicles running a cargo line, so
        /// the row stayed empty in a city full of trucks. A delivery truck also carries its load
        /// on the component itself rather than in a Resources buffer.
        /// </summary>
        private void CountDeliveryTrucks(EntityQuery query, EntityManager entities)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            TransportMode row = null;
            foreach (var mode in _cargo)
            {
                if (mode.Cargo == CargoKind.Truck)
                {
                    row = mode;
                    break;
                }
            }

            if (row == null)
            {
                return;
            }

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var vehicle in vehicles)
                {
                    var prefab = entities.GetComponentData<PrefabRef>(vehicle).m_Prefab;
                    if (!entities.HasComponent<DeliveryTruckData>(prefab))
                    {
                        continue;
                    }

                    row.Aboard += entities.GetComponentData<Game.Vehicles.DeliveryTruck>(vehicle).m_Amount;
                    row.Capacity += entities.GetComponentData<DeliveryTruckData>(prefab).m_CargoCapacity;
                }
            }
        }

        private void CountCargo(EntityQuery query, EntityManager entities)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var vehicle in vehicles)
                {
                    var prefab = entities.GetComponentData<PrefabRef>(vehicle).m_Prefab;
                    if (!entities.HasComponent<CargoTransportVehicleData>(prefab))
                    {
                        continue;
                    }

                    var kind = KindOf(entities, vehicle);
                    if (kind == CargoKind.None)
                    {
                        continue;
                    }

                    var data = entities.GetComponentData<CargoTransportVehicleData>(prefab);

                    foreach (var mode in _cargo)
                    {
                        if (mode.Cargo != kind)
                        {
                            continue;
                        }

                        if (entities.HasBuffer<Game.Economy.Resources>(vehicle))
                        {
                            var held = entities.GetBuffer<Game.Economy.Resources>(vehicle, true);
                            for (var i = 0; i < held.Length; i++)
                            {
                                mode.Aboard += held[i].m_Amount;
                            }
                        }

                        mode.Capacity += data.m_CargoCapacity;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Which cargo row a vehicle belongs to.
        ///
        /// Decided from what the vehicle IS rather than from a transport type, because
        /// CargoTransportVehicleData does not carry one. The same four components the traffic
        /// jam list already distinguishes vehicles by.
        /// </summary>
        private static CargoKind KindOf(EntityManager entities, Entity vehicle)
        {
            if (entities.HasComponent<Game.Vehicles.Train>(vehicle))
            {
                return CargoKind.Train;
            }

            if (entities.HasComponent<Game.Vehicles.Watercraft>(vehicle))
            {
                return CargoKind.Ship;
            }

            if (entities.HasComponent<Game.Vehicles.Aircraft>(vehicle))
            {
                return CargoKind.Aircraft;
            }

            // No Car branch: a vehicle running a cargo LINE is a train, a ship or an aircraft.
            // Road freight is a DeliveryTruck and has its own pass - see CountDeliveryTrucks.
            return CargoKind.None;
        }
    }
}
