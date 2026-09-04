using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Rendering;
using Game.Simulation;
using Game.UI;
using Unity.Entities;

namespace Seety.Vitals
{
    /// <summary>One tradeable good, and how the city is doing at selling or making it.</summary>
    public sealed class ResourceEntry
    {
        /// <summary>The resource's own name, e.g. "Food". Not localised - the game does not localise these either.</summary>
        public string Name;

        /// <summary>
        /// How much the city wants this good relative to the good it wants most, 0-100 - the
        /// resource on top always reads 100. The raw figure behind it is an internal simulation
        /// weight with no unit a player can read (values in the millions, no ceiling), so what is
        /// shown is its share of the top entry rather than the number itself. See
        /// ResourceBreakdown.Normalize.
        /// </summary>
        public int Demand;

        /// <summary>Companies selling it (commercial) or making it (industrial).</summary>
        public int Companies;

        /// <summary>Companies of that trade with nowhere to operate from - they want a building.</summary>
        public int NoPremises;

        /// <summary>How stocked the shelves are, 0-100. Low with high demand is a supply problem.</summary>
        public float Stock;

        /// <summary>How staffed those companies are, 0-100.</summary>
        public float Staff;

        /// <summary>The Resource's own index, for the jumpToResource trigger. See EconomyUtils.GetResourceIndex.</summary>
        public int ResourceIndex;

        /// <summary>The company currently losing the most ground selling or making this, or empty. See CompanyPriority.</summary>
        public string PriorityName;

        /// <summary>The biggest cost behind that, or empty.</summary>
        public string PriorityReason;
    }

    /// <summary>
    /// The blue commercial bar, taken apart.
    ///
    /// Vanilla shows one number for "commercial demand" and one for industrial, which answers
    /// whether to zone but never what for. Underneath, the game already tracks all of this per
    /// resource - it simply never draws it.
    ///
    /// Everything here is public API on three simulation systems:
    ///
    ///   * CommercialDemandSystem.GetResourceDemands / IndustrialDemandSystem.GetResourceDemands
    ///   * CountCompanyDataSystem.GetCommercialCompanyDatas / GetIndustrialCompanyDatas
    ///
    /// All three are plain GameSystemBase on a 16-tick interval, so unlike the infoview bindings
    /// they run whether or not anything is on screen. There is no sleeping-binding trap here.
    ///
    /// The trap is a different one: those getters hand back a JobHandle along with the array,
    /// because the jobs that fill them may still be running. Reading without completing it first
    /// is a race, and in a development build the job safety system throws. Hence the Complete()
    /// before every read below.
    /// </summary>
    public sealed class ResourceBreakdown
    {
        private readonly List<ResourceEntry> _entries = new List<ResourceEntry>();
        private readonly CompanyPriority _priority = new CompanyPriority();

        public IReadOnlyList<ResourceEntry> Entries
        {
            get { return _entries; }
        }

        /// <summary>What the city's shops are selling, most wanted first.</summary>
        public void RefreshCommercial(CommercialDemandSystem demand, CountCompanyDataSystem companies,
            EntityQuery companyQuery, EntityManager entities, NameSystem names)
        {
            _entries.Clear();

            if (demand == null || companies == null)
            {
                return;
            }

            try
            {
                _priority.Scan(companyQuery, entities, names);

                Unity.Jobs.JobHandle demandDeps;
                var demands = demand.GetResourceDemands(out demandDeps);
                demandDeps.Complete();

                Unity.Jobs.JobHandle companyDeps;
                var data = companies.GetCommercialCompanyDatas(out companyDeps);
                companyDeps.Complete();

                var iterator = ResourceIterator.GetIterator();
                while (iterator.Next())
                {
                    var index = EconomyUtils.GetResourceIndex(iterator.resource);
                    if (index < 0)
                    {
                        continue;
                    }

                    Add(iterator.resource, index,
                        Read(demands, index),
                        Read(data.m_ServiceCompanies, index),
                        Read(data.m_ServicePropertyless, index),
                        Share(data.m_CurrentAvailables, data.m_TotalAvailables, index),
                        Share(data.m_CurrentServiceWorkers, data.m_MaxServiceWorkers, index));
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not read the commercial resource breakdown.");
            }

            Sort();
        }

        /// <summary>The same for industry, which makes the goods the shops above are selling.</summary>
        public void RefreshIndustrial(IndustrialDemandSystem demand, CountCompanyDataSystem companies,
            EntityQuery companyQuery, EntityManager entities, NameSystem names)
        {
            RefreshIndustrialOrOffice(demand, companies, companyQuery, entities, names, officeOnly: false);
        }

        /// <summary>
        /// Offices, which are not a separate kind of company in this data - EconomyUtils.
        /// IsOfficeResource(Resource) is the same single check IndustrialDemandSystem's own job
        /// uses to decide whether a resource counts toward officeCompanyDemand or
        /// industrialCompanyDemand. There is no separate system, no separate array, no separate
        /// company query: GetIndustrialResourceDemands and GetIndustrialCompanyDatas already carry
        /// every office resource mixed in with true industrial ones. This is the exact same read
        /// as RefreshIndustrial, filtered to the four resources that check calls office instead.
        /// </summary>
        public void RefreshOffice(IndustrialDemandSystem demand, CountCompanyDataSystem companies,
            EntityQuery companyQuery, EntityManager entities, NameSystem names)
        {
            RefreshIndustrialOrOffice(demand, companies, companyQuery, entities, names, officeOnly: true);
        }

        private void RefreshIndustrialOrOffice(IndustrialDemandSystem demand, CountCompanyDataSystem companies,
            EntityQuery companyQuery, EntityManager entities, NameSystem names, bool officeOnly)
        {
            _entries.Clear();

            if (demand == null || companies == null)
            {
                return;
            }

            try
            {
                _priority.Scan(companyQuery, entities, names);

                Unity.Jobs.JobHandle demandDeps;
                var demands = demand.GetIndustrialResourceDemands(out demandDeps);
                demandDeps.Complete();

                Unity.Jobs.JobHandle companyDeps;
                var data = companies.GetIndustrialCompanyDatas(out companyDeps);
                companyDeps.Complete();

                var iterator = ResourceIterator.GetIterator();
                while (iterator.Next())
                {
                    // The one line that tells industry and office apart. Everything else about
                    // this read is identical - same systems, same arrays, same company scan.
                    if (EconomyUtils.IsOfficeResource(iterator.resource) != officeOnly)
                    {
                        continue;
                    }

                    var index = EconomyUtils.GetResourceIndex(iterator.resource);
                    if (index < 0)
                    {
                        continue;
                    }

                    Add(iterator.resource, index,
                        Read(demands, index),
                        Read(data.m_ProductionCompanies, index),
                        Read(data.m_ProductionPropertyless, index),
                        // Industry has no shelves. What it has is production against the demand
                        // for it, which reads the same way: low means the city wants more of this
                        // than anyone is making.
                        Share(data.m_Production, data.m_Demand, index),
                        Share(data.m_CurrentProductionWorkers, data.m_MaxProductionWorkers, index));
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, officeOnly
                    ? "Could not read the office resource breakdown."
                    : "Could not read the industrial resource breakdown.");
            }

            Sort();
        }

        /// <summary>Moves the camera to the resource's worst-off company. False if there is none.</summary>
        public bool Jump(int resourceIndex, CameraUpdateSystem camera)
        {
            return _priority.Jump(resourceIndex, camera);
        }

        /// <summary>
        /// Adds a resource, unless the city has nothing to do with it.
        ///
        /// Every save carries the full resource list whether or not the city trades in it, so
        /// without this the table would be forty rows of zeroes with the six that matter buried
        /// somewhere inside.
        /// </summary>
        private void Add(Resource resource, int resourceIndex, int demand, int companies, int noPremises,
            float stock, float staff)
        {
            // Not a good. Money is a real bit in the Resource flags and carries a demand signal
            // like everything else, but no company sells or makes it - CountCompanyDataSystem
            // itself excludes it when matching what a company produces. Left in, it showed up as
            // a company count of zero next to a demand in the millions: noise with no action
            // behind it.
            if (resource == Resource.Money)
            {
                return;
            }

            if (demand <= 0 && companies <= 0 && noPremises <= 0)
            {
                return;
            }

            _entries.Add(new ResourceEntry
            {
                Name = NameOf(resource),
                Demand = demand,
                Companies = companies,
                NoPremises = noPremises,
                Stock = stock,
                Staff = staff,
                ResourceIndex = resourceIndex,
                PriorityName = _priority.NameFor(resourceIndex),
                PriorityReason = _priority.ReasonFor(resourceIndex)
            });
        }

        /// <summary>Most wanted first: the top of the list is what the city is asking for.</summary>
        private void Sort()
        {
            Normalize();

            _entries.Sort(delegate(ResourceEntry a, ResourceEntry b)
            {
                return b.Demand.CompareTo(a.Demand);
            });
        }

        /// <summary>
        /// Rescales Demand to a share of the top entry, 0-100. Monotonic, so it changes nothing
        /// about the order Sort produces - only what the number reads as.
        /// </summary>
        private void Normalize()
        {
            var max = 0;
            foreach (var entry in _entries)
            {
                if (entry.Demand > max)
                {
                    max = entry.Demand;
                }
            }

            if (max <= 0)
            {
                foreach (var entry in _entries)
                {
                    entry.Demand = 0;
                }

                return;
            }

            foreach (var entry in _entries)
            {
                var share = 100.0 * Math.Max(0, entry.Demand) / max;
                entry.Demand = (int)Math.Round(share, MidpointRounding.AwayFromZero);
            }
        }

        private static string NameOf(Resource resource)
        {
            try
            {
                var name = EconomyUtils.GetName(resource);
                return string.IsNullOrEmpty(name) ? resource.ToString() : name;
            }
            catch
            {
                return resource.ToString();
            }
        }

        /// <summary>
        /// Reads one slot, tolerating an array the game has not filled yet. These arrays are
        /// sized from the resource list, but a save loaded a moment ago can still hand back a
        /// default (unallocated) NativeArray.
        /// </summary>
        private static int Read(Unity.Collections.NativeArray<int> array, int index)
        {
            return array.IsCreated && index < array.Length ? array[index] : 0;
        }

        /// <summary>One count against another as a percentage, capped at 100.</summary>
        private static float Share(Unity.Collections.NativeArray<int> current,
            Unity.Collections.NativeArray<int> total, int index)
        {
            var max = Read(total, index);
            if (max <= 0)
            {
                return 0f;
            }

            var share = 100f * Read(current, index) / max;
            return share > 100f ? 100f : share;
        }
    }
}
