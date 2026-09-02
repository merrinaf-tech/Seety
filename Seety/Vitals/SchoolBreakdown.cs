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
        public string Name;
        public int Students;
        public int Capacity;
        public float3 Position;

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
                return;
            }

            using (var schools = query.ToEntityArray(Allocator.Temp))
            {
                foreach (var school in schools)
                {
                    try
                    {
                        var prefab = entities.GetComponentData<PrefabRef>(school).m_Prefab;
                        if (!entities.HasComponent<SchoolData>(prefab))
                        {
                            continue;
                        }

                        var data = entities.GetComponentData<SchoolData>(prefab);

                        // m_EducationLevel is 1-based on the prefab - elementary is 1 - while the
                        // strip counts its four school rows from zero.
                        if (data.m_EducationLevel - 1 != level)
                        {
                            continue;
                        }

                        var roll = entities.GetBuffer<Game.Buildings.Student>(school, true);
                        var position = entities.HasComponent<Transform>(school)
                            ? entities.GetComponentData<Transform>(school).m_Position
                            : float3.zero;

                        _entries.Add(new SchoolEntry
                        {
                            Name = SafeName(names, school),
                            Students = roll.Length,
                            Capacity = data.m_StudentCapacity,
                            Position = position
                        });
                    }
                    catch (Exception e)
                    {
                        Mod.Log.Warn("Could not read a school: " + e.Message);
                    }
                }
            }

            // Fullest first: the list is read from the top and the top is what needs building.
            _entries.Sort(delegate(SchoolEntry a, SchoolEntry b)
            {
                return b.Fullness.CompareTo(a.Fullness);
            });
        }

        /// <summary>Moves the camera to a school by its position in the list.</summary>
        public bool Jump(int index, CameraUpdateSystem camera)
        {
            if (index < 0 || index >= _entries.Count)
            {
                return false;
            }

            if (camera == null || camera.activeCameraController == null)
            {
                return false;
            }

            var target = _entries[index].Position;
            camera.activeCameraController.pivot = new UnityEngine.Vector3(target.x, target.y, target.z);
            return true;
        }

        private static string SafeName(NameSystem names, Entity entity)
        {
            try
            {
                var label = names.GetRenderedLabelName(entity);
                return string.IsNullOrEmpty(label) ? "School" : label;
            }
            catch
            {
                return "School";
            }
        }
    }
}
