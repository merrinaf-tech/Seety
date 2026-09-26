using System.Collections.Generic;
using Game.Buildings;
using Game.Common;
using Game.Notifications;
using Game.Prefabs;
using Game.Tools;
using Game.UI.Localization;
using Game.UI.Tooltip;
using Seety.Tooltips;
using Unity.Entities;
using UnityEngine.Scripting;

namespace Seety.Systems
{
    /// <summary>
    /// "Why": under the cursor, what is holding a building back.
    ///
    /// Seety's job is to say what is wrong with the city and take the player there. This does the
    /// same for one building: up to three lines naming what the game itself sees as the problem,
    /// and nothing at all when there is none. It deliberately does not summarise the building -
    /// occupancy, workers, value - which the building's panel and other tooltip mods already do.
    ///
    /// Two sources, both the game's own:
    /// - the notification icons the game raised on the building, named with the game's own title
    ///   (Notifications.TITLE[...]), so the words match the icon floating over it;
    /// - efficiency factors below full, named as the building panel names them
    ///   (SelectedInfoPanel.EFFICIENCY_FACTORS[...]) with the percentage they cost.
    /// Both are localisation ids handed to the game, so every language the game has is covered.
    ///
    /// Only with the default tool: while a road or a zone is being drawn, a tooltip about the
    /// building under the brush is in the way. The lines are recomputed when the building under
    /// the cursor changes and about twice a second otherwise; reading two small buffers of one
    /// entity is all the work there is.
    /// </summary>
    public partial class BuildingReasonsTooltipSystem : TooltipSystemBase
    {
        /// <summary>Frames between refreshes while the cursor stays on the same building.</summary>
        private const int RefreshFrames = 30;

        private ToolSystem _tools;
        private DefaultToolSystem _defaultTool;
        private ToolRaycastSystem _raycast;
        private PrefabSystem _prefabs;

        private readonly StringTooltip[] _lines = new StringTooltip[ReasonRanking.MaxLines];
        private int _lineCount;

        private readonly List<ReasonCandidate> _notifications = new List<ReasonCandidate>();
        private readonly List<ReasonCandidate> _factors = new List<ReasonCandidate>();
        private readonly List<ReasonCandidate> _picked = new List<ReasonCandidate>();

        /// <summary>Notification prefab names, resolved once each: GetPrefabName allocates.</summary>
        private readonly Dictionary<Entity, string> _prefabNames = new Dictionary<Entity, string>();

        private Entity _lastBuilding;
        private int _framesSinceRefresh;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();

            _tools = World.GetOrCreateSystemManaged<ToolSystem>();
            _defaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            _raycast = World.GetOrCreateSystemManaged<ToolRaycastSystem>();
            _prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();

            for (var i = 0; i < _lines.Length; i++)
            {
                _lines[i] = new StringTooltip { path = "seety.reason" + i };
            }
        }

        [Preserve]
        protected override void OnUpdate()
        {
            if (Mod.Settings == null || !Mod.Settings.BuildingReasons
                || _tools.activeTool != _defaultTool)
            {
                _lastBuilding = Entity.Null;
                return;
            }

            RaycastResult hit;
            Entity building = _raycast.GetRaycastResult(out hit) ? ResolveBuilding(hit.m_Owner) : Entity.Null;
            if (building == Entity.Null)
            {
                _lastBuilding = Entity.Null;
                return;
            }

            if (building != _lastBuilding || ++_framesSinceRefresh >= RefreshFrames)
            {
                _lastBuilding = building;
                _framesSinceRefresh = 0;
                Refresh(building);
            }

            for (var i = 0; i < _lineCount; i++)
            {
                AddMouseTooltip(_lines[i]);
            }
        }

        private void Refresh(Entity building)
        {
            _notifications.Clear();
            _factors.Clear();

            if (EntityManager.HasBuffer<IconElement>(building))
            {
                var icons = EntityManager.GetBuffer<IconElement>(building, true);
                for (var i = 0; i < icons.Length; i++)
                {
                    var icon = icons[i].m_Icon;
                    if (!EntityManager.Exists(icon) || !EntityManager.HasComponent<Icon>(icon)
                        || !EntityManager.HasComponent<PrefabRef>(icon))
                    {
                        continue;
                    }

                    // The same floor the Problems count uses: below Problem is information.
                    var priority = EntityManager.GetComponentData<Icon>(icon).m_Priority;
                    if (priority < IconPriority.Problem)
                    {
                        continue;
                    }

                    var name = PrefabName(EntityManager.GetComponentData<PrefabRef>(icon).m_Prefab);
                    if (!string.IsNullOrEmpty(name))
                    {
                        _notifications.Add(new ReasonCandidate
                        {
                            Key = name,
                            IsNotification = true,
                            Severity = (float)priority,
                        });
                    }
                }
            }

            if (EntityManager.HasBuffer<Efficiency>(building))
            {
                var factors = EntityManager.GetBuffer<Efficiency>(building, true);
                for (var i = 0; i < factors.Length; i++)
                {
                    _factors.Add(new ReasonCandidate
                    {
                        Key = factors[i].m_Factor.ToString(),
                        Severity = factors[i].m_Efficiency,
                    });
                }
            }

            ReasonRanking.Pick(_notifications, _factors, _picked);

            _lineCount = _picked.Count;
            for (var i = 0; i < _lineCount; i++)
            {
                var reason = _picked[i];
                var line = _lines[i];

                if (reason.IsNotification)
                {
                    line.value = LocalizedString.IdWithFallback(
                        "Notifications.TITLE[" + reason.Key + "]", reason.Key);
                    line.icon = Seety.Notifications.NotificationBreakdown.IconFor(reason.Key);
                    line.color = reason.Severity >= (float)IconPriority.MajorProblem
                        ? TooltipColor.Error
                        : TooltipColor.Warning;
                }
                else
                {
                    line.value = LocalizedString.Id("Seety.TIP_FACTOR",
                        ("FACTOR", LocalizedString.IdWithFallback(
                            "SelectedInfoPanel.EFFICIENCY_FACTORS[" + reason.Key + "]", reason.Key)),
                        ("PERCENT", LocalizedString.Value(
                            ReasonRanking.LossPercent(reason.Severity).ToString(
                                System.Globalization.CultureInfo.InvariantCulture))));
                    line.icon = null;
                    line.color = TooltipColor.Warning;
                }
            }
        }

        private string PrefabName(Entity prefab)
        {
            if (prefab == Entity.Null)
            {
                return null;
            }

            string name;
            if (_prefabNames.TryGetValue(prefab, out name))
            {
                return name;
            }

            try
            {
                name = _prefabs.GetPrefabName(prefab);
            }
            catch
            {
                name = null;
            }

            _prefabNames[prefab] = name;
            return name;
        }

        /// <summary>
        /// Walks up from whatever the cursor hit to the building that owns it: a raycast often
        /// lands on a sign or a roof fitting, which is its own entity.
        ///
        /// Never through a vehicle or a creature. Their Owner is the depot or home they belong to,
        /// so climbing from a train reached its railway depot and showed the depot's garbage under
        /// a train. A cursor on something that moves is not a cursor on a building.
        /// </summary>
        private Entity ResolveBuilding(Entity hit)
        {
            for (var guard = 0; guard < 8 && hit != Entity.Null; guard++)
            {
                if (!EntityManager.Exists(hit) || EntityManager.HasComponent<Temp>(hit)
                    || EntityManager.HasComponent<Game.Vehicles.Vehicle>(hit)
                    || EntityManager.HasComponent<Game.Creatures.Creature>(hit))
                {
                    return Entity.Null;
                }

                if (EntityManager.HasComponent<Building>(hit) && !EntityManager.HasComponent<Deleted>(hit))
                {
                    return hit;
                }

                if (!EntityManager.HasComponent<Owner>(hit))
                {
                    return Entity.Null;
                }

                hit = EntityManager.GetComponentData<Owner>(hit).m_Owner;
            }

            return Entity.Null;
        }

        [Preserve]
        public BuildingReasonsTooltipSystem()
        {
        }
    }
}
