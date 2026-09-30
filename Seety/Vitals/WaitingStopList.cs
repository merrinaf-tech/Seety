using System.Collections.Generic;
using System.Globalization;
using Game.Prefabs;
using Game.UI;
using Unity.Collections;
using Unity.Entities;

namespace Seety.Vitals
{
    public sealed class WaitingStop
    {
        public Entity Route;
        public Entity Waypoint;
        public Entity PhysicalStop;
        public NameSystem.Name? Name;
        public NameSystem.Name? LineName;
        public string Colour;
        public int Number;
        public int Count;
        public string Id => Waypoint.Index.ToString(CultureInfo.InvariantCulture)
            + ":" + Waypoint.Version.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The busiest route waypoints of passenger lines, each counted from its own
    /// WaitingPassengers.m_Count. The station's TransportStop is only for the displayed name;
    /// its waiting count can cover several platforms or unrelated services.
    /// This follows the same RouteWaypoint -> Connected -> TransportStop chain that vanilla's
    /// LineVisualizerSection uses to draw a line's stops.
    /// </summary>
    public sealed class WaitingStopList
    {
        private const int MaxStops = 10;
        private readonly List<WaitingStop> _entries = new List<WaitingStop>();
        public IReadOnlyList<WaitingStop> Entries => _entries;

        public void Clear() => _entries.Clear();

        public void Refresh(EntityQuery lines, EntityManager entities, NameSystem names, bool rank)
        {
            if (rank)
            {
                _entries.Clear();
                using (var routes = lines.ToEntityArray(Allocator.Temp))
                {
                    for (int i = 0; i < routes.Length; i++)
                    {
                        Entity route = routes[i];
                        if (!IsPassengerLine(route, entities)) continue;
                        DynamicBuffer<Game.Routes.RouteWaypoint> waypoints =
                            entities.GetBuffer<Game.Routes.RouteWaypoint>(route, true);
                        for (int j = 0; j < waypoints.Length; j++)
                        {
                            Entity waypoint = waypoints[j].m_Waypoint;
                            Entity physicalStop = ConnectedStop(waypoint, entities);
                            int count = Count(waypoint, entities);
                            if (physicalStop == Entity.Null || count <= 0) continue;
                            _entries.Add(new WaitingStop
                            {
                                Route = route, Waypoint = waypoint,
                                PhysicalStop = physicalStop, Count = count
                            });
                        }
                    }
                }
                _entries.Sort((a, b) =>
                {
                    int order = b.Count.CompareTo(a.Count);
                    return order != 0 ? order : a.Waypoint.Index.CompareTo(b.Waypoint.Index);
                });
                if (_entries.Count > MaxStops) _entries.RemoveRange(MaxStops, _entries.Count - MaxStops);
            }

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (!IsPassengerLine(entry.Route, entities)
                    || ConnectedStop(entry.Waypoint, entities) != entry.PhysicalStop
                    || !OnRoute(entry.Route, entry.Waypoint, entities))
                {
                    _entries.RemoveAt(i);
                    continue;
                }
                entry.Count = Count(entry.Waypoint, entities);
                try { entry.Name = names.GetName(entry.PhysicalStop); }
                catch { entry.Name = null; }
                try { entry.LineName = names.GetName(entry.Route); }
                catch { entry.LineName = null; }
                entry.Colour = JourneyTrace.LineColour(entry.Route, entities);
                entry.Number = JourneyTrace.LineNumber(entry.Route, entities);
            }
        }

        private static bool IsPassengerLine(Entity route, EntityManager entities)
        {
            if (!Alive(route, entities)
                || !entities.HasComponent<Game.Routes.TransportLine>(route)
                || !entities.HasComponent<Game.Routes.Route>(route)
                || !entities.HasBuffer<Game.Routes.RouteWaypoint>(route)
                || !entities.HasComponent<PrefabRef>(route)) return false;
            Entity prefab = entities.GetComponentData<PrefabRef>(route).m_Prefab;
            if (!Alive(prefab, entities)
                || !entities.HasComponent<TransportLineData>(prefab)) return false;
            var data = entities.GetComponentData<TransportLineData>(prefab);
            return data.m_PassengerTransport && !data.m_CargoTransport;
        }

        private static bool OnRoute(Entity route, Entity waypoint, EntityManager entities)
        {
            var waypoints = entities.GetBuffer<Game.Routes.RouteWaypoint>(route, true);
            for (int i = 0; i < waypoints.Length; i++)
                if (waypoints[i].m_Waypoint == waypoint) return true;
            return false;
        }

        private static Entity ConnectedStop(Entity waypoint, EntityManager entities)
        {
            if (!Alive(waypoint, entities)
                || !entities.HasComponent<Game.Routes.Connected>(waypoint)) return Entity.Null;
            Entity stop = entities.GetComponentData<Game.Routes.Connected>(waypoint).m_Connected;
            return Alive(stop, entities) && entities.HasComponent<Game.Routes.TransportStop>(stop)
                && !entities.HasComponent<Game.Routes.TaxiStand>(stop)
                && !entities.HasComponent<Game.Objects.OutsideConnection>(stop)
                ? stop : Entity.Null;
        }

        private static int Count(Entity waypoint, EntityManager entities)
        {
            if (!Alive(waypoint, entities)
                || !entities.HasComponent<Game.Routes.WaitingPassengers>(waypoint)) return 0;
            return System.Math.Max(0, entities.GetComponentData<Game.Routes.WaitingPassengers>(waypoint).m_Count);
        }

        private static bool Alive(Entity entity, EntityManager entities) => entity != Entity.Null
            && entities.Exists(entity)
            && !entities.HasComponent<Game.Common.Deleted>(entity)
            && !entities.HasComponent<Game.Tools.Temp>(entity);

        public Entity LineFor(string id, EntityManager entities)
        {
            var entry = _entries.Find(row => row.Id == id);
            if (entry == null || !IsPassengerLine(entry.Route, entities)
                || ConnectedStop(entry.Waypoint, entities) != entry.PhysicalStop
                || !OnRoute(entry.Route, entry.Waypoint, entities)) return Entity.Null;
            return entry.Route;
        }
    }
}
