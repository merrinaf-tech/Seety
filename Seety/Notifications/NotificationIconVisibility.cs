using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace Seety.Notifications
{
    /// <summary>
    /// Hides or shows the notification icons floating over the city.
    ///
    /// This looked impossible for a while. Disabling `NotificationIconRenderSystem` does nothing -
    /// its `OnUpdate` only raises a dirty flag and the drawing happens in a render-pipeline
    /// callback - and the only lever visible there, `RenderingSystem.hideOverlay`, hides every
    /// overlay in the game rather than just these.
    ///
    /// The actual switch is upstream. `NotificationIconDisplayData` is an **IEnableableComponent**
    /// on each notification prefab, and `NotificationIconBufferSystem` checks
    /// `IsComponentEnabled(prefab)` before adding an icon to the buffer it hands the renderer.
    /// Disable it and those icons are never built, so nothing draws them.
    ///
    /// That alone was not enough, and the reason took a second look at the buffer system. It has
    /// three paths, and only two of them check `IsComponentEnabled`. The third - the one for
    /// **clustered** icons - reads the display data straight out of the lookup with no check at
    /// all. In any city big enough to matter almost every icon is clustered, so disabling the
    /// prefab silently did nothing on screen while the log happily reported success.
    ///
    /// The lever the cluster path does respect is `Game.Tools.Hidden` on the icon entity itself:
    /// an icon carrying it is routed to the hidden-position branch and never reaches the draw
    /// list. So both are applied - the prefab flag stops new unclustered icons, and `Hidden`
    /// covers everything already on screen.
    ///
    /// Only problems. The same icon system draws two other kinds - IconClusterLayer.Marker, the
    /// markers such as a line's stops while it is being edited, and IconClusterLayer.Transaction,
    /// the money figures - and hiding every icon took those with it: on 2026-10-02 a line being
    /// edited showed no stations while the problem icons were hidden. So an icon is hidden only if
    /// its layer is Default, and a prefab is disabled only if it was seen on such an icon and never
    /// on a marker or transaction. A prefab used by both is left enabled; its problem icons are
    /// still caught one by one on the next refresh.
    ///
    /// This writes to prefab entities and to icon entities, not to the city: no simulation state
    /// changes and nothing reaches a savegame. The state is restored on unload so a session that
    /// ends hidden does not leave the next one wondering where the icons went.
    /// </summary>
    public sealed class NotificationIconVisibility
    {
        private readonly EntityManager _entities;

        private bool _hidden;

        private readonly EntityQuery _visibleIcons;
        // Entity includes its version: a destroyed icon's recycled index is not ours to restore.
        private readonly HashSet<Entity> _hiddenByUs = new HashSet<Entity>();
        private readonly HashSet<Entity> _disabledByUs = new HashSet<Entity>();
        // Prefabs seen on markers or transactions. Never disabled, and re-enabled if they were.
        private readonly HashSet<Entity> _keptPrefabs = new HashSet<Entity>();

        public NotificationIconVisibility(EntityManager entities, EntityQuery visibleIcons)
        {
            _entities = entities;
            _visibleIcons = visibleIcons;
        }

        /// <summary>
        /// Icons are created constantly, so while hidden the new ones have to be caught too.
        /// The visible snapshot is recorded before the batched change. Icons hidden before we
        /// touched them are never in that snapshot and must remain hidden when we restore it.
        /// </summary>
        public void KeepUp()
        {
            if (!_hidden && _hiddenByUs.Count == 0 && _disabledByUs.Count == 0)
            {
                return;
            }

            try
            {
                Apply(_hidden);
            }
            catch (Exception e)
            {
                Mod.Log.Warn("Could not maintain notification icon visibility: " + e.Message);
            }
        }

        public bool Hidden
        {
            get { return _hidden; }
        }

        public void Set(bool hidden)
        {
            // This is the requested state. If a structural change fails, KeepUp retries the
            // outstanding work, including restoration, without forgetting the owned entities.
            _hidden = hidden;
            KeepUp();
        }

        /// <summary>Puts the icons back. Called when the mod unloads.</summary>
        public void Restore()
        {
            Set(false);
        }

        private void Apply(bool hidden)
        {
            if (hidden)
            {
                // Prune disappeared entities during long sessions instead of retaining every
                // notification ever seen until the player turns the icons back on.
                _hiddenByUs.RemoveWhere(entity => !_entities.Exists(entity));
                _disabledByUs.RemoveWhere(entity => !_entities.Exists(entity));
                using (var icons = _visibleIcons.ToEntityArray(Allocator.Temp))
                {
                    // Markers and transactions first, so their prefabs are known before any
                    // problem prefab is disabled.
                    for (var i = 0; i < icons.Length; i++)
                    {
                        if (!IsProblem(icons[i]))
                        {
                            var prefab = PrefabOf(icons[i]);
                            if (prefab != Entity.Null && _keptPrefabs.Add(prefab)) Enable(prefab);
                        }
                    }

                    var problems = new NativeList<Entity>(icons.Length, Allocator.Temp);
                    for (var i = 0; i < icons.Length; i++)
                    {
                        if (!IsProblem(icons[i])) continue;
                        problems.Add(icons[i]);
                        _hiddenByUs.Add(icons[i]);

                        var prefab = PrefabOf(icons[i]);
                        if (prefab != Entity.Null && !_keptPrefabs.Contains(prefab)
                            && _entities.HasComponent<NotificationIconDisplayData>(prefab)
                            && _entities.IsComponentEnabled<NotificationIconDisplayData>(prefab))
                        {
                            _disabledByUs.Add(prefab);
                            _entities.SetComponentEnabled<NotificationIconDisplayData>(prefab, false);
                        }
                    }

                    if (problems.Length > 0) _entities.AddComponent<Game.Tools.Hidden>(problems.AsArray());
                    problems.Dispose();
                }
                return;
            }

            // Remove each entry only after restoring it, so a partial failure can be retried.
            _keptPrefabs.Clear();
            foreach (var prefab in new List<Entity>(_disabledByUs))
            {
                if (_entities.Exists(prefab) && _entities.HasComponent<NotificationIconDisplayData>(prefab))
                {
                    _entities.SetComponentEnabled<NotificationIconDisplayData>(prefab, true);
                }
                _disabledByUs.Remove(prefab);
            }
            foreach (var icon in new List<Entity>(_hiddenByUs))
            {
                if (_entities.Exists(icon) && _entities.HasComponent<Game.Tools.Hidden>(icon))
                {
                    _entities.RemoveComponent<Game.Tools.Hidden>(icon);
                }
                _hiddenByUs.Remove(icon);
            }

            // Back on screen only once the game looks at them again. IconClusterSystem rebuilds its
            // clusters only for icons marked Updated or Deleted, and icons that changed while their
            // prefab was disabled fell out of the clusters - so re-enabling the prefabs and lifting
            // Hidden left the problem icons invisible. Marking them Updated is what the game itself
            // does when an icon changes.
            using (var icons = _visibleIcons.ToEntityArray(Allocator.Temp))
            {
                var problems = new NativeList<Entity>(icons.Length, Allocator.Temp);
                for (var i = 0; i < icons.Length; i++)
                {
                    if (IsProblem(icons[i])) problems.Add(icons[i]);
                }

                if (problems.Length > 0) _entities.AddComponent<Game.Common.Updated>(problems.AsArray());
                problems.Dispose();
            }
        }

        /// <summary>A problem notification, as opposed to a marker or a money figure.</summary>
        private bool IsProblem(Entity icon)
        {
            return _entities.HasComponent<Game.Notifications.Icon>(icon)
                && _entities.GetComponentData<Game.Notifications.Icon>(icon).m_ClusterLayer
                    == Game.Notifications.IconClusterLayer.Default;
        }

        private Entity PrefabOf(Entity icon)
        {
            return _entities.HasComponent<PrefabRef>(icon)
                ? _entities.GetComponentData<PrefabRef>(icon).m_Prefab
                : Entity.Null;
        }

        /// <summary>Re-enables a prefab this class disabled before it was seen on a marker.</summary>
        private void Enable(Entity prefab)
        {
            if (!_disabledByUs.Remove(prefab)) return;
            if (_entities.Exists(prefab) && _entities.HasComponent<NotificationIconDisplayData>(prefab))
            {
                _entities.SetComponentEnabled<NotificationIconDisplayData>(prefab, true);
            }
        }
    }
}
