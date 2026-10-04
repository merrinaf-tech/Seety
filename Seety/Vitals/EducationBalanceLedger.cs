using System;
using System.Collections.Generic;

namespace Seety.Vitals
{
    /// <summary>One game-day interval, measured from successive resident snapshots.</summary>
    public sealed class EducationBalanceDay
    {
        public uint Day;
        public bool Complete;
        public readonly int[] Start = new int[5];
        public readonly int[] End = new int[5];
        public readonly int[] FirstSeen = new int[5];
        public readonly int[] NoLongerSeen = new int[5];
        public readonly int[] ChangedIn = new int[5];
        public readonly int[] ChangedOut = new int[5];

        public int Net(int level) { return End[level] - Start[level]; }
    }

    /// <summary>
    /// Keeps identity only between observations. An appearance is not called immigration and a
    /// disappearance is not called emigration or death: snapshots cannot prove those causes.
    /// An entity key includes its version, so reuse of an entity index cannot look like a graduate.
    /// No citizen data or simulation component is changed.
    /// </summary>
    public sealed class EducationBalanceLedger
    {
        public const uint FramesPerSample = 8192;
        public const uint SamplesPerDay = 32;
        private const int MaxDays = 14;

        private Dictionary<ulong, byte> _previous = new Dictionary<ulong, byte>();
        private Dictionary<ulong, byte> _current = new Dictionary<ulong, byte>();
        private readonly int[] _previousTotals = new int[5];
        private readonly int[] _currentTotals = new int[5];
        private readonly List<EducationBalanceDay> _days = new List<EducationBalanceDay>();
        private bool _baseline;
        private uint _frame;

        public bool HasBaseline { get { return _baseline; } }
        public IReadOnlyList<EducationBalanceDay> Days { get { return _days; } }

        public void Clear()
        {
            _previous.Clear();
            _current.Clear();
            Array.Clear(_previousTotals, 0, 5);
            Array.Clear(_currentTotals, 0, 5);
            _days.Clear();
            _baseline = false;
        }

        public void Begin(uint frame)
        {
            _frame = frame;
            _current.Clear();
            Array.Clear(_currentTotals, 0, 5);
        }

        public void Observe(ulong entityKey, int level)
        {
            if (level < 0 || level >= 5 || _current.ContainsKey(entityKey)) return;
            _current.Add(entityKey, (byte)level);
            _currentTotals[level]++;
        }

        public void End()
        {
            if (!_baseline)
            {
                _baseline = true;
            }
            else
            {
                var dayNumber = _frame / (FramesPerSample * SamplesPerDay);
                EducationBalanceDay day;
                if (_days.Count == 0 || _days[_days.Count - 1].Day != dayNumber)
                {
                    if (_days.Count > 0) _days[_days.Count - 1].Complete = true;
                    day = new EducationBalanceDay { Day = dayNumber };
                    Array.Copy(_previousTotals, day.Start, 5);
                    _days.Add(day);
                    if (_days.Count > MaxDays) _days.RemoveAt(0);
                }
                else
                {
                    day = _days[_days.Count - 1];
                }

                foreach (var resident in _current)
                {
                    byte oldLevel;
                    if (!_previous.TryGetValue(resident.Key, out oldLevel))
                        day.FirstSeen[resident.Value]++;
                    else if (oldLevel != resident.Value)
                    {
                        day.ChangedOut[oldLevel]++;
                        day.ChangedIn[resident.Value]++;
                    }
                }
                foreach (var resident in _previous)
                    if (!_current.ContainsKey(resident.Key)) day.NoLongerSeen[resident.Value]++;
                Array.Copy(_currentTotals, day.End, 5);
            }

            var swap = _previous;
            _previous = _current;
            _current = swap;
            Array.Copy(_currentTotals, _previousTotals, 5);
        }
    }
}
