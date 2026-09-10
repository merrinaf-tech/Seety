using System;
using System.Collections.Generic;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Seety.Vitals
{
    /// <summary>One school, with how full it is and where to find it.</summary>
    public sealed class SchoolEntry
    {
        /// <summary>
        /// Which school this actually is.
        ///
        /// The list is re-sorted by fullness twice a second while its window is open, and
        /// fullness moves constantly, so two schools within a student of each other swap places
        /// between one refresh and the next. A row identified by its position in the list is
        /// therefore identifying whatever happens to be sitting there when the click lands, not
        /// the school the player aimed at - which is how a click on "Small High School" jumped
        /// the camera to "EE High School" instead. The row carries this entity's id now.
        /// </summary>
        public Entity Entity;

        public string Name;
        public int Students;
        public int Capacity;
        public float3 Position;

        /// <summary>
        /// False only when neither the school entity nor anything it belongs to has a position.
        ///
        /// A school inside a signature building is a sub-building or an upgrade: it carries no
        /// Transform of its own, because it is placed relative to its parent. Without this Jump
        /// fell back to float3.zero and the camera landed at the world origin - open water more
        /// often than not - so the row was reported as non-clickable instead. That was honest but
        /// unhelpful: the parent building does have a position, and it is where the player wants
        /// to be taken. See PositionOf.
        /// </summary>
        public bool HasPosition;

        /// <summary>0-100. The number the list is sorted on and coloured by.</summary>
        public float Fullness
        {
            get { return Capacity > 0 ? 100f * Students / Capacity : 0f; }
        }
    }

    /// <summary>
    /// The schools of one education level, fullest first.
    ///
    /// This is the answer to "which of my schools is about to overflow", which the strip's
    /// percentage can only ever answer for the city as a whole. Building Use paints the same
    /// information onto the map, and doing that needs a Harmony patch over
    /// `ObjectColorSystem.OnUpdate` because no vanilla infomode can be pointed at a number of
    /// ours. A clickable list answers the same question and keeps Seety to what it has always
    /// been: it reads, and it takes you there.
    ///
    /// Sources are all public: `Game.Buildings.School` marks the building, its prefab's
    /// `SchoolData` carries `m_StudentCapacity` and `m_EducationLevel`, the `Student` buffer is
    /// the roll, and `Transform.m_Position` is where to point the camera.
    /// </summary>
    public sealed class SchoolBreakdown
    {
        /// <summary>Education level ids, in the order the catalogue uses them.</summary>
        public static readonly string[] VitalIds = { "elementary", "highschool", "college", "university" };

        private readonly List<SchoolEntry> _entries = new List<SchoolEntry>();

        public IReadOnlyList<SchoolEntry> Entries
        {
            get { return _entries; }
        }

        /// <summary>The level a vital id refers to, or -1 when it is not a school row.</summary>
        public static int LevelFor(string vitalId)
        {
            for (var i = 0; i < VitalIds.Length; i++)
            {
                if (VitalIds[i] == vitalId)
                {
                    return i;
                }
            }

            return -1;
        }

        public void Refresh(EntityManager entities, EntityQuery query, NameSystem names, int level)
        {
            _entries.Clear();

            if (level < 0 || query.IsEmptyIgnoreFilter)
            {
                // The early return is a branch too, and an empty query would look exactly like a
                // silent failure from the log's point of view. level < 0 is the ordinary case -
                // it fires on every non-school row - so only the surprising half is written down.
                if (level >= 0)
                {
                    Mod.Log.Info("Schools refreshed: level=" + level + " but the query is empty.");
                }

                return;
            }

            // One line per refresh saying what the query found and what became of it. The log
            // has been silent through three attempts at this - no jump, no failure, not even the
            // unplaceable report - which is contradictory: a school with a position is clickable
            // and a click writes a line either way. Silence means the assumption is wrong
            // somewhere before all of that, so this counts every branch rather than guessing
            // which one to instrument next.
            var matched = 0;
            var noSchoolData = 0;
            var shutDown = 0;
            var wrongLevel = 0;
            var failed = 0;

            using (var schools = query.ToEntityArray(Allocator.Temp))
            {
                matched = schools.Length;

                foreach (var school in schools)
                {
                    try
                    {
                        var prefab = entities.GetComponentData<PrefabRef>(school).m_Prefab;
                        if (!entities.HasComponent<SchoolData>(prefab))
                        {
                            noSchoolData++;
                            continue;
                        }

                        var data = entities.GetComponentData<SchoolData>(prefab);

                        // The prefab carries only the BASE capacity. Expansions are separate
                        // entities in an InstalledUpgrade buffer, and their capacity has to be
                        // combined in - which is what the game's own education panel does.
                        // Without this a school with two wings reported 448 students in 250
                        // places, and the list happily showed 179% full.
                        if (entities.HasBuffer<InstalledUpgrade>(school))
                        {
                            var upgrades = entities.GetBuffer<InstalledUpgrade>(school, true);
                            UpgradeUtils.CombineStats(entities, ref data, upgrades);
                        }

                        // A building with no efficiency is not running, and vanilla leaves it out
                        // of the totals. Counting it would promise places that do not exist.
                        if (BuildingLocator.IsShutDown(entities, school))
                        {
                            shutDown++;
                            continue;
                        }

                        // m_EducationLevel is 1-based on the prefab - elementary is 1 - while the
                        // strip counts its four school rows from zero.
                        if (data.m_EducationLevel - 1 != level)
                        {
                            wrongLevel++;
                            continue;
                        }

                        var roll = entities.GetBuffer<Game.Buildings.Student>(school, true);

                        float3 position;
                        var hasPosition = BuildingLocator.PositionOf(entities, school, out position);

                        if (!hasPosition)
                        {
                            ReportUnplaceable(entities, school, names);
                        }

                        _entries.Add(new SchoolEntry
                        {
                            Entity = school,
                            Name = BuildingLocator.SafeName(names, school, "School"),
                            Students = roll.Length,
                            Capacity = data.m_StudentCapacity,
                            Position = position,
                            HasPosition = hasPosition
                        });
                    }
                    catch (Exception e)
                    {
                        // Info, not Warn: a swallowed exception here would drop the school from
                        // the list silently, and the whole point of this pass is that nothing be
                        // silent. Warn may not reach the file at the configured level.
                        failed++;
                        Mod.Log.Info("Could not read school #" + school.Index + ": " + e);
                    }
                }
            }

            var clickable = 0;
            foreach (var entry in _entries)
            {
                if (entry.HasPosition)
                {
                    clickable++;
                }
            }

            Mod.Log.Info("Schools refreshed: level=" + level
                + " matchedQuery=" + matched
                + " listed=" + _entries.Count
                + " clickable=" + clickable
                + " (skipped: noSchoolData=" + noSchoolData
                + " shutDown=" + shutDown
                + " wrongLevel=" + wrongLevel
                + " threw=" + failed + ")");

            // Fullest first: the list is read from the top and the top is what needs building.
            _entries.Sort(delegate(SchoolEntry a, SchoolEntry b)
            {
                return b.Fullness.CompareTo(a.Fullness);
            });
        }

        /// <summary>Schools already reported as unplaceable, so the log says each one once.</summary>
        private readonly HashSet<string> _reported = new HashSet<string>();

        /// <summary>
        /// Writes down exactly why a school has nowhere for the camera to go.
        ///
        /// Following Game.Common.Owner up was the obvious answer for a school inside a signature
        /// building and it did not work, so the next test needs to say what the chain actually
        /// looks like rather than leaving it to be guessed at again. Many of these buildings come
        /// from mods Paradox distributes, so they need not be shaped like a vanilla one.
        /// </summary>
        private void ReportUnplaceable(EntityManager entities, Entity school, NameSystem names)
        {
            var name = BuildingLocator.SafeName(names, school, "School");
            if (!_reported.Add(name))
            {
                return;
            }

            var chain = string.Empty;
            var current = school;

            for (var depth = 0; depth < 5; depth++)
            {
                chain += (depth == 0 ? "" : " -> ") + "#" + current.Index
                    + (entities.HasComponent<Transform>(current) ? " [Transform]" : " [no Transform]")
                    + (entities.HasComponent<Game.Buildings.Building>(current) ? " [Building]" : string.Empty)
                    + (entities.HasComponent<Game.Buildings.Extension>(current) ? " [Extension]" : string.Empty)
                    + (entities.HasBuffer<Game.Objects.SubObject>(current) ? " [has SubObjects]" : string.Empty);

                if (!entities.HasComponent<Game.Common.Owner>(current))
                {
                    chain += " [no Owner]";
                    break;
                }

                var owner = entities.GetComponentData<Game.Common.Owner>(current).m_Owner;
                if (owner == Entity.Null || owner == current)
                {
                    chain += " [Owner is null or self]";
                    break;
                }

                current = owner;
            }

            Mod.Log.Info("School '" + name + "' has nowhere to jump to: " + chain);
        }

        /// <summary>The school with this entity id, or null - see SchoolEntry.Entity.</summary>
        public SchoolEntry Find(int entityIndex)
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

        /// <summary>
        /// Moves the camera to one school, named by its entity id rather than by where it
        /// happens to sit in the list. See SchoolEntry.Entity for why that distinction matters.
        /// </summary>
        public bool Jump(int entityIndex, CameraUpdateSystem camera)
        {
            var entry = Find(entityIndex);
            if (entry == null || !entry.HasPosition)
            {
                return false;
            }

            if (camera == null || camera.activeCameraController == null)
            {
                return false;
            }

            return BuildingLocator.Jump(entry.Position, camera);
        }

    }
}
