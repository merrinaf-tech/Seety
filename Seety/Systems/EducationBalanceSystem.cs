using Colossal.Serialization.Entities;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using Seety.Vitals;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Seety.Systems
{
    /// <summary>
    /// Optional, read-only observation of resident education. One Burst pass every 8192 simulation
    /// frames while enabled; closing the Workers window does not interrupt an observation.
    /// The ledger lives in memory for this play session and is cleared on load or when switched off.
    /// </summary>
    public partial class EducationBalanceSystem : GameSystemBase
    {
        private EntityQuery _citizens;
        private SimulationSystem _simulation;
        private uint _lastFrame;
        private bool _tracking;
        private double _worstMilliseconds;
        public readonly EducationBalanceLedger Ledger = new EducationBalanceLedger();

        public bool Tracking { get { return _tracking; } }

        protected override void OnCreate()
        {
            base.OnCreate();
            _simulation = World.GetOrCreateSystemManaged<SimulationSystem>();
            _citizens = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Citizen>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            Ledger.Clear();
            _lastFrame = 0;
        }

        public void SetTracking(bool tracking)
        {
            if (_tracking == tracking) return;
            _tracking = tracking;
            Ledger.Clear();
            _lastFrame = 0;
            if (tracking) Capture();
        }

        protected override void OnUpdate()
        {
            if (!_tracking || _simulation == null) return;
            var frame = _simulation.frameIndex;
            if (frame < _lastFrame)
            {
                // A different city can start at a lower simulation frame after a load.
                Ledger.Clear();
                _lastFrame = 0;
            }
            if (!Ledger.HasBaseline || frame - _lastFrame >= EducationBalanceLedger.FramesPerSample)
                Capture();
        }

        private void Capture()
        {
            if (_simulation == null) return;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var frame = _simulation.frameIndex;
            var capacity = System.Math.Max(1, _citizens.CalculateEntityCount());
            using (var residents = new NativeList<ResidentEducation>(capacity, Allocator.TempJob))
            {
                var job = new CaptureJob
                {
                    Entities = GetEntityTypeHandle(),
                    Citizens = GetComponentTypeHandle<Citizen>(true),
                    Members = GetComponentTypeHandle<HouseholdMember>(true),
                    Health = GetComponentTypeHandle<HealthProblem>(true),
                    CommuterHouseholds = GetComponentLookup<CommuterHousehold>(true),
                    Results = residents
                };
                JobChunkExtensions.Run(job, _citizens);
                Ledger.Begin(frame);
                for (var i = 0; i < residents.Length; i++)
                {
                    var entry = residents[i];
                    Ledger.Observe(entry.Key, entry.Level);
                }
                Ledger.End();
            }
            _lastFrame = frame;
            clock.Stop();
            if (clock.Elapsed.TotalMilliseconds > _worstMilliseconds)
            {
                _worstMilliseconds = clock.Elapsed.TotalMilliseconds;
                Mod.Log.Info("Education balance: worst sample "
                    + _worstMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    + " ms for " + capacity + " citizen entities.");
            }
        }

        private struct ResidentEducation
        {
            public ulong Key;
            public byte Level;
        }

        [BurstCompile]
        private struct CaptureJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle Entities;
            [ReadOnly] public ComponentTypeHandle<Citizen> Citizens;
            [ReadOnly] public ComponentTypeHandle<HouseholdMember> Members;
            [ReadOnly] public ComponentTypeHandle<HealthProblem> Health;
            [ReadOnly] public ComponentLookup<CommuterHousehold> CommuterHouseholds;
            public NativeList<ResidentEducation> Results;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(Entities);
                var citizens = chunk.GetNativeArray(ref Citizens);
                var members = chunk.Has<HouseholdMember>()
                    ? chunk.GetNativeArray(ref Members) : default(NativeArray<HouseholdMember>);
                var health = chunk.Has<HealthProblem>()
                    ? chunk.GetNativeArray(ref Health) : default(NativeArray<HealthProblem>);

                for (var i = 0; i < citizens.Length; i++)
                {
                    var citizen = citizens[i];
                    if ((citizen.m_State & CitizenFlags.ValidCitizen) == 0
                        || (citizen.m_State & CitizenFlags.Tourist) != 0
                        || (health.IsCreated && CitizenUtils.IsDead(health[i]))
                        || (members.IsCreated && CommuterHouseholds.HasComponent(members[i].m_Household)))
                        continue;

                    var level = citizen.GetEducationLevel();
                    if (level < 0 || level >= 5) continue;
                    var entity = entities[i];
                    Results.Add(new ResidentEducation
                    {
                        Key = ((ulong)(uint)entity.Index << 32) | (uint)entity.Version,
                        Level = (byte)level
                    });
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }
    }
}
