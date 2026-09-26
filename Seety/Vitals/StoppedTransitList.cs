using System.Collections.Generic;
using System.Globalization;
using Game.Rendering;
using Game.UI;
using Unity.Collections;
using Unity.Entities;

namespace Seety.Vitals
{
    /// <summary>
    /// Public transport standing still, grouped by the line it belongs to.
    ///
    /// The city moves four kinds of vehicle - Car, Train, Watercraft, Aircraft - and a transit
    /// line can be any of them: a bus and a tram are both here, a tram and a metro are both
    /// Train. So this list is built over <see cref="Game.Vehicles.PublicTransport"/>, which is
    /// what marks the role rather than the movement, and covers every mode in one pass.
    ///
    /// <para>
    /// Stopped means standing still, read from the vehicle's own speed: TrainNavigation for rail,
    /// Moving for everything else. <see cref="Game.Vehicles.Blocker"/> is no help: Game.dll adds it
    /// and never removes it, so every vehicle carries one. Reading its presence as "held up" listed
    /// each line's whole fleet, moving or not. A vehicle standing at a red light or a signal is
    /// counted - the game does not say why something has stopped, only that it has.
    /// </para>
    ///
    /// <para>
    /// A train or tram is one vehicle to the player but one entity per carriage to the game, each
    /// pointing at the lead one through <see cref="Game.Vehicles.Controller"/>. Counting entities
    /// listed a single seven-carriage train as seven stopped vehicles. Every carriage is resolved
    /// to its train and each train is counted once, and the train's speed is read from
    /// <see cref="Game.Vehicles.TrainNavigation"/>. With no speed to read at all, a vehicle is not
    /// assumed to be standing: that assumption is what put a moving train in the list.
    /// </para>
    ///
    /// <para>
    /// Boarding is excluded for every kind. <see cref="Game.Vehicles.PublicTransportFlags"/>
    /// carries it on all of them, so a vehicle doing its job at a stop is never reported, and
    /// that part is the game talking rather than Seety guessing.
    /// </para>
    /// </summary>
    public sealed class StoppedTransitList
    {
        /// <summary>
        /// States in which a motionless vehicle is not a problem with a line.
        ///
        /// This is the fix for a list that ended with rows nobody could see a jam behind. A
        /// vehicle at the depot refuelling, one on its way back, one out of service or on its way
        /// off a route is standing still for a reason that has nothing to do with congestion, and
        /// the game says which is which - so the filter reads its flags rather than guessing from
        /// position or from how many vehicles share a row.
        ///
        /// Boarding is in here for the same reason: a vehicle at a stop is doing its job.
        ///
        /// RequiresMaintenance is deliberately NOT in here. It says a vehicle needs servicing,
        /// not that it has stopped serving, and a bus that is due for maintenance can be stuck in
        /// the same queue as any other. Filtering it would hide real cases to tidy the list.
        /// </summary>
        private const Game.Vehicles.PublicTransportFlags OutOfService =
            Game.Vehicles.PublicTransportFlags.Boarding
            | Game.Vehicles.PublicTransportFlags.Returning
            | Game.Vehicles.PublicTransportFlags.Refueling
            | Game.Vehicles.PublicTransportFlags.AbandonRoute
            | Game.Vehicles.PublicTransportFlags.Disabled
            | Game.Vehicles.PublicTransportFlags.DummyTraffic
            | Game.Vehicles.PublicTransportFlags.Testing;

        /// <summary>Below this, in metres per second, a rail vehicle is standing rather than crawling.</summary>
        private const float StoppedSpeed = 0.1f;

        /// <summary>Same ceiling as the jam list: past this the list stops being readable.</summary>
        private const int MaxGroups = 10;

        private readonly List<TrafficJamGroup> _groups = new List<TrafficJamGroup>();
        private readonly Dictionary<string, TrafficJamGroup> _byId = new Dictionary<string, TrafficJamGroup>();

        /// <summary>Trains already decided on in this refresh. Kept to avoid allocating per call.</summary>
        private readonly HashSet<Entity> _seenUnits = new HashSet<Entity>();

        public IReadOnlyList<TrafficJamGroup> Groups
        {
            get { return _groups; }
        }

        public void Refresh(EntityQuery query, EntityManager entities, NameSystem names)
        {
            _groups.Clear();
            _byId.Clear();
            _seenUnits.Clear();

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicles.Length; i++)
                {
                    var vehicle = vehicles[i];
                    var unit = UnitOf(vehicle, entities);

                    // Decided once per train, whichever carriage the query hands over first.
                    if (!_seenUnits.Add(unit))
                    {
                        continue;
                    }

                    if (!Stopped(vehicle, unit, entities))
                    {
                        continue;
                    }

                    string id;
                    Entity route;
                    string label = Where(vehicle, entities, names, out id, out route)
                                   ?? Where(unit, entities, names, out id, out route);

                    if (string.IsNullOrEmpty(label))
                    {
                        continue;
                    }

                    TrafficJamGroup group;
                    if (!_byId.TryGetValue(id, out group))
                    {
                        group = new TrafficJamGroup { Id = id, Name = label, Count = 0, Route = route };

                        // The first one found sets where a click goes. Any of them is on the same
                        // line, and averaging their positions can point at a spot between two
                        // vehicles where there is nothing to look at.
                        if (entities.HasComponent<Game.Objects.Transform>(unit))
                        {
                            group.Position = entities
                                .GetComponentData<Game.Objects.Transform>(unit).m_Position;
                        }

                        _byId[id] = group;
                        _groups.Add(group);
                    }

                    group.Count++;
                }
            }

            _groups.Sort((a, b) => b.Count.CompareTo(a.Count));

            if (_groups.Count > MaxGroups)
            {
                _groups.RemoveRange(MaxGroups, _groups.Count - MaxGroups);
            }
        }

        /// <summary>
        /// Held up, rather than doing its job at a stop. See the note on the class for why this
        /// asks a different question of rail than of everything else.
        /// </summary>
        private static bool Stopped(Entity vehicle, Entity unit, EntityManager entities)
        {
            if (OutOfServiceState(vehicle, entities) || OutOfServiceState(unit, entities))
            {
                return false;
            }

            if (entities.HasComponent<Game.Vehicles.TrainNavigation>(unit))
            {
                var speed = entities.GetComponentData<Game.Vehicles.TrainNavigation>(unit).m_Speed;
                return Unity.Mathematics.math.abs(speed) < StoppedSpeed;
            }

            if (entities.HasComponent<Game.Objects.Moving>(unit))
            {
                var velocity = entities.GetComponentData<Game.Objects.Moving>(unit).m_Velocity;
                return Unity.Mathematics.math.lengthsq(velocity) < StoppedSpeed * StoppedSpeed;
            }

            return false;
        }

        private static bool OutOfServiceState(Entity vehicle, EntityManager entities)
        {
            return entities.HasComponent<Game.Vehicles.PublicTransport>(vehicle)
                && (entities.GetComponentData<Game.Vehicles.PublicTransport>(vehicle).m_State
                    & OutOfService) != 0;
        }

        /// <summary>
        /// The train a carriage belongs to, or the vehicle itself when it is not part of one.
        /// </summary>
        private static Entity UnitOf(Entity vehicle, EntityManager entities)
        {
            if (!entities.HasComponent<Game.Vehicles.Controller>(vehicle))
            {
                return vehicle;
            }

            var controller = entities.GetComponentData<Game.Vehicles.Controller>(vehicle).m_Controller;
            return controller != Entity.Null && entities.Exists(controller) ? controller : vehicle;
        }

        /// <summary>
        /// What to call the row, and the id a click uses.
        ///
        /// The line, and only the line: that is what a player manages, and it is what makes
        /// several stopped vehicles one problem rather than several.
        /// </summary>
        private static string Where(Entity vehicle, EntityManager entities, NameSystem names,
            out string id, out Entity route)
        {
            id = null;
            route = Entity.Null;

            if (entities.HasComponent<Game.Routes.CurrentRoute>(vehicle))
            {
                route = entities.GetComponentData<Game.Routes.CurrentRoute>(vehicle).m_Route;
                string line = SafeName(names, route);

                if (!string.IsNullOrEmpty(line))
                {
                    id = "line:" + Reference(route);
                    return line;
                }
            }

            // No fallback to the road or track underneath. A transit vehicle that is on no line
            // is not a line with a problem - it is a vehicle between jobs - and naming it after
            // the street it happens to be parked on is what filled the end of the list with rows
            // that had no jam behind them.
            return null;
        }

        /// <summary>Index and version both, so a click cannot land on a recycled slot.</summary>
        private static string Reference(Entity entity)
        {
            return entity.Index.ToString(CultureInfo.InvariantCulture)
                + ":" + entity.Version.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Moves the camera to one of them. Same contract as the jam list.</summary>
        public bool Jump(string id, CameraUpdateSystem camera)
        {
            TrafficJamGroup group;
            if (string.IsNullOrEmpty(id) || !_byId.TryGetValue(id, out group))
            {
                return false;
            }

            if (camera == null || camera.activeCameraController == null)
            {
                return false;
            }

            var target = group.Position;
            camera.activeCameraController.pivot =
                new UnityEngine.Vector3(target.x, target.y, target.z);
            return true;
        }

        private static string SafeName(NameSystem names, Entity entity)
        {
            if (entity == Entity.Null)
            {
                return null;
            }

            try
            {
                return names.GetRenderedLabelName(entity);
            }
            catch
            {
                return null;
            }
        }
    }
}
