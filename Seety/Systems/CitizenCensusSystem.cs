using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Companies;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Seety.Systems
{
    /// <summary>
    /// Counts every citizen once, split by education level.
    ///
    /// This exists because the interesting columns - unemployed, working below their level,
    /// working outside the city, commuting in - are not exposed per education level anywhere.
    /// CountHouseholdDataSystem computes them internally as m_EmployableByEducation0..4 and keeps
    /// them in a private struct. Rather than reach in with reflection or apportion a city-wide
    /// figure across levels, Seety does the same count itself.
    ///
    /// On cost: this is the same iteration vanilla already runs. CountHouseholdDataSystem's
    /// CountCitizensJob walks exactly these chunks, Burst-compiled, every 16 simulation frames,
    /// for the whole game. Ours is Burst-compiled too, runs twice a second, and only while the
    /// window showing it is open. It is a fraction of what the game already pays.
    ///
    /// The classification mirrors that job deliberately - the Tourist and ValidCitizen filters,
    /// the dead check, the age switch, and the rule that someone holding a job below their
    /// education still counts as looking for work. Diverging would produce a table that quietly
    /// disagrees with the game.
    /// </summary>
    public partial class CitizenCensusSystem : GameSystemBase
    {
        /// <summary>Education levels, 0 (uneducated) to 4 (highly educated).</summary>
        public const int Levels = 5;

        /// <summary>Counters held per level. Order matters: Field indexes into them.</summary>
        public const int Fields = 13;

        public enum Field
        {
            Total = 0,
            Children = 1,
            Teens = 2,
            Adults = 3,
            Seniors = 4,
            Students = 5,
            Workers = 6,
            Unemployed = 7,
            Under = 8,
            Outside = 9,
            Commuters = 10,

            /// <summary>Jobs that exist at this level, counted from the buildings themselves.</summary>
            Jobs = 11,

            /// <summary>Of those, the ones nobody is doing.</summary>
            Vacant = 12
        }

        private EntityQuery _query;

        /// <summary>Every company that employs people. See WorkplaceJob for why this is counted here.</summary>
        private EntityQuery _workplaceQuery;
        private NativeArray<int> _results;

        public int Get(int level, Field field)
        {
            var index = level * Fields + (int)field;
            return _results.IsCreated && index < _results.Length ? _results[index] : 0;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            _query = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Citizen>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            _workplaceQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<WorkProvider>(),
                    ComponentType.ReadOnly<Employee>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            _results = new NativeArray<int>(Levels * Fields, Allocator.Persistent);

            // Nothing drives this on a schedule; it runs when the table asks for it.
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            if (_results.IsCreated)
            {
                _results.Dispose();
            }

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
        }

        /// <summary>Recounts now. Called only while the workforce window is open.</summary>
        public void Recount()
        {
            if (!_results.IsCreated)
            {
                return;
            }

            for (var i = 0; i < _results.Length; i++)
            {
                _results[i] = 0;
            }

            var job = new CensusJob
            {
                m_CitizenType = GetComponentTypeHandle<Citizen>(true),
                m_WorkerType = GetComponentTypeHandle<Worker>(true),
                m_HealthProblemType = GetComponentTypeHandle<HealthProblem>(true),
                m_OutsideConnections = GetComponentLookup<Game.Objects.OutsideConnection>(true),
                m_Results = _results
            };

            // Run rather than Schedule: one pass is quick, and the caller wants the answer in the
            // same frame to write it straight into the binding.
            JobChunkExtensions.Run(job, _query);

            var workplaces = new WorkplaceJob
            {
                m_WorkProviderType = GetComponentTypeHandle<WorkProvider>(true),
                m_PrefabRefType = GetComponentTypeHandle<PrefabRef>(true),
                m_EmployeeType = GetBufferTypeHandle<Employee>(true),
                m_PropertyRenterType = GetComponentTypeHandle<PropertyRenter>(true),
                m_PrefabRefs = GetComponentLookup<PrefabRef>(true),
                m_WorkplaceData = GetComponentLookup<WorkplaceData>(true),
                m_SpawnableBuildings = GetComponentLookup<SpawnableBuildingData>(true),
                m_Results = _results
            };

            JobChunkExtensions.Run(workplaces, _workplaceQuery);
        }

        /// <summary>
        /// Counts the jobs the city actually has, level by level, from the companies offering them.
        ///
        /// The obvious source was the workplaces.workplacesData binding, the one vanilla's own
        /// Workplaces panel uses. It reads zero for us: like every infoview binding it is filled by
        /// a job that only runs while that panel is on screen, and subscribing from a mod does not
        /// wake it. Three rounds of the table shipped with an empty column because of it.
        ///
        /// So the jobs are counted the way the game counts them, through the same public helper:
        /// each employer's WorkProvider.m_MaxWorkers and its prefab's WorkplaceComplexity, run
        /// through EconomyUtils.CalculateNumberOfWorkplaces with the building level, which splits
        /// the total across the five education levels exactly as the game does. Filled positions
        /// come from the Employee buffer, which already records each worker's level.
        /// </summary>
        [BurstCompile]
        private struct WorkplaceJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<WorkProvider> m_WorkProviderType;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabRefType;
            [ReadOnly] public BufferTypeHandle<Employee> m_EmployeeType;
            [ReadOnly] public ComponentTypeHandle<PropertyRenter> m_PropertyRenterType;

            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefs;
            [ReadOnly] public ComponentLookup<WorkplaceData> m_WorkplaceData;
            [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;

            public NativeArray<int> m_Results;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var providers = chunk.GetNativeArray(ref m_WorkProviderType);
                var prefabs = chunk.GetNativeArray(ref m_PrefabRefType);
                var employees = chunk.GetBufferAccessor(ref m_EmployeeType);

                var hasRenter = chunk.Has<PropertyRenter>();
                var renters = hasRenter
                    ? chunk.GetNativeArray(ref m_PropertyRenterType)
                    : default(NativeArray<PropertyRenter>);

                for (var i = 0; i < providers.Length; i++)
                {
                    var prefab = prefabs[i].m_Prefab;
                    if (!m_WorkplaceData.HasComponent(prefab))
                    {
                        continue;
                    }

                    var data = m_WorkplaceData[prefab];
                    var level = BuildingLevel(hasRenter ? renters[i].m_Property : Entity.Null);

                    var jobs = Game.Economy.EconomyUtils.CalculateNumberOfWorkplaces(
                        providers[i].m_MaxWorkers, data.m_Complexity, level);

                    AddLevels(Field.Jobs, jobs);

                    // Vacancies start as every post and have the staff subtracted, so a level with
                    // more people than posts - which happens while a building downgrades - cannot
                    // push the column negative.
                    AddLevels(Field.Vacant, jobs);

                    var staff = employees[i];
                    for (var e = 0; e < staff.Length; e++)
                    {
                        int lvl = staff[e].m_Level;
                        if (lvl >= 0 && lvl < Levels)
                        {
                            Add(lvl, Field.Vacant, -1);
                        }
                    }
                }
            }

            private void AddLevels(Field field, Game.Companies.Workplaces jobs)
            {
                Add(0, field, jobs.m_Uneducated);
                Add(1, field, jobs.m_PoorlyEducated);
                Add(2, field, jobs.m_Educated);
                Add(3, field, jobs.m_WellEducated);
                Add(4, field, jobs.m_HighlyEducated);
            }

            /// <summary>
            /// The level of the building a company rents, or 1 for anything that does not rent -
            /// city services and the like, which have no zone level.
            /// </summary>
            private int BuildingLevel(Entity property)
            {
                if (property == Entity.Null || !m_PrefabRefs.HasComponent(property))
                {
                    return 1;
                }

                var buildingPrefab = m_PrefabRefs[property].m_Prefab;
                if (!m_SpawnableBuildings.HasComponent(buildingPrefab))
                {
                    return 1;
                }

                return m_SpawnableBuildings[buildingPrefab].m_Level;
            }

            private void Add(int level, Field field, int amount)
            {
                var index = level * Fields + (int)field;
                m_Results[index] = m_Results[index] + amount;
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        [BurstCompile]
        private struct CensusJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Citizen> m_CitizenType;
            [ReadOnly] public ComponentTypeHandle<Worker> m_WorkerType;
            [ReadOnly] public ComponentTypeHandle<HealthProblem> m_HealthProblemType;
            [ReadOnly] public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

            public NativeArray<int> m_Results;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var citizens = chunk.GetNativeArray(ref m_CitizenType);

                var hasWorker = chunk.Has<Worker>();
                var isStudentChunk = chunk.Has<Game.Citizens.Student>();
                var hasHealth = chunk.Has<HealthProblem>();

                var workers = hasWorker ? chunk.GetNativeArray(ref m_WorkerType) : default(NativeArray<Worker>);
                var health = hasHealth ? chunk.GetNativeArray(ref m_HealthProblemType) : default(NativeArray<HealthProblem>);

                for (var i = 0; i < citizens.Length; i++)
                {
                    var citizen = citizens[i];

                    if ((citizen.m_State & CitizenFlags.ValidCitizen) == 0)
                    {
                        continue;
                    }

                    // Tourists never belong here. Commuters get their own column and nothing else:
                    // vanilla leaves them out of every other figure, so mixing them in would stop
                    // the table adding up against the game panels.
                    if ((citizen.m_State & CitizenFlags.Tourist) != 0)
                    {
                        continue;
                    }

                    var level = citizen.GetEducationLevel();
                    if (level < 0 || level >= Levels)
                    {
                        continue;
                    }

                    if ((citizen.m_State & CitizenFlags.Commuter) != 0)
                    {
                        Add(level, Field.Commuters);
                        continue;
                    }

                    if (hasHealth && CitizenUtils.IsDead(health[i]))
                    {
                        continue;
                    }

                    Add(level, Field.Total);

                    var age = citizen.GetAge();
                    switch (age)
                    {
                        case CitizenAge.Child: Add(level, Field.Children); break;
                        case CitizenAge.Teen: Add(level, Field.Teens); break;
                        case CitizenAge.Adult: Add(level, Field.Adults); break;
                        case CitizenAge.Elderly: Add(level, Field.Seniors); break;
                    }

                    if (isStudentChunk)
                    {
                        Add(level, Field.Students);
                    }

                    var worksOutside = false;

                    if (hasWorker)
                    {
                        var worker = workers[i];
                        worksOutside = m_OutsideConnections.HasComponent(worker.m_Workplace);

                        if (worksOutside)
                        {
                            Add(level, Field.Outside);
                        }
                        else
                        {
                            Add(level, Field.Workers);

                            // A job below their qualification. Vanilla treats these people as
                            // still looking, which is why they are counted apart rather than
                            // simply as employed.
                            if (worker.m_Level < level)
                            {
                                Add(level, Field.Under);
                            }
                        }
                    }

                    if (CitizenUtils.IsWorkableCitizen(citizen, age, isStudentChunk, false)
                        && (!hasWorker || worksOutside))
                    {
                        Add(level, Field.Unemployed);
                    }
                }
            }

            private void Add(int level, Field field)
            {
                var index = level * Fields + (int)field;
                m_Results[index] = m_Results[index] + 1;
            }
        }
    }
}
