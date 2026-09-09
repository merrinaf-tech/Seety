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
    /// The filters mirror that job deliberately - Tourist and ValidCitizen, the dead check
    /// and the age switch - because diverging there would produce a table that quietly disagrees
    /// with the game.
    ///
    /// One column is Seety's own and is NOT a vanilla figure: someone holding a job below their
    /// education is counted in Under and nowhere else. It does not also go into Unemployed, which
    /// is exactly what the table's own note says - "Idle is anyone of working age without a job in
    /// the city". Folding the two together would double-count a person who is visibly in both
    /// columns and make the row stop adding up.
    /// </summary>
    public partial class CitizenCensusSystem : GameSystemBase
    {
        /// <summary>Education levels, 0 (uneducated) to 4 (highly educated).</summary>
        public const int Levels = 5;

        /// <summary>Counters held per level. Order matters: Field indexes into them.</summary>
        public const int Fields = 14;

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
            Vacant = 12,

            /// <summary>
            /// Students who are still children or teenagers.
            ///
            /// Students counts every citizen in a Student chunk, and university students are
            /// adults - so the table's "Kids" column, which is children minus students, went to
            /// zero on the levels where adults study. This is the part of Students that is
            /// actually a child, which is the only part Kids should be subtracting.
            /// </summary>
            ChildStudents = 13
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

            // Same shape as Game.Simulation.CountWorkplacesSystem's own query - the system behind
            // vanilla's Workplace Availability panel. Matching it here is what Jobs and Vacant
            // need to agree with that panel; see WorkplaceJob for what else that meant fixing.
            _workplaceQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<WorkProvider>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<PropertyRenter>(),
                    ComponentType.ReadOnly<Building>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Game.Objects.OutsideConnection>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
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
                m_HouseholdMemberType = GetComponentTypeHandle<HouseholdMember>(true),
                m_CommuterHouseholds = GetComponentLookup<CommuterHousehold>(true),
                m_Results = _results
            };

            // Run rather than Schedule: one pass is quick, and the caller wants the answer in the
            // same frame to write it straight into the binding.
            JobChunkExtensions.Run(job, _query);

            var workplaces = new WorkplaceJob
            {
                m_EntityType = GetEntityTypeHandle(),
                m_WorkProviderType = GetComponentTypeHandle<WorkProvider>(true),
                m_PrefabRefType = GetComponentTypeHandle<PrefabRef>(true),
                m_FreeWorkplacesType = GetComponentTypeHandle<FreeWorkplaces>(true),
                m_PropertyRenterType = GetComponentTypeHandle<PropertyRenter>(true),
                m_PrefabRefs = GetComponentLookup<PrefabRef>(true),
                m_WorkplaceData = GetComponentLookup<WorkplaceData>(true),
                m_SpawnableBuildings = GetComponentLookup<SpawnableBuildingData>(true),
                m_Buildings = GetComponentLookup<Building>(true),
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
        /// the total across the five education levels exactly as the game does.
        ///
        /// Vacant used to be Jobs with each employee's level subtracted - which produced
        /// impossible negative vacancies at some levels, because Jobs and "who counts as staff at
        /// this level" were not computed the same way the game computes them. Vacant now reads
        /// Game.Companies.FreeWorkplaces directly, the running counter CountWorkplacesSystem's own
        /// job reads for the same purpose: the game updates it itself as workers are hired and
        /// let go, so it can never disagree with itself the way a second, independent subtraction
        /// could. The building-level resolution and the skip for a workplace not yet connected to
        /// a road both mirror that same vanilla job too - both a mismatch here inflated Jobs
        /// against the vanilla panel it should agree with.
        /// </summary>
        [BurstCompile]
        private struct WorkplaceJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<WorkProvider> m_WorkProviderType;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabRefType;
            [ReadOnly] public ComponentTypeHandle<FreeWorkplaces> m_FreeWorkplacesType;
            [ReadOnly] public ComponentTypeHandle<PropertyRenter> m_PropertyRenterType;

            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefs;
            [ReadOnly] public ComponentLookup<WorkplaceData> m_WorkplaceData;
            [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;
            [ReadOnly] public ComponentLookup<Building> m_Buildings;

            public NativeArray<int> m_Results;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(m_EntityType);
                var providers = chunk.GetNativeArray(ref m_WorkProviderType);
                var prefabs = chunk.GetNativeArray(ref m_PrefabRefType);

                var hasRenter = chunk.Has<PropertyRenter>();
                var renters = hasRenter
                    ? chunk.GetNativeArray(ref m_PropertyRenterType)
                    : default(NativeArray<PropertyRenter>);

                var hasFree = chunk.Has<FreeWorkplaces>();
                var free = hasFree
                    ? chunk.GetNativeArray(ref m_FreeWorkplacesType)
                    : default(NativeArray<FreeWorkplaces>);

                for (var i = 0; i < providers.Length; i++)
                {
                    var entity = entities[i];
                    var prefab = prefabs[i].m_Prefab;
                    if (!m_WorkplaceData.HasComponent(prefab))
                    {
                        continue;
                    }

                    var level = 1;

                    if (hasRenter && renters[i].m_Property != Entity.Null)
                    {
                        var property = renters[i].m_Property;
                        if (!m_PrefabRefs.HasComponent(property))
                        {
                            continue;
                        }

                        var propertyPrefab = m_PrefabRefs[property].m_Prefab;
                        if (m_SpawnableBuildings.HasComponent(propertyPrefab))
                        {
                            level = m_SpawnableBuildings[propertyPrefab].m_Level;
                        }
                    }
                    else if (m_Buildings.HasComponent(entity) && m_Buildings[entity].m_RoadEdge == Entity.Null)
                    {
                        // Not connected to a road yet - vanilla's own count leaves it out
                        // entirely rather than promising jobs nobody can reach.
                        continue;
                    }

                    var data = m_WorkplaceData[prefab];
                    var jobs = Game.Economy.EconomyUtils.CalculateNumberOfWorkplaces(
                        providers[i].m_MaxWorkers, data.m_Complexity, level);

                    AddLevels(Field.Jobs, jobs);

                    if (hasFree)
                    {
                        var vacancies = free[i];
                        Add(0, Field.Vacant, vacancies.m_Uneducated);
                        Add(1, Field.Vacant, vacancies.m_PoorlyEducated);
                        Add(2, Field.Vacant, vacancies.m_Educated);
                        Add(3, Field.Vacant, vacancies.m_WellEducated);
                        Add(4, Field.Vacant, vacancies.m_HighlyEducated);
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

            /// <summary>
            /// Commuters are recognised by their household, not by a flag on the citizen.
            /// CitizenFlags.Commuter exists and reads false for them, which is why the column sat
            /// at zero while the game's own panels found hundreds.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;
            [ReadOnly] public ComponentLookup<CommuterHousehold> m_CommuterHouseholds;

            public NativeArray<int> m_Results;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var citizens = chunk.GetNativeArray(ref m_CitizenType);

                // Commuters are a property of the household, so the flag has to be resolved per
                // citizen before the loop rather than read off the citizen itself.
                var members = chunk.Has<HouseholdMember>()
                    ? chunk.GetNativeArray(ref m_HouseholdMemberType)
                    : default(NativeArray<HouseholdMember>);
                // Disposed at the end of this method, not left to the allocator. Execute runs
                // once per CHUNK, and a Temp allocation inside a job is only rewound when the
                // whole job finishes - so without this every chunk in the city piled up before
                // anything was released, twice a second.
                var isCommuter = new NativeArray<bool>(citizens.Length, Allocator.Temp);
                if (members.IsCreated)
                {
                    for (var c = 0; c < citizens.Length; c++)
                    {
                        isCommuter[c] = m_CommuterHouseholds.HasComponent(members[c].m_Household);
                    }
                }

                var hasWorker = chunk.Has<Worker>();
                var isStudentChunk = chunk.Has<Game.Citizens.Student>();
                var hasHealth = chunk.Has<HealthProblem>();

                var workers = hasWorker ? chunk.GetNativeArray(ref m_WorkerType) : default(NativeArray<Worker>);
                var health = hasHealth ? chunk.GetNativeArray(ref m_HealthProblemType) : default(NativeArray<HealthProblem>);

                for (var i = 0; i < citizens.Length; i++)
                {
                    var citizen = citizens[i];

                    var level = citizen.GetEducationLevel();
                    if (level < 0 || level >= Levels)
                    {
                        continue;
                    }

                    // Commuters first, before any other filter. They do not carry ValidCitizen -
                    // they do not live here - so testing that flag first threw every one of them
                    // away and left the column reading zero while the game found hundreds.
                    if (isCommuter[i])
                    {
                        Add(level, Field.Commuters);
                        continue;
                    }

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

                        if (age == CitizenAge.Child || age == CitizenAge.Teen)
                        {
                            Add(level, Field.ChildStudents);
                        }
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

                isCommuter.Dispose();
            }

            private void Add(int level, Field field)
            {
                var index = level * Fields + (int)field;
                m_Results[index] = m_Results[index] + 1;
            }
        }
    }
}
