using System.Collections.Generic;
using System.Globalization;
using Game.Rendering;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Seety.Vitals
{
    /// <summary>One pile-up: where it is, how big it is, and what street to call it.</summary>
    public sealed class TrafficJamGroup
    {
        /// <summary>The grid location, independent of the street label and list order.</summary>
        public string Id;

        internal long Cell;

        /// <summary>The street it is on, made unique if two jams share a name. Shown as the row.</summary>
        public string Name;

        /// <summary>Stuck vehicles in it.</summary>
        public int Count;

        /// <summary>Where the camera goes.</summary>
        public float3 Position;

        /// <summary>The transit line, for rows that are one. Its name is bound by the game's own
        /// NameSystem so the UI can show it the way the vanilla line list does.</summary>
        public Entity Route;
    }

    /// <summary>
    /// The city's traffic jams, worst first.
    ///
    /// <see cref="Game.Vehicles.Blocker"/> does not mean blocked. Game.dll adds it and never removes
    /// it, so every car on the road carries one; whether something is in the way is in its fields
    /// (BlockerType has nine values as of 1.6.2f1, None among them). Taking its presence as "stuck"
    /// made this list count moving traffic, and only the size threshold hid that. So a vehicle
    /// counts only when it is standing or crawling, read from its own speed.
    ///
    /// And one stopped vehicle is not a jam: a car at a red light or giving way at a junction is
    /// standing just as still as one in a half-mile queue.
    ///
    /// So a jam is defined by crowding rather than by any flag: vehicles stopped close together,
    /// in numbers. That is what a player means by the word, and it is the only thing in the data
    /// that separates a queue from a give-way.
    ///
    /// Grouping used to be by vehicle model, which was worse than useless. A queue of thirty cars
    /// is a mix of models, so it arrived as ten rows of two or three - the jam was real and
    /// visible on screen while the panel reported its largest knot as four. Rows were also named
    /// things like "EU_TruckTractor01". Jams are places, so they are grouped by place and named
    /// after the street.
    /// </summary>
    public sealed class TrafficJamBreakdown
    {
        /// <summary>Only the worst handful are worth a row - see NotificationBreakdown.MaxProblemRows.</summary>
        private const int MaxGroups = 10;

        /// <summary>
        /// The grid stopped vehicles are sorted into, in metres. Two occupied cells next to each
        /// other on the same street are one queue; an empty cell between them - a stretch of road
        /// with nothing stopped on it - ends the queue.
        /// </summary>
        private const float CellSize = 32f;

        /// <summary>
        /// Metres per second under which a car is part of a queue: about 11 km/h. A long queue
        /// moves in waves, and at 1 m/s most of a stop-and-go queue of trucks was read as moving
        /// at any given instant, so a jam visible on screen went unreported.
        /// </summary>
        private const float QueueSpeed = 3f;

        /// <summary>
        /// Minimum vehicles in one queue. Below this, a queue is a red light or a give-way.
        /// </summary>
        private const int MinJamSize = 15;

        private readonly List<TrafficJamGroup> _groups = new List<TrafficJamGroup>();
        private readonly Dictionary<string, TrafficJamGroup> _byId = new Dictionary<string, TrafficJamGroup>();

        public IReadOnlyList<TrafficJamGroup> Groups
        {
            get { return _groups; }
        }

        public void Refresh(EntityQuery query, EntityManager entities,
            NameSystem names, Game.Simulation.TerrainSystem terrain)
        {
            _groups.Clear();
            _byId.Clear();

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            UnityEngine.Bounds map = terrain != null
                ? terrain.GetTerrainBounds()
                : new UnityEngine.Bounds();
            bool bounded = map.size.x > 0f && map.size.z > 0f;

            var positions = new List<float3>();
            var roads = new List<Entity>();
            var streets = new List<Entity>();

            // Many cars share a lane, and resolving one means several component lookups, so the
            // answer is remembered for the length of this refresh.
            var laneRoads = new Dictionary<Entity, Entity>();

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicles.Length; i++)
                {
                    var vehicle = vehicles[i];
                    if (!entities.HasComponent<Game.Objects.Transform>(vehicle))
                    {
                        continue;
                    }

                    if (entities.HasComponent<Game.Objects.Moving>(vehicle)
                        && math.lengthsq(entities.GetComponentData<Game.Objects.Moving>(vehicle).m_Velocity)
                           >= QueueSpeed * QueueSpeed)
                    {
                        continue;
                    }

                    var position = entities.GetComponentData<Game.Objects.Transform>(vehicle).m_Position;

                    if (bounded && !OnMap(position, map))
                    {
                        // Not traffic in this city. Vehicles queue at the outside connections in
                        // dense stacks, and crowding is exactly what is being looked for here, so
                        // those stacks would outrank every real jam on the map.
                        continue;
                    }

                    Entity road = RoadOf(vehicle, entities, laneRoads);
                    if (road == Entity.Null)
                    {
                        // Standing somewhere that is not a road: the car parks inside buildings,
                        // mostly. They are stationary and packed tighter than any queue, so they
                        // won every round of the search below, and they are the one place a
                        // player can do nothing about - you cannot widen a garage.
                        continue;
                    }

                    positions.Add(position);
                    roads.Add(road);
                    streets.Add(StreetOf(road, entities));
                }
            }

            FindJams(positions, roads, streets, entities, names);
        }

        /// <summary>
        /// Picks the queues out of a scatter of stopped vehicles.
        ///
        /// Each stopped vehicle goes into a 32 m cell, kept apart by street. Occupied cells that
        /// touch and belong to the same street are one queue, however long: a jam is a line of
        /// vehicles along a road, and the old test - fifteen inside one 96 m square - missed a
        /// queue of spaced-out trucks stretching for hundreds of metres.
        ///
        /// Joining only along the same street is what keeps this from the flood fill that once
        /// grew through every junction downtown into one 800-vehicle "jam": a queue stops where
        /// its street meets another, and where the road ahead of it is clear. Roads belonging to
        /// no named street (slip roads, pieces of interchange) count as one street together, so a
        /// queue running down a ramp of several pieces stays whole.
        /// </summary>
        private void FindJams(List<float3> positions, List<Entity> roads, List<Entity> streets,
            EntityManager entities, NameSystem names)
        {
            var cells = new Dictionary<(long Cell, Entity Street), List<int>>();

            for (var i = 0; i < positions.Count; i++)
            {
                var key = (CellKey(positions[i]), streets[i]);

                List<int> bucket;
                if (!cells.TryGetValue(key, out bucket))
                {
                    bucket = new List<int>();
                    cells.Add(key, bucket);
                }

                bucket.Add(i);
            }

            var seen = new HashSet<(long Cell, Entity Street)>();
            var found = new List<TrafficJamGroup>();
            var pending = new Stack<(long Cell, Entity Street)>();

            foreach (var start in cells.Keys)
            {
                if (!seen.Add(start))
                {
                    continue;
                }

                var members = new List<int>();
                long busiestCell = start.Cell;
                int busiestCount = -1;
                pending.Push(start);

                while (pending.Count > 0)
                {
                    var node = pending.Pop();
                    var bucket = cells[node];
                    members.AddRange(bucket);

                    // The busiest cell names the queue's location, so its identity survives the
                    // queue growing or shrinking at either end. Ties go to the lower key, so the
                    // order the query returned cars in decides nothing.
                    if (bucket.Count > busiestCount
                        || (bucket.Count == busiestCount && node.Cell < busiestCell))
                    {
                        busiestCell = node.Cell;
                        busiestCount = bucket.Count;
                    }

                    foreach (long neighbour in NeighbourKeys(node.Cell))
                    {
                        var next = (neighbour, node.Street);
                        if (cells.ContainsKey(next) && seen.Add(next))
                        {
                            pending.Push(next);
                        }
                    }
                }

                if (members.Count < MinJamSize)
                {
                    continue;
                }

                found.Add(new TrafficJamGroup
                {
                    Id = busiestCell.ToString(CultureInfo.InvariantCulture) + ":"
                        + start.Street.Index.ToString(CultureInfo.InvariantCulture),
                    Cell = busiestCell,
                    Name = StreetName(roads[members[0]], entities, names),
                    Count = members.Count,
                    Position = Centre(positions, members)
                });
            }

            found.Sort((x, y) => x.Count != y.Count
                ? y.Count.CompareTo(x.Count)
                : string.CompareOrdinal(x.Id, y.Id));
            for (int i = 0; i < found.Count && i < MaxGroups; i++)
            {
                _groups.Add(found[i]);
            }

            // Number duplicate labels by location, not by congestion ranking. Keep the displayed
            // list worst-first, but changing counts must not exchange two streets' suffixes.
            var byLocation = new List<TrafficJamGroup>(_groups);
            byLocation.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
            var used = new HashSet<string>();
            foreach (var group in byLocation)
            {
                group.Name = Unique(group.Name, used);
                _byId[group.Id] = group;
            }
        }

        /// <summary>
        /// Where the camera goes: the queued vehicle nearest the queue's middle. The plain average
        /// of a queue that bends round a corner lies off the road, on whatever is inside the bend.
        /// </summary>
        private static float3 Centre(List<float3> positions, List<int> members)
        {
            float3 mean = float3.zero;
            foreach (int index in members)
            {
                mean += positions[index];
            }

            mean /= members.Count;

            float3 best = positions[members[0]];
            float bestDistance = float.MaxValue;
            foreach (int index in members)
            {
                float distance = math.lengthsq(positions[index] - mean);
                if (distance < bestDistance)
                {
                    best = positions[index];
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static IEnumerable<long> NeighbourKeys(long key)
        {
            int x = (int)(key >> 32);
            int z = (int)(key & 0xFFFFFFFFL);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    yield return Pack(x + dx, z + dz);
                }
            }
        }

        private static long CellKey(float3 position)
        {
            return Pack(
                (int)math.floor(position.x / CellSize),
                (int)math.floor(position.z / CellSize));
        }

        private static long Pack(int x, int z)
        {
            return ((long)x << 32) | (uint)z;
        }

        /// <summary>
        /// Whether a position is on the map at all.
        ///
        /// Width and depth only: the bounds' vertical extent describes the terrain, and a vehicle
        /// on an elevated road sits above it. Testing height too would quietly drop every jam on
        /// a flyover, which is where a good few of them are.
        /// </summary>
        private static bool OnMap(float3 position, UnityEngine.Bounds map)
        {
            return position.x >= map.min.x && position.x <= map.max.x
                && position.z >= map.min.z && position.z <= map.max.z;
        }

        /// <summary>
        /// The road a car is standing on, or Entity.Null if it is not standing on one.
        ///
        /// A car knows its lane, and a lane belongs to whatever it was cut from by way of
        /// Game.Common.Owner: a road edge out in the city, or a building, for the lanes inside a
        /// car park. That distinction is the whole point of asking. Cars inside a building's
        /// parking are stationary and packed closer than any queue on any street, so they took
        /// every round of the search and sent the camera to a garage - a place with no visible
        /// traffic, and the one place where knowing about it changes nothing, since a lot inside
        /// a building is not a road anyone can widen.
        ///
        /// The test is for a net edge rather than for a named street, deliberately. A slip road
        /// or a piece of an interchange often belongs to no named street at all, and those carry
        /// some of the worst queues in a city; asking for a name would have thrown them out with
        /// the car parks.
        ///
        /// Bounded rather than a while loop, for the reason BuildingLocator gives: an unexpected
        /// cycle in the ownership chain would hang the UI thread.
        /// </summary>
        private static Entity RoadOf(Entity vehicle, EntityManager entities,
            Dictionary<Entity, Entity> cache)
        {
            if (!entities.HasComponent<Game.Vehicles.CarCurrentLane>(vehicle))
            {
                return Entity.Null;
            }

            Entity lane = entities.GetComponentData<Game.Vehicles.CarCurrentLane>(vehicle).m_Lane;
            if (lane == Entity.Null)
            {
                return Entity.Null;
            }

            Entity known;
            if (cache.TryGetValue(lane, out known))
            {
                return known;
            }

            Entity found = Entity.Null;
            Entity current = lane;

            for (int depth = 0; depth < 4 && current != Entity.Null; depth++)
            {
                if (entities.HasComponent<Game.Buildings.Building>(current))
                {
                    break;
                }

                if (entities.HasComponent<Game.Net.Edge>(current))
                {
                    found = current;
                    break;
                }

                if (!entities.HasComponent<Game.Common.Owner>(current))
                {
                    break;
                }

                Entity owner = entities.GetComponentData<Game.Common.Owner>(current).m_Owner;
                if (owner == current)
                {
                    break;
                }

                current = owner;
            }

            cache[lane] = found;
            return found;
        }

        /// <summary>
        /// The street a road belongs to, or Entity.Null for a road that belongs to none.
        /// </summary>
        private static Entity StreetOf(Entity road, EntityManager entities)
        {
            return entities.HasComponent<Game.Net.Aggregated>(road)
                ? entities.GetComponentData<Game.Net.Aggregated>(road).m_Aggregate
                : Entity.Null;
        }

        /// <summary>
        /// What to call a jam: the street the road it sits on belongs to.
        ///
        /// An edge belongs to an aggregate, which is the thing the game itself calls a street and
        /// the only link in the chain carrying a name a player would recognise.
        ///
        /// Naming the first entity up the chain that answered to a name gave rows called "Car
        /// Drive Lane 3" and "Highway Drive Lane 4" - lane prefabs have names too, they are simply
        /// the wrong ones, and being non-empty they satisfied a check that was only ever asking
        /// whether something was there.
        /// </summary>
        private static string StreetName(Entity road, EntityManager entities, NameSystem names)
        {
            const string Fallback = "Traffic jam";

            if (road != Entity.Null && entities.HasComponent<Game.Net.Aggregated>(road))
            {
                Entity street = entities.GetComponentData<Game.Net.Aggregated>(road).m_Aggregate;
                string label = SafeName(names, street);

                if (!string.IsNullOrEmpty(label))
                {
                    return label;
                }
            }

            // Roads belonging to no named street - a lone slip road, a piece of interchange - keep
            // the generic name. Better an honest "Traffic jam" than the name of a lane.
            return Fallback;
        }

        /// <summary>
        /// Keeps the labels distinguishable. Clicks use the location ID, never this label.
        /// </summary>
        private static string Unique(string name, HashSet<string> used)
        {
            if (used.Add(name))
            {
                return name;
            }

            for (int n = 2; n < 100; n++)
            {
                string candidate = name + " (" + n + ")";
                if (used.Add(candidate))
                {
                    return candidate;
                }
            }

            return name;
        }

        /// <summary>
        /// Moves the camera to a jam. Not a cycling tour like NotificationBreakdown's: a jam is
        /// one place, and the row already names it.
        /// </summary>
        public bool Jump(string id, CameraUpdateSystem camera)
        {
            TrafficJamGroup group;
            // If this location has fallen out of the list, ignore an old click rather than
            // redirecting it to whichever queue has inherited its former street label.
            if (string.IsNullOrEmpty(id) || !_byId.TryGetValue(id, out group))
            {
                return false;
            }

            if (camera == null || camera.activeCameraController == null)
            {
                return false;
            }

            var target = group.Position;
            camera.activeCameraController.pivot = new UnityEngine.Vector3(target.x, target.y, target.z);
            return true;
        }

        private static string SafeName(NameSystem names, Entity entity)
        {
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
