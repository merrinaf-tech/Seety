using System.Collections.Generic;
using Game.Prefabs;
using Game.Rendering;
using Game.UI;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Seety.Vitals
{
    /// <summary>One kind of vehicle currently stuck in traffic, and how many.</summary>
    public sealed class TrafficJamGroup
    {
        public string Name;
        public int Count;

        /// <summary>
        /// Where the actual pile-up of this kind is - see ClusterCenter. Not just wherever the
        /// first one happened to be found: entity order out of an EntityQuery is chunk order, not
        /// spatial order, so "first found" was landing on whichever stray blocked vehicle of that
        /// kind the query iterated to first - the middle of nowhere as often as the middle of the
        /// jam, and once on a vehicle waiting at an outside connection past the map edge.
        /// </summary>
        public float3 Position;
    }

    /// <summary>
    /// The city's stuck vehicles, grouped by kind, worst first.
    ///
    /// A vehicle carries <see cref="Blocker"/> the moment something else has already decided it is
    /// not moving - the same component Game.Simulation.StuckMovingObjectSystem itself acts on. Its
    /// presence on an entity already means "stuck"; there is no threshold to invent, unlike the
    /// company-priority ranking, which had to be built from raw numbers because no single flag said
    /// "this one is struggling".
    ///
    /// One icon for every row rather than one per vehicle kind. A notification's icon file can be
    /// found by name because the notification prefab and its artwork share that name by convention
    /// - vehicles have no equivalent convention available here, and guessing a path the way the
    /// education icons once did fails silently rather than with an error. The traffic row's own
    /// icon, already verified, stands in for all of them.
    /// </summary>
    public sealed class TrafficJamBreakdown
    {
        /// <summary>Only the worst handful are worth a row - see NotificationBreakdown.MaxProblemRows for the same call.</summary>
        private const int MaxGroups = 10;

        /// <summary>
        /// Positions within this of each other, in metres, count as the same pile-up when picking
        /// which one to jump to. Small enough that two unrelated queues on opposite sides of a
        /// junction do not merge, large enough that a jam a few car-lengths long still counts as
        /// one cluster rather than a dozen singletons.
        /// </summary>
        private const float ClusterCellSize = 32f;

        private readonly List<TrafficJamGroup> _groups = new List<TrafficJamGroup>();
        private readonly Dictionary<string, TrafficJamGroup> _byName = new Dictionary<string, TrafficJamGroup>();

        // Every position seen this refresh, kept only long enough to pick a cluster centre below -
        // Count on the finished group comes from here rather than being tallied one at a time,
        // since the representative position cannot be chosen until all of a kind are in.
        private readonly Dictionary<string, List<float3>> _positions = new Dictionary<string, List<float3>>();

        public IReadOnlyList<TrafficJamGroup> Groups
        {
            get { return _groups; }
        }

        public void Refresh(EntityQuery query, EntityManager entities, PrefabSystem prefabs,
            NameSystem names, Game.Simulation.TerrainSystem terrain)
        {
            _groups.Clear();
            _byName.Clear();
            _positions.Clear();

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            UnityEngine.Bounds map = terrain != null
                ? terrain.GetTerrainBounds()
                : new UnityEngine.Bounds();
            bool bounded = map.size.x > 0f && map.size.z > 0f;

            using (var vehicles = query.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < vehicles.Length; i++)
                {
                    Collect(vehicles[i], entities, prefabs, names, map, bounded);
                }
            }

            foreach (var entry in _positions)
            {
                var group = new TrafficJamGroup
                {
                    Name = entry.Key,
                    Count = entry.Value.Count,
                    Position = ClusterCenter(entry.Value)
                };

                _byName.Add(entry.Key, group);
                _groups.Add(group);
            }

            // Worst first: the top of the list is the jam doing the most damage right now.
            _groups.Sort(delegate(TrafficJamGroup a, TrafficJamGroup b)
            {
                return b.Count.CompareTo(a.Count);
            });

            if (_groups.Count > MaxGroups)
            {
                _groups.RemoveRange(MaxGroups, _groups.Count - MaxGroups);
            }
        }

        private void Collect(Entity vehicle, EntityManager entities, PrefabSystem prefabs,
            NameSystem names, UnityEngine.Bounds map, bool bounded)
        {
            if (!entities.HasComponent<PrefabRef>(vehicle) || !entities.HasComponent<Game.Objects.Transform>(vehicle))
            {
                return;
            }

            var prefab = entities.GetComponentData<PrefabRef>(vehicle).m_Prefab;
            var name = SafeName(names, prefab, prefabs);
            var position = entities.GetComponentData<Game.Objects.Transform>(vehicle).m_Position;

            if (bounded && !OnMap(position, map))
            {
                // Not traffic in this city. Vehicles queue at the outside connections in dense
                // stacks, and density is exactly what picks the place to fly to below, so those
                // stacks beat any real jam and sent the camera off the edge of the world. They
                // were inflating the counts for the same reason: a hundred taxis waiting to be
                // let in is not a hundred taxis stuck in your streets.
                return;
            }

            List<float3> positions;
            if (!_positions.TryGetValue(name, out positions))
            {
                positions = new List<float3>();
                _positions.Add(name, positions);
            }

            positions.Add(position);
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
        /// The position that has the most others of the same kind near it, on a flat grid of
        /// <see cref="ClusterCellSize"/>-metre cells. One real jam vastly outnumbers any lone
        /// straggler stuck for an unrelated reason, so the busiest cell is the jam - a single
        /// blocked car, wherever it is, only wins when there is no real cluster to beat it.
        /// </summary>
        private static float3 ClusterCenter(List<float3> positions)
        {
            if (positions.Count == 1)
            {
                return positions[0];
            }

            var counts = new Dictionary<(int, int), int>();
            var firstInCell = new Dictionary<(int, int), float3>();

            foreach (var position in positions)
            {
                var key = (
                    (int)math.floor(position.x / ClusterCellSize),
                    (int)math.floor(position.z / ClusterCellSize));

                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;

                if (!firstInCell.ContainsKey(key))
                {
                    firstInCell[key] = position;
                }
            }

            var bestKey = (0, 0);
            var bestCount = 0;
            foreach (var entry in counts)
            {
                if (entry.Value > bestCount)
                {
                    bestCount = entry.Value;
                    bestKey = entry.Key;
                }
            }

            return firstInCell[bestKey];
        }

        /// <summary>
        /// Moves the camera to the first vehicle found of this kind. Not a cycling tour like
        /// NotificationBreakdown's - a jam does not move the way separate buildings do, so one
        /// vehicle already puts the player at the jam.
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

        private static string SafeName(NameSystem names, Entity prefab, PrefabSystem prefabs)
        {
            try
            {
                var label = names.GetRenderedLabelName(prefab);
                if (!string.IsNullOrEmpty(label))
                {
                    return label;
                }
            }
            catch
            {
                // Falls through to the raw prefab name below.
            }

            try
            {
                return prefabs.GetPrefabName(prefab);
            }
            catch
            {
                return "Vehicle";
            }
        }
    }
}
