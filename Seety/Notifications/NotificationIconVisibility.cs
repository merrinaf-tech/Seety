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
    /// This writes to prefab entities and to icon entities, not to the city: no simulation state
    /// changes and nothing reaches a savegame. The state is restored on unload so a session that
    /// ends hidden does not leave the next one wondering where the icons went.
    /// </summary>
    public sealed class NotificationIconVisibility
    {
        private readonly EntityManager _entities;
        private readonly EntityQuery _query;

        private bool _hidden;

        private readonly EntityQuery _visibleIcons;
        // Entity includes its version: a destroyed icon's recycled index is not ours to restore.
        private readonly HashSet<Entity> _hiddenByUs = new HashSet<Entity>();
        private readonly HashSet<Entity> _disabledByUs = new HashSet<Entity>();

        public NotificationIconVisibility(EntityManager entities, EntityQuery prefabs,
            EntityQuery visibleIcons)
        {
            _entities = entities;
            _query = prefabs;
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
                using (var prefabs = _query.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < prefabs.Length; i++)
                    {
                        if (_entities.IsComponentEnabled<NotificationIconDisplayData>(prefabs[i]))
                        {
                            _disabledByUs.Add(prefabs[i]);
                            _entities.SetComponentEnabled<NotificationIconDisplayData>(prefabs[i], false);
                        }
                    }
                }

                using (var icons = _visibleIcons.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < icons.Length; i++)
                    {
                        _hiddenByUs.Add(icons[i]);
                    }
                    if (icons.Length > 0) _entities.AddComponent<Game.Tools.Hidden>(icons);
                }
                return;
            }

            // Remove each entry only after restoring it, so a partial failure can be retried.
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
        }
    }
}
