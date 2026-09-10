using System;
using Game.Buildings;
using Game.Objects;
using Game.Rendering;
using Game.UI;
using Unity.Entities;
using Unity.Mathematics;

namespace Seety.Vitals
{
    /// <summary>
    /// The three questions every "list the buildings, click one, fly there" panel has to answer:
    /// where is it, is it actually running, and what is it called.
    ///
    /// Extracted rather than copied when the cemetery list joined the school list. All three were
    /// learned the hard way and are worth having in one place: a school with no Transform sent the
    /// camera to the world origin, a shut-down building promised places that did not exist, and a
    /// name resolved off the wrong entity came back as a raw lookup key.
    /// </summary>
    internal static class BuildingLocator
    {
        /// <summary>
        /// Where to send the camera for this building: its own Transform, or the nearest thing it
        /// belongs to that has one.
        ///
        /// A service inside a signature building has no Transform of its own - it is a
        /// sub-building or an upgrade, placed relative to its parent - so it is followed up the
        /// Game.Common.Owner chain instead. Bounded rather than a while loop on purpose: an
        /// unexpected cycle in that chain would hang the UI thread, and nothing here is worth that
        /// risk. Four is well past what the game actually nests.
        /// </summary>
        public static bool PositionOf(EntityManager entities, Entity entity, out float3 position)
        {
            var current = entity;

            for (var depth = 0; depth < 4; depth++)
            {
                if (entities.HasComponent<Transform>(current))
                {
                    position = entities.GetComponentData<Transform>(current).m_Position;
                    return true;
                }

                if (!entities.HasComponent<Game.Common.Owner>(current))
                {
                    break;
                }

                var owner = entities.GetComponentData<Game.Common.Owner>(current).m_Owner;
                if (owner == Entity.Null || owner == current)
                {
                    break;
                }

                current = owner;
            }

            position = float3.zero;
            return false;
        }

        /// <summary>
        /// Whether a building is producing nothing at all.
        ///
        /// Efficiency is a buffer of factors that multiply together; a single zero shuts the
        /// building down. BuildingUtils.GetEfficiency does exactly this, but its DynamicBuffer
        /// overload will not compile against net48 - it resolves through Span - so the same
        /// multiplication is done here.
        /// </summary>
        public static bool IsShutDown(EntityManager entities, Entity building)
        {
            if (!entities.HasBuffer<Efficiency>(building))
            {
                return false;
            }

            var factors = entities.GetBuffer<Efficiency>(building, true);
            for (var i = 0; i < factors.Length; i++)
            {
                if (factors[i].m_Efficiency <= 0f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The building's display name, or <paramref name="fallback"/> if it has none.</summary>
        public static string SafeName(NameSystem names, Entity entity, string fallback)
        {
            try
            {
                var label = names.GetRenderedLabelName(entity);
                return string.IsNullOrEmpty(label) ? fallback : label;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// Moves the camera to a point. Only the pivot moves: zoom and angle stay as the player
        /// left them, because yanking the camera to a fixed framing every click is disorienting.
        /// </summary>
        public static bool Jump(float3 target, CameraUpdateSystem camera)
        {
            if (camera == null || camera.activeCameraController == null)
            {
                return false;
            }

            camera.activeCameraController.pivot = new UnityEngine.Vector3(target.x, target.y, target.z);
            return true;
        }
    }
}
