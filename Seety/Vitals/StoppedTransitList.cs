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
    /// Being stopped is not one question, because the game does not answer it the same way for
    /// every kind. CarNavigationSystem, WatercraftNavigationSystem and AircraftNavigationSystem
    /// each write <see cref="Game.Vehicles.Blocker"/> when something is in the way - the same
    /// component the road jam list is built on. No train navigation system writes it, and
    /// TrainFlags carries no blocked state, so rail is the one kind with nothing to read.
    /// </para>
    ///
    /// <para>
    /// So the game's own signal is used wherever it exists, and only rail falls back to "no
    /// speed". That matters for accuracy in both directions: a bus at a red light has no speed
    /// but no Blocker either, and requiring Blocker keeps it out of the list, while a metro held
    /// at a signal will appear - which is the honest cost of the game not saying.
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

        public IReadOnlyList<TrafficJamGroup> Groups
        {
            get { return _groups; }
        }

        public void Refresh(EntityQuery query, EntityManager entities, NameSystem names)
        {
            _groups.Clear();
            _byId.Clear();

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicles.Length; i++)
                {
                    var vehicle = vehicles[i];

                    if (!Stopped(vehicle, entities))
                    {
                        continue;
                    }

                    string id;
                    string label = Where(vehicle, entities, names, out id);

                    if (string.IsNullOrEmpty(label))
                    {
                        continue;
                    }

                    TrafficJamGroup group;
                    if (!_byId.TryGetValue(id, out group))
                    {
                        group = new TrafficJamGroup { Id = id, Name = label, Count = 0 };

                        // The first one found sets where a click goes. Any of them is on the same
                        // line, and averaging their positions can point at a spot between two
                        // vehicles where there is nothing to look at.
                        if (entities.HasComponent<Game.Objects.Transform>(vehicle))
                        {
                            group.Position = entities
                                .GetComponentData<Game.Objects.Transform>(vehicle).m_Position;
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
        private static bool Stopped(Entity vehicle, EntityManager entities)
        {
            if (entities.HasComponent<Game.Vehicles.PublicTransport>(vehicle))
            {
                var state = entities
                    .GetComponentData<Game.Vehicles.PublicTransport>(vehicle).m_State;

                if ((state & OutOfService) != 0)
                {
                    return false;
                }
            }

            // The game's own answer, for the three kinds that have one.
            if (entities.HasComponent<Game.Vehicles.Blocker>(vehicle))
            {
                return true;
            }

            if (!entities.HasComponent<Game.Vehicles.Train>(vehicle))
            {
                return false;
            }

            // Rail only: nothing writes Blocker for it, so speed is all there is to go on. A
            // missing Moving component is the strongest form of not moving.
            if (!entities.HasComponent<Game.Objects.Moving>(vehicle))
            {
                return true;
            }

            var velocity = entities.GetComponentData<Game.Objects.Moving>(vehicle).m_Velocity;
            return Unity.Mathematics.math.lengthsq(velocity) < StoppedSpeed * StoppedSpeed;
        }

        /// <summary>
        /// What to call the row, and the id a click uses.
        ///
        /// The line, and only the line: that is what a player manages, and it is what makes
        /// several stopped vehicles one problem rather than several.
        /// </summary>
        private static string Where(Entity vehicle, EntityManager entities, NameSystem names,
            out string id)
        {
            id = null;

            if (entities.HasComponent<Game.Routes.CurrentRoute>(vehicle))
            {
                Entity route = entities.GetComponentData<Game.Routes.CurrentRoute>(vehicle).m_Route;
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
