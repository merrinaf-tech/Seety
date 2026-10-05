using System;
using System.Collections.Generic;
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
    /// Read-only observation of resident education. One Burst pass every 8192 simulation frames.
    /// Only bounded aggregate history is saved with the city; citizen identities stay in memory.
    /// </summary>
    public partial class EducationBalanceSystem : GameSystemBase, IDefaultSerializable
    {
        private const int SaveSchema = 1;
        private EntityQuery _citizens;
        private SimulationSystem _simulation;
        private TimeSystem _timeSystem;
        private NativeList<ResidentEducation> _residents;
        private uint _lastFrame;
        private bool _tracking;
        private bool _inGame;
        private readonly double[] _recentMilliseconds = new double[64];
        private int _sampleCount;
        public readonly EducationBalanceLedger Ledger = new EducationBalanceLedger();

        public bool Tracking { get { return _tracking; } }
        public double MeanMilliseconds { get { return SampleMetric(0); } }
        public double P95Milliseconds { get { return SampleMetric(1); } }
        public double WorstMilliseconds { get { return SampleMetric(2); } }

        protected override void OnCreate()
        {
            base.OnCreate();
            _simulation = World.GetOrCreateSystemManaged<SimulationSystem>();
            _timeSystem = World.GetOrCreateSystemManaged<TimeSystem>();
            _residents = new NativeList<ResidentEducation>(1, Allocator.Persistent);
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
            _tracking = false;
            _inGame = false;
            _sampleCount = 0;
        }

        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            _inGame = mode == GameMode.Game;
            if (_inGame && Mod.Settings != null && Mod.Settings.AutomaticEducationBalance)
                SetTracking(true);
        }

        protected override void OnDestroy()
        {
            if (_residents.IsCreated) _residents.Dispose();
            base.OnDestroy();
        }

        public void SetTracking(bool tracking)
        {
            if (_tracking == tracking) return;
            _tracking = tracking;
            Ledger.BreakObservation();
            _lastFrame = 0;
            if (tracking && _inGame) Capture();
        }

        public void ClearHistory()
        {
            Ledger.Clear();
            _lastFrame = 0;
            if (_tracking && _inGame) Capture();
        }

        protected override void OnUpdate()
        {
            if (!_tracking || !_inGame || _simulation == null) return;
            var frame = _simulation.frameIndex;
            if (frame < _lastFrame)
            {
                // A different city can start at a lower simulation frame after a load.
                Ledger.BreakObservation();
                _lastFrame = 0;
            }
            if (!Ledger.HasBaseline || frame - _lastFrame >= EducationBalanceLedger.FramesPerSample)
                Capture();
        }

        private void Capture()
        {
            if (_simulation == null || !_residents.IsCreated) return;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var frame = _simulation.frameIndex;
            var capacity = System.Math.Max(1, _citizens.CalculateEntityCount());
            if (_residents.Capacity < capacity) _residents.Capacity = capacity;
            _residents.Clear();
            var job = new CaptureJob
            {
                Entities = GetEntityTypeHandle(),
                Citizens = GetComponentTypeHandle<Citizen>(true),
                Members = GetComponentTypeHandle<HouseholdMember>(true),
                Health = GetComponentTypeHandle<HealthProblem>(true),
                CommuterHouseholds = GetComponentLookup<CommuterHousehold>(true),
                Results = _residents
            };
            JobChunkExtensions.Run(job, _citizens);
            Ledger.Begin(frame, _timeSystem.GetCurrentDateTime());
            for (var i = 0; i < _residents.Length; i++)
            {
                var entry = _residents[i];
                Ledger.Observe(entry.Key, entry.Level);
            }
            Ledger.End();
            _lastFrame = frame;
            clock.Stop();
            _recentMilliseconds[_sampleCount % _recentMilliseconds.Length] = clock.Elapsed.TotalMilliseconds;
            _sampleCount++;
            if (_sampleCount % 32 == 0)
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                Mod.Log.Info("Education balance: " + capacity + " citizens; last 64 samples mean "
                    + MeanMilliseconds.ToString("F2", culture) + " ms, p95 "
                    + P95Milliseconds.ToString("F2", culture) + " ms, worst "
                    + WorstMilliseconds.ToString("F2", culture) + " ms.");
            }
        }

        private double SampleMetric(int kind)
        {
            var length = System.Math.Min(_sampleCount, _recentMilliseconds.Length);
            if (length == 0) return 0;
            var values = new double[length];
            Array.Copy(_recentMilliseconds, values, length);
            if (kind == 0)
            {
                double sum = 0;
                foreach (var value in values) sum += value;
                return sum / length;
            }
            Array.Sort(values);
            return kind == 2 ? values[length - 1] : values[(int)System.Math.Ceiling(length * 0.95) - 1];
        }

        public void SetDefaults(Context context)
        {
            Ledger.Clear();
            _tracking = false;
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(SaveSchema);
            writer.Write(Ledger.Days.Count);
            foreach (var day in Ledger.Days)
            {
                writer.Write(day.Day);
                writer.Write(day.Complete);
                writer.Write(day.GapBefore);
                writer.Write(day.StartFrame);
                writer.Write(day.EndFrame);
                writer.Write(day.StartTime.Ticks);
                writer.Write(day.EndTime.Ticks);
                WriteCounts(writer, day.Start);
                WriteCounts(writer, day.End);
                WriteCounts(writer, day.FirstSeen);
                WriteCounts(writer, day.NoLongerSeen);
                WriteCounts(writer, day.ChangedIn);
                WriteCounts(writer, day.ChangedOut);
                WriteCounts(writer, day.Transitions);
            }
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int schema);
            reader.Read(out int count);
            if (schema != SaveSchema || count < 0 || count > 64)
                throw new InvalidOperationException("Unsupported Seety education history schema.");
            var history = new List<EducationBalanceDay>(count);
            for (var i = 0; i < count; i++)
            {
                var day = new EducationBalanceDay();
                reader.Read(out day.Day);
                reader.Read(out day.Complete);
                reader.Read(out day.GapBefore);
                reader.Read(out day.StartFrame);
                reader.Read(out day.EndFrame);
                reader.Read(out long startTicks);
                reader.Read(out long endTicks);
                if (startTicks >= DateTime.MinValue.Ticks && startTicks <= DateTime.MaxValue.Ticks
                    && endTicks >= DateTime.MinValue.Ticks && endTicks <= DateTime.MaxValue.Ticks)
                {
                    day.StartTime = new DateTime(startTicks);
                    day.EndTime = new DateTime(endTicks);
                }
                ReadCounts(reader, day.Start);
                ReadCounts(reader, day.End);
                ReadCounts(reader, day.FirstSeen);
                ReadCounts(reader, day.NoLongerSeen);
                ReadCounts(reader, day.ChangedIn);
                ReadCounts(reader, day.ChangedOut);
                ReadCounts(reader, day.Transitions);
                history.Add(day);
            }
            Ledger.Restore(history);
            _tracking = false;
            _lastFrame = 0;
        }

        private static void WriteCounts<TWriter>(TWriter writer, int[] values) where TWriter : IWriter
        {
            foreach (var value in values) writer.Write(value);
        }

        private static void ReadCounts<TReader>(TReader reader, int[] values) where TReader : IReader
        {
            for (var i = 0; i < values.Length; i++) reader.Read(out values[i]);
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
