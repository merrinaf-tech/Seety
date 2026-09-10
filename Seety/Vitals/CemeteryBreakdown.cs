using System;
using System.Collections.Generic;
using Game.Buildings;
using Game.Prefabs;
using Game.Rendering;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Seety.Vitals
{
    /// <summary>One cemetery, with how full it is and where to find it.</summary>
    public sealed class CemeteryEntry
    {
        /// <summary>Which cemetery this is. Rows are identified by this, never by list position.</summary>
        public Entity Entity;

        public string Name;
        public int Stored;
        public int Capacity;
        public float3 Position;

        /// <summary>False when neither the building nor anything it belongs to has a position.</summary>
        public bool HasPosition;

        /// <summary>0-100. The number the list is sorted on and coloured by.</summary>
        public float Fullness
        {
            get { return Capacity > 0 ? 100f * Stored / Capacity : 0f; }
        }
    }

    /// <summary>
    /// The city's cemeteries, fullest first.
    ///
    /// The same question the school list answers, asked about the other thing that quietly fills
    /// up and is only noticed once it has: the strip's percentage is city-wide, and a city with
    /// room on average can still have the one cemetery its hearses actually drive to full.
    ///
    /// Sources are all public. <see cref="DeathcareFacility"/> marks the building and carries
    /// m_LongTermStoredCount, the bodies actually interred; its prefab's
    /// <see cref="DeathcareFacilityData"/> carries m_StorageCapacity and m_LongTermStorage.
    ///
    /// Only long-term facilities are listed, which is what "cemetery" means here: a crematorium
    /// carries the same component but processes rather than stores, and it is the deathcare row
    /// rather than this one that reports on it. Mirroring the split vanilla itself makes is what
    /// keeps this list agreeing with the number that opened it.
    /// </summary>
    public sealed class CemeteryBreakdown
    {
        private readonly List<CemeteryEntry> _entries = new List<CemeteryEntry>();

        public IReadOnlyList<CemeteryEntry> Entries
        {
            get { return _entries; }
        }

        public void Refresh(EntityManager entities, EntityQuery query, NameSystem names, bool wanted)
        {
            _entries.Clear();

            if (!wanted || query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using (var buildings = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var building in buildings)
                {
                    try
                    {
                        var prefab = entities.GetComponentData<PrefabRef>(building).m_Prefab;
                        if (!entities.HasComponent<DeathcareFacilityData>(prefab))
                        {
                            continue;
                        }

                        var data = entities.GetComponentData<DeathcareFacilityData>(prefab);

                        // A crematorium is the deathcare row's business, not this one's.
                        if (!data.m_LongTermStorage)
                        {
                            continue;
                        }

                        // The prefab carries only the BASE capacity; expansions are separate
                        // entities in an InstalledUpgrade buffer. Same combine the game's own
                        // panels do, and the same one the school list needs.
                        if (entities.HasBuffer<InstalledUpgrade>(building))
                        {
                            var upgrades = entities.GetBuffer<InstalledUpgrade>(building, true);
                            UpgradeUtils.CombineStats(entities, ref data, upgrades);
                        }

                        // Not running is not the same as full, and vanilla leaves it out.
                        if (BuildingLocator.IsShutDown(entities, building))
                        {
                            continue;
                        }

                        var facility = entities.GetComponentData<Game.Buildings.DeathcareFacility>(building);

                        float3 position;
                        var hasPosition = BuildingLocator.PositionOf(entities, building, out position);

                        _entries.Add(new CemeteryEntry
                        {
                            Entity = building,
                            Name = BuildingLocator.SafeName(names, building, "Cemetery"),
                            Stored = facility.m_LongTermStoredCount,
                            Capacity = data.m_StorageCapacity,
                            Position = position,
                            HasPosition = hasPosition
                        });
                    }
                    catch (Exception e)
                    {
                        Mod.Log.Info("Could not read cemetery #" + building.Index + ": " + e);
                    }
                }
            }

            // Fullest first: the top of the list is the one that needs building next to.
            _entries.Sort(delegate(CemeteryEntry a, CemeteryEntry b)
            {
                return b.Fullness.CompareTo(a.Fullness);
            });
        }

        /// <summary>The cemetery with this entity id, or null.</summary>
        public CemeteryEntry Find(int entityIndex)
        {
            foreach (var entry in _entries)
            {
                if (entry.Entity.Index == entityIndex)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>Moves the camera to one cemetery, named by its entity id.</summary>
        public bool Jump(int entityIndex, CameraUpdateSystem camera)
        {
            var entry = Find(entityIndex);
            if (entry == null || !entry.HasPosition)
            {
                return false;
            }

            return BuildingLocator.Jump(entry.Position, camera);
        }
    }
}
