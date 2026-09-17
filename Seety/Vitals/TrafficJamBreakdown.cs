using System.Collections.Generic;
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
        /// <summary>The street it is on, made unique if two jams share a name. Shown as the row.</summary>
        public string Name;

        /// <summary>Stuck vehicles in it.</summary>
        public int Count;

        /// <summary>Where the camera goes.</summary>
        public float3 Position;
    }

    /// <summary>
    /// The city's traffic jams, worst first.
    ///
    /// A vehicle carries <see cref="Game.Vehicles.Blocker"/> the moment something else has already
    /// decided it is not moving - the same component Game.Simulation.StuckMovingObjectSystem acts
    /// on. Its presence already means "stuck"; there is no threshold to invent.
    ///
    /// But one stuck vehicle is not a jam, and that is the whole difficulty here. Blocker carries
    /// no timer and only two types, None and Temporary, so it fires for a car giving way at a
    /// junction exactly as it does for a car in a half-mile queue. Across a large city hundreds of
    /// vehicles hold that component at any instant, almost all of them for a second or two.
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
        /// The grid the city is divided into, in metres, before crowds are looked for. Small
        /// enough that two unrelated queues either side of a junction stay distinct.
        /// </summary>
        private const float CellSize = 32f;

        /// <summary>
        /// Fewer stuck vehicles than this in one place is traffic behaving normally, not a jam.
        /// Without a floor the list fills up with pairs of cars waiting at a give-way, which is
        /// what every quiet city is full of.
        /// </summary>
        private const int MinJamSize = 5;

        private readonly List<TrafficJamGroup> _groups = new List<TrafficJamGroup>();
        private readonly Dictionary<string, TrafficJamGroup> _byName = new Dictionary<string, TrafficJamGroup>();

        public IReadOnlyList<TrafficJamGroup> Groups
        {
            get { return _groups; }
        }

        public void Refresh(EntityQuery query, EntityManager entities,
            NameSystem names, Game.Simulation.TerrainSystem terrain)
        {
            _groups.Clear();
            _byName.Clear();

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            UnityEngine.Bounds map = terrain != null
                ? terrain.GetTerrainBounds()
                : new UnityEngine.Bounds();
            bool bounded = map.size.x > 0f && map.size.z > 0f;

            var positions = new List<float3>();
            var vehicleEntities = new List<Entity>();

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicles.Length; i++)
                {
                    var vehicle = vehicles[i];
                    if (!entities.HasComponent<Game.Objects.Transform>(vehicle))
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

                    positions.Add(position);
                    vehicleEntities.Add(vehicle);
                }
            }

            FindJams(positions, vehicleEntities, entities, names);
        }

        /// <summary>
        /// Picks the crowds out of a scatter of stopped vehicles.
        ///
        /// Repeatedly takes the busiest cell together with its eight neighbours, calls that a jam,
        /// and removes those cells from consideration before looking for the next one. Taking the
        /// neighbours in means a queue straddling a cell boundary counts once rather than twice;
        /// removing them afterwards means the second row is a different jam rather than the same
        /// one shifted by thirty metres.
        ///
        /// Deliberately not a flood fill over adjacent cells. That is the obvious way to grow a
        /// jam to its true extent, and in a busy city centre it grows through every junction on
        /// every connecting street until one "jam" covers downtown and reports eight hundred
        /// vehicles - a number that names nowhere. A jam bounded to roughly ninety metres is a
        /// junction and its approaches, which is a place you can be sent to.
        /// </summary>
        private void FindJams(List<float3> positions, List<Entity> vehicles,
            EntityManager entities, NameSystem names)
        {
            var cells = new Dictionary<long, List<int>>();

            for (var i = 0; i < positions.Count; i++)
            {
                long key = CellKey(positions[i]);

                List<int> bucket;
                if (!cells.TryGetValue(key, out bucket))
                {
                    bucket = new List<int>();
                    cells.Add(key, bucket);
                }

                bucket.Add(i);
            }

            var claimed = new HashSet<long>();
            var used = new HashSet<string>();

            while (_groups.Count < MaxGroups)
            {
                // Scored by count alone, and only the winner's members are gathered. Building a
                // list for every candidate cell instead allocated one per cell per round - tens
                // of thousands of short-lived lists on the UI thread every time this refreshed,
                // for nine of every ten of which nothing was ever read.
                long bestKey = 0;
                int bestCount = 0;

                foreach (var cell in cells)
                {
                    if (claimed.Contains(cell.Key))
                    {
                        continue;
                    }

                    int count = NeighbourCount(cells, claimed, cell.Key);
                    if (count > bestCount)
                    {
                        bestKey = cell.Key;
                        bestCount = count;
                    }
                }

                if (bestCount < MinJamSize)
                {
                    break;
                }

                List<int> bestMembers = Neighbourhood(cells, claimed, bestKey);

                foreach (long key in NeighbourKeys(bestKey))
                {
                    claimed.Add(key);
                }

                // The centre of what was found, rather than the centre of the cell that won: a jam
                // sitting in the corner of its cell should put the camera on the cars, not on the
                // empty ground beside them.
                float3 centre = float3.zero;
                foreach (int index in bestMembers)
                {
                    centre += positions[index];
                }

                centre /= bestMembers.Count;

                _groups.Add(new TrafficJamGroup
                {
                    Name = Unique(StreetName(vehicles[bestMembers[0]], entities, names), used),
                    Count = bestMembers.Count,
                    Position = centre
                });
            }

            foreach (var group in _groups)
            {
                _byName[group.Name] = group;
            }
        }

        /// <summary>How many vehicles a cell and its eight neighbours hold, ignoring claimed cells.</summary>
        private static int NeighbourCount(Dictionary<long, List<int>> cells,
            HashSet<long> claimed, long key)
        {
            int total = 0;

            foreach (long neighbour in NeighbourKeys(key))
            {
                if (claimed.Contains(neighbour))
                {
                    continue;
                }

                List<int> bucket;
                if (cells.TryGetValue(neighbour, out bucket))
                {
                    total += bucket.Count;
                }
            }

            return total;
        }

        /// <summary>Every vehicle in a cell and its eight neighbours, skipping claimed cells.</summary>
        private static List<int> Neighbourhood(Dictionary<long, List<int>> cells,
            HashSet<long> claimed, long key)
        {
            var members = new List<int>();

            foreach (long neighbour in NeighbourKeys(key))
            {
                if (claimed.Contains(neighbour))
                {
                    continue;
                }

                List<int> bucket;
                if (cells.TryGetValue(neighbour, out bucket))
                {
                    members.AddRange(bucket);
                }
            }

            return members;
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
        /// What to call a jam: the street the vehicle representing it is standing on.
        ///
        /// A car knows its lane, and a lane belongs to the road it was cut from, so the name comes
        /// from walking up Game.Common.Owner until something answers to a name - the same climb
        /// BuildingLocator makes for a service inside a signature building, and bounded for the
        /// same reason.
        /// </summary>
        private static string StreetName(Entity vehicle, EntityManager entities, NameSystem names)
        {
            const string Fallback = "Traffic jam";

            if (!entities.HasComponent<Game.Vehicles.CarCurrentLane>(vehicle))
            {
                return Fallback;
            }

            Entity current = entities.GetComponentData<Game.Vehicles.CarCurrentLane>(vehicle).m_Lane;

            for (int depth = 0; depth < 4 && current != Entity.Null; depth++)
            {
                string label = SafeName(names, current);
                if (!string.IsNullOrEmpty(label))
                {
                    return label;
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

            return Fallback;
        }

        /// <summary>
        /// Keeps two jams on the same street from sharing a row.
        ///
        /// The name is the row's identity as well as its label - it is the key the click comes
        /// back with - so a long street with two separate queues on it would otherwise send the
        /// camera to whichever of them was recorded last, whichever row was clicked.
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
        public bool Jump(string name, CameraUpdateSystem camera)
        {
            TrafficJamGroup group;
            if (string.IsNullOrEmpty(name) || !_byName.TryGetValue(name, out group))
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
