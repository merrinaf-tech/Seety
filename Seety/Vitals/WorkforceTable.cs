using System.Collections.Generic;
using Seety.Systems;

namespace Seety.Vitals
{
    /// <summary>One education level: who has it, and what they are doing.</summary>
    public sealed class WorkforceRow
    {
        public string Level;

        /// <summary>Everyone with this education level, tourists and commuters excluded.</summary>
        public int Total;

        public int Children;
        public int Students;
        public int Seniors;

        /// <summary>Adults, which is the pool the jobs actually draw from.</summary>
        public int WorkingAge;

        public int Workers;
        public int Unemployed;

        /// <summary>Holding a job below their qualification. See CitizenCensusSystem.</summary>
        public int Under;

        /// <summary>Living here, working at an outside connection.</summary>
        public int Outside;

        /// <summary>Living outside, working here.</summary>
        public int Commuters;

        /// <summary>Posts that exist at this level, and how many of them nobody is doing.</summary>
        public int Jobs;
        public int Vacant;
    }

    /// <summary>
    /// The people half of the workforce table, one row per education level.
    ///
    /// Every column is counted rather than derived, so nothing here is an estimate. The jobs half
    /// - how many posts exist at each level and how many are filled - is not here: it comes from
    /// vanilla's own `workplaces.workplacesData` and `workplaces.employeesData` bindings, read in
    /// the UI, which is the exact source the game's Workplaces panel uses. Reading it anywhere
    /// else produced numbers that disagreed with the game.
    ///
    /// The columns are deliberately unambiguous. "Total" is everyone at that level; children,
    /// students and seniors are broken out so the working-age figure is visibly what is left,
    /// rather than something the reader has to trust.
    /// </summary>
    public sealed class WorkforceTable
    {
        private static readonly string[] LevelNames =
        {
            "Uneducated", "Poorly educated", "Educated", "Well educated", "Highly educated"
        };

        private readonly List<WorkforceRow> _rows = new List<WorkforceRow>();

        public IReadOnlyList<WorkforceRow> Rows
        {
            get { return _rows; }
        }

        public void Refresh(CitizenCensusSystem census)
        {
            _rows.Clear();

            if (census == null)
            {
                return;
            }

            census.Recount();

            for (var level = 0; level < CitizenCensusSystem.Levels; level++)
            {
                _rows.Add(new WorkforceRow
                {
                    Level = LevelNames[level],
                    Total = census.Get(level, CitizenCensusSystem.Field.Total),
                    Children = census.Get(level, CitizenCensusSystem.Field.Children)
                               + census.Get(level, CitizenCensusSystem.Field.Teens),
                    Students = census.Get(level, CitizenCensusSystem.Field.Students),
                    Seniors = census.Get(level, CitizenCensusSystem.Field.Seniors),
                    WorkingAge = census.Get(level, CitizenCensusSystem.Field.Adults),
                    Workers = census.Get(level, CitizenCensusSystem.Field.Workers),
                    Unemployed = census.Get(level, CitizenCensusSystem.Field.Unemployed),
                    Under = census.Get(level, CitizenCensusSystem.Field.Under),
                    Outside = census.Get(level, CitizenCensusSystem.Field.Outside),
                    Jobs = census.Get(level, CitizenCensusSystem.Field.Jobs),
                    Vacant = census.Get(level, CitizenCensusSystem.Field.Vacant),
                    Commuters = census.Get(level, CitizenCensusSystem.Field.Commuters)
                });
            }
        }
    }
}
