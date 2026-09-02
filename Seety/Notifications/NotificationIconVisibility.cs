using System;
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
        private readonly EntityQuery _hiddenIcons;

        public NotificationIconVisibility(EntityManager entities, EntityQuery prefabs,
            EntityQuery visibleIcons, EntityQuery hiddenIcons)
        {
            _entities = entities;
            _query = prefabs;
            _visibleIcons = visibleIcons;
            _hiddenIcons = hiddenIcons;
        }

        /// <summary>
        /// Icons are created constantly, so while hidden the new ones have to be caught too.
        /// Both calls are batched over a whole query, which is far cheaper than touching entities
        /// one at a time, and both are no-ops when the query is empty.
        /// </summary>
        public void KeepUp()
        {
            if (!_hidden)
            {
                return;
            }

            try
            {
                if (!_visibleIcons.IsEmptyIgnoreFilter)
                {
                    _entities.AddComponent<Game.Tools.Hidden>(_visibleIcons);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn("Could not hide new notification icons: " + e.Message);
            }
        }

        public bool Hidden
        {
            get { return _hidden; }
        }

        public void Set(bool hidden)
        {
            if (_hidden == hidden)
            {
                return;
            }

            Apply(hidden);
            _hidden = hidden;
        }

        /// <summary>Puts the icons back. Called when the mod unloads.</summary>
        public void Restore()
        {
            if (_hidden)
            {
                Apply(false);
                _hidden = false;
            }
        }

        private void Apply(bool hidden)
        {
            try
            {
                var prefabCount = 0;
                using (var prefabs = _query.ToEntityArray(Allocator.Temp))
                {
                    prefabCount = prefabs.Length;
                    for (var i = 0; i < prefabs.Length; i++)
                    {
                        _entities.SetComponentEnabled<NotificationIconDisplayData>(prefabs[i], !hidden);
                    }
                }

                if (hidden)
                {
                    if (!_visibleIcons.IsEmptyIgnoreFilter)
                    {
                        _entities.AddComponent<Game.Tools.Hidden>(_visibleIcons);
                    }
                }
                else if (!_hiddenIcons.IsEmptyIgnoreFilter)
                {
                    _entities.RemoveComponent<Game.Tools.Hidden>(_hiddenIcons);
                }

                Mod.Log.Info((hidden ? "Hid " : "Restored ") + "the in-world notification icons ("
                    + prefabCount + " prefabs).");
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not change the notification icon visibility.");
            }
        }
    }
}
