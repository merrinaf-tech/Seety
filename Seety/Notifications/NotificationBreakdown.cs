using System.Collections.Generic;
using Game.Notifications;
using Game.Prefabs;
using Game.Rendering;
using Seety.Vitals;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Seety.Notifications
{
    /// <summary>One kind of problem the city is currently reporting.</summary>
    public sealed class NotificationGroup
    {
        /// <summary>The notification prefab's name, e.g. "BuildingAbandoned". Stable, never localised.</summary>
        public string Id;

        /// <summary>Vanilla icon for this notification type, relative to the GameUI root.</summary>
        public string Icon;

        public int Count;

        public VitalLevel Level;

        /// <summary>Where each one is, so clicking can take the player there.</summary>
        public readonly List<float3> Locations = new List<float3>();

        /// <summary>Which one the next click jumps to; cycles so repeat clicks tour them all.</summary>
        public int Cursor;
    }

    /// <summary>
    /// Splits the city's active notifications by type.
    ///
    /// This is the grouped view the Problems row expands into: a single total answers "is
    /// something wrong", but not "what". Notifications are entities carrying
    /// <see cref="Icon"/> plus a <see cref="PrefabRef"/> pointing at their
    /// NotificationIconPrefab, so the prefab name is both the grouping key and the icon name.
    ///
    /// Anything below IconPriority.Problem is skipped, exactly as the Problems count does - the
    /// two must agree, or the row and the list it opens would contradict each other.
    /// </summary>
    public sealed class NotificationBreakdown
    {
        private readonly List<NotificationGroup> _groups = new List<NotificationGroup>();
        private readonly Dictionary<string, NotificationGroup> _byId = new Dictionary<string, NotificationGroup>();

        /// <summary>
        /// Notification types whose prefab name does not match any icon file.
        ///
        /// Most do: strip the spaces from "Track Not Connected" and TrackNotConnected.svg is
        /// there. These are the ones where the game names the prefab one thing and the artwork
        /// another, so without the map several unrelated problems fell back to the same generic
        /// marker and the list looked like it had no icons at all.
        /// </summary>
        private static readonly Dictionary<string, string> IconAliases = new Dictionary<string, string>
        {
            { "ElectricityNotification", "NotEnoughElectricity" },
            { "WaterNotification", "NoRunningWater" },
            { "SewageNotification", "Sewage" },
            { "MissingEducatedWorkers", "NoEducatedWorkers" },
            { "MissingUneducatedWorkers", "NoWorkers" },
            { "HearseNotification", "HearseServiceNeeded" },
            { "TrafficBottleneckNotification", "TrafficBottleneck" },
            { "NoRoadAccess", "RoadNotConnected" },
            { "GarbageNotification", "TooMuchGarbage" },
            { "TelecomNotification", "NoTelecomCoverage" }
        };

        /// <summary>Prefab entity -> name. Resolving a name is not free; the set is small and stable.</summary>
        private readonly Dictionary<Entity, string> _names = new Dictionary<Entity, string>();

        public IReadOnlyList<NotificationGroup> Groups
        {
            get { return _groups; }
        }

        public void Refresh(EntityQuery query, PrefabSystem prefabs)
        {
            // Cursors are what make repeat clicks tour a group rather than sit on one building,
            // so they are carried across a refresh instead of being rebuilt with the list.
            var cursors = new Dictionary<string, int>();
            foreach (var group in _groups)
            {
                cursors[group.Id] = group.Cursor;
            }

            _groups.Clear();
            _byId.Clear();

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using (var icons = query.ToComponentDataArray<Icon>(Allocator.Temp))
            using (var refs = query.ToComponentDataArray<PrefabRef>(Allocator.Temp))
            {
                // Both arrays come from the same query, so index i is the same entity in each.
                var count = math.min(icons.Length, refs.Length);

                for (var i = 0; i < count; i++)
                {
                    var priority = icons[i].m_Priority;
                    if (priority < IconPriority.Problem)
                    {
                        continue;
                    }

                    var name = ResolveName(refs[i].m_Prefab, prefabs);
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    NotificationGroup group;
                    if (!_byId.TryGetValue(name, out group))
                    {
                        group = new NotificationGroup
                        {
                            Id = name,
                            Icon = IconFor(name),
                            Level = VitalLevel.Warning
                        };

                        int cursor;
                        if (cursors.TryGetValue(name, out cursor))
                        {
                            group.Cursor = cursor;
                        }

                        _byId.Add(name, group);
                        _groups.Add(group);
                    }

                    group.Count++;
                    group.Locations.Add(icons[i].m_Location);

                    if (priority >= IconPriority.MajorProblem)
                    {
                        group.Level = VitalLevel.Critical;
                    }
                }
            }

            // Largest first, full stop. Sorting by severity first read strangely in game: two
            // hearse notifications sat above twenty-four disconnected tracks purely because the
            // game grades them differently. What the player scans for is the big number; severity
            // still shows in the row's colour.
            _groups.Sort(delegate(NotificationGroup a, NotificationGroup b)
            {
                return b.Count.CompareTo(a.Count);
            });
        }

        /// <summary>
        /// Moves the camera to the next notification of this type. Returns false when there is
        /// nothing to go to, so the caller can stay quiet rather than pretend something happened.
        /// </summary>
        public bool Jump(string id, CameraUpdateSystem camera)
        {
            NotificationGroup group;
            if (!_byId.TryGetValue(id, out group) || group.Locations.Count == 0)
            {
                return false;
            }

            if (camera == null || camera.activeCameraController == null)
            {
                return false;
            }

            var index = group.Cursor % group.Locations.Count;
            group.Cursor = (index + 1) % group.Locations.Count;

            var target = group.Locations[index];

            // Only the pivot moves. Zoom and angle stay as the player left them: yanking the
            // camera to a fixed framing every click would be disorienting.
            camera.activeCameraController.pivot = new UnityEngine.Vector3(target.x, target.y, target.z);

            return true;
        }

        /// <summary>
        /// The icon file for a notification type. Prefab names carry spaces that filenames do not,
        /// and a handful are named differently again - see <see cref="IconAliases"/>.
        /// </summary>
        private static string IconFor(string name)
        {
            var file = name.Replace(" ", string.Empty);

            string alias;
            if (IconAliases.TryGetValue(file, out alias))
            {
                file = alias;
            }

            return "Media/Game/Notifications/" + file + ".svg";
        }

        private string ResolveName(Entity prefab, PrefabSystem prefabs)
        {
            if (prefab == Entity.Null || prefabs == null)
            {
                return null;
            }

            string name;
            if (_names.TryGetValue(prefab, out name))
            {
                return name;
            }

            try
            {
                name = prefabs.GetPrefabName(prefab);
            }
            catch
            {
                name = null;
            }

            _names[prefab] = name;
            return name;
        }
    }
}
