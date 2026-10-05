using System;
using System.Collections.Generic;

namespace Seety.Vitals
{
    /// <summary>A measured interval, assigned to the game date of its final sample.</summary>
    public sealed class EducationBalanceDay
    {
        public uint Day;
        public bool Complete;
        public bool GapBefore;
        public uint StartFrame;
        public uint EndFrame;
        public DateTime StartTime;
        public DateTime EndTime;
        public readonly int[] Start = new int[5];
        public readonly int[] End = new int[5];
        public readonly int[] FirstSeen = new int[5];
        public readonly int[] NoLongerSeen = new int[5];
        public readonly int[] ChangedIn = new int[5];
        public readonly int[] ChangedOut = new int[5];
        // From * 5 + To. Diagonal cells stay zero; a stable resident is not a transition.
        public readonly int[] Transitions = new int[25];

        public int Net(int level) { return End[level] - Start[level]; }
    }

    /// <summary>
    /// Compares resident identities and education between samples. A new or missing identity is
    /// not classified as a birth, immigrant, death or emigrant. The in-memory identity maps are
    /// never serialized; loading a save starts a fresh baseline and marks the observation gap.
    /// </summary>
    public sealed class EducationBalanceLedger
    {
        public const uint FramesPerSample = 8192;
        public const uint SamplesPerDay = 32;
        private const uint DaysToKeep = 14;
        private const int MaxPeriods = 64;

        private Dictionary<ulong, byte> _previous = new Dictionary<ulong, byte>();
        private Dictionary<ulong, byte> _current = new Dictionary<ulong, byte>();
        private readonly int[] _previousTotals = new int[5];
        private readonly int[] _currentTotals = new int[5];
        private readonly List<EducationBalanceDay> _days = new List<EducationBalanceDay>();
        private bool _baseline;
        private bool _gapBeforeNext;
        private uint _frame;
        private uint _previousFrame;
        private DateTime _time;
        private DateTime _previousTime;

        public bool HasBaseline { get { return _baseline; } }
        public IReadOnlyList<EducationBalanceDay> Days { get { return _days; } }

        public void Clear()
        {
            _days.Clear();
            BreakObservation();
            _gapBeforeNext = false;
        }

        /// <summary>Keep measured history, but never compare entities across a load or pause.</summary>
        public void BreakObservation()
        {
            _previous.Clear();
            _current.Clear();
            Array.Clear(_previousTotals, 0, 5);
            Array.Clear(_currentTotals, 0, 5);
            _baseline = false;
            _gapBeforeNext = _days.Count > 0;
        }

        /// <summary>Restore only bounded aggregate history from the game save.</summary>
        public void Restore(IEnumerable<EducationBalanceDay> history)
        {
            Clear();
            foreach (var day in history)
            {
                if (day == null || day.StartTime > day.EndTime) continue;
                _days.Add(day);
                if (_days.Count > MaxPeriods) _days.RemoveAt(0);
            }
            BreakObservation();
        }

        public void Begin(uint frame, DateTime time)
        {
            _frame = frame;
            _time = time;
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
                var dayNumber = (uint)(_time.Date.Ticks / TimeSpan.TicksPerDay);
                EducationBalanceDay day;
                if (_days.Count == 0 || _days[_days.Count - 1].Day != dayNumber || _gapBeforeNext)
                {
                    if (_days.Count > 0 && !_gapBeforeNext)
                        _days[_days.Count - 1].Complete = true;
                    day = new EducationBalanceDay
                    {
                        Day = dayNumber,
                        GapBefore = _gapBeforeNext,
                        StartFrame = _previousFrame,
                        StartTime = _previousTime
                    };
                    Array.Copy(_previousTotals, day.Start, 5);
                    _days.Add(day);
                    _gapBeforeNext = false;
                    while (_days.Count > 0 && (dayNumber - _days[0].Day >= DaysToKeep
                        || _days.Count > MaxPeriods)) _days.RemoveAt(0);
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
                        day.Transitions[oldLevel * 5 + resident.Value]++;
                    }
                }
                foreach (var resident in _previous)
                    if (!_current.ContainsKey(resident.Key)) day.NoLongerSeen[resident.Value]++;
                Array.Copy(_currentTotals, day.End, 5);
                day.EndFrame = _frame;
                day.EndTime = _time;
            }

            var swap = _previous;
            _previous = _current;
            _current = swap;
            Array.Copy(_currentTotals, _previousTotals, 5);
            _previousFrame = _frame;
            _previousTime = _time;
        }
    }
}
