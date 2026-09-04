using System;
using Game.Buildings;
using Game.Companies;
using Game.Economy;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Seety.Vitals
{
    /// <summary>
    /// Which single company of a resource is worst off, and roughly why.
    ///
    /// "Worst off" is not "has an active notification". A business can lose money for a long time
    /// before, or without ever, tripping one - a distant supply, a route nobody widened, rent it
    /// cannot cover. Ranking instead by <see cref="Profitability"/>.m_Profitability, the game's own
    /// running measure of whether a company's worth is rising or falling (127 flat, lower losing
    /// ground), catches that quiet case without losing the acute one: a company with no customers
    /// or no inputs loses worth too, so it still sorts to the top.
    ///
    /// One pass over every company answers this for every resource at once, rather than one pass
    /// per resource. Each company's prefab carries IndustrialProcessData.m_Output.m_Resource - what
    /// it sells (commercial) or makes (industrial) - the exact field CountCompanyDataSystem's own
    /// job reads to bucket companies by resource; this keeps a running worst per resource index as
    /// the same single walk goes.
    /// </summary>
    public sealed class CompanyPriority
    {
        private string[] _names = Array.Empty<string>();
        private string[] _reasons = Array.Empty<string>();
        private float3[] _positions = Array.Empty<float3>();
        private bool[] _found = Array.Empty<bool>();

        /// <summary>The company currently worst off for this resource, or empty if none trade in it.</summary>
        public string NameFor(int resourceIndex)
        {
            return HasCandidate(resourceIndex) ? _names[resourceIndex] : string.Empty;
        }

        /// <summary>The biggest cost eating into that company, or empty.</summary>
        public string ReasonFor(int resourceIndex)
        {
            return HasCandidate(resourceIndex) ? _reasons[resourceIndex] : string.Empty;
        }

        private bool HasCandidate(int resourceIndex)
        {
            return resourceIndex >= 0 && resourceIndex < _found.Length && _found[resourceIndex];
        }

        /// <summary>Re-walks every company in the query, finding the worst one per resource.</summary>
        public void Scan(EntityQuery companies, EntityManager entities, NameSystem names)
        {
            var count = EconomyUtils.ResourceCount;

            var worstCompany = new Entity[count];
            var worstBuilding = new Entity[count];
            var worstProfitability = new byte[count];
            var found = new bool[count];

            for (var i = 0; i < count; i++)
            {
                worstProfitability[i] = byte.MaxValue;
            }

            if (!companies.IsEmptyIgnoreFilter)
            {
                using (var list = companies.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < list.Length; i++)
                    {
                        Scan(list[i], entities, count, worstCompany, worstBuilding, worstProfitability, found);
                    }
                }
            }

            _names = new string[count];
            _reasons = new string[count];
            _positions = new float3[count];
            _found = found;

            for (var i = 0; i < count; i++)
            {
                if (!found[i])
                {
                    continue;
                }

                _names[i] = SafeName(names, entities, worstCompany[i]);
                _reasons[i] = BuildReason(entities, worstCompany[i]);
                _positions[i] = entities.GetComponentData<Transform>(worstBuilding[i]).m_Position;
            }
        }

        private static void Scan(Entity company, EntityManager entities, int count,
            Entity[] worstCompany, Entity[] worstBuilding, byte[] worstProfitability, bool[] found)
        {
            if (!entities.HasComponent<PrefabRef>(company))
            {
                return;
            }

            var prefab = entities.GetComponentData<PrefabRef>(company).m_Prefab;
            if (!entities.HasComponent<IndustrialProcessData>(prefab))
            {
                return;
            }

            var resource = entities.GetComponentData<IndustrialProcessData>(prefab).m_Output.m_Resource;
            if (resource == Resource.NoResource)
            {
                return;
            }

            var index = EconomyUtils.GetResourceIndex(resource);
            if (index < 0 || index >= count)
            {
                return;
            }

            if (!entities.HasComponent<Profitability>(company))
            {
                return;
            }

            // Excludes propertyless companies - the ones already counted separately as
            // "homeless" in the resource table. A company waiting for premises has nowhere to
            // jump to: without this check it could still win a resource's worst slot, and the
            // camera landed at Unity's coordinate zero when it did - open water or bare terrain,
            // wherever that happens to sit on the current map, with nothing there to see.
            if (!entities.HasComponent<PropertyRenter>(company))
            {
                return;
            }

            var building = entities.GetComponentData<PropertyRenter>(company).m_Property;
            if (building == Entity.Null || !entities.HasComponent<Transform>(building))
            {
                return;
            }

            var profitability = entities.GetComponentData<Profitability>(company).m_Profitability;
            if (found[index] && profitability >= worstProfitability[index])
            {
                return;
            }

            worstCompany[index] = company;
            worstBuilding[index] = building;
            worstProfitability[index] = profitability;
            found[index] = true;
        }

        /// <summary>
        /// The largest of the company's own cost categories, as a share of the sum of all of
        /// them, plus what it pays for its own raw materials where that price is tracked
        /// separately. Reads Game.Companies.CompanyStatisticData - a real per-company ledger, not
        /// a verdict of ours - and names the biggest line in it. The player draws the conclusion.
        /// </summary>
        private static string BuildReason(EntityManager entities, Entity company)
        {
            if (!entities.HasComponent<CompanyStatisticData>(company))
            {
                return string.Empty;
            }

            var stats = entities.GetComponentData<CompanyStatisticData>(company);

            var best = 0;
            var label = string.Empty;

            Consider(stats.m_RentPaid, "rent", ref best, ref label);
            Consider(stats.m_CostBuyResource, "materials", ref best, ref label);
            Consider(stats.m_WagePaid, "wages", ref best, ref label);
            Consider(stats.m_ElectricityPaid, "electricity", ref best, ref label);
            Consider(stats.m_WaterPaid + stats.m_SewagePaid, "water and sewage", ref best, ref label);
            Consider(stats.m_TaxPaid, "tax", ref best, ref label);
            Consider(stats.m_GarbagePaid, "garbage collection", ref best, ref label);

            if (string.IsNullOrEmpty(label))
            {
                return string.Empty;
            }

            var total = stats.m_RentPaid + stats.m_CostBuyResource + stats.m_WagePaid + stats.m_ElectricityPaid
                + stats.m_WaterPaid + stats.m_SewagePaid + stats.m_TaxPaid + stats.m_GarbagePaid;

            var share = total > 0 ? 100 * best / total : 0;

            return "biggest cost: " + label + " (" + share + "% of spending)" + SupplyCost(entities, company);
        }

        /// <summary>
        /// What this company pays to source its own raw materials, if that price is tracked.
        ///
        /// Not the resource this row is about - that is what the company sells or makes, and
        /// nobody buys their own output. Its inputs come from the same
        /// IndustrialProcessData.m_Input1/m_Input2 that decide what shows in "materials" above;
        /// Game.Companies.TradeCost is a buffer, one entry per resource a company actually
        /// trades, carrying the game's own per-unit price rather than a distance Seety works out
        /// itself. Shown as a plain fact, not a verdict - "high" only means something next to
        /// what other rows in the same list are paying.
        /// </summary>
        private static string SupplyCost(EntityManager entities, Entity company)
        {
            if (!entities.HasComponent<PrefabRef>(company))
            {
                return string.Empty;
            }

            var prefab = entities.GetComponentData<PrefabRef>(company).m_Prefab;
            if (!entities.HasComponent<IndustrialProcessData>(prefab) || !entities.HasBuffer<TradeCost>(company))
            {
                return string.Empty;
            }

            var process = entities.GetComponentData<IndustrialProcessData>(prefab);
            var costs = entities.GetBuffer<TradeCost>(company, true);

            var best = 0f;
            var name = string.Empty;

            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i];
                if (cost.m_BuyCost <= best)
                {
                    continue;
                }

                if (cost.m_Resource == process.m_Input1.m_Resource || cost.m_Resource == process.m_Input2.m_Resource)
                {
                    best = cost.m_BuyCost;
                    name = NameOf(cost.m_Resource);
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            return " - pays " + (int)Math.Round(best) + " to source each unit of " + name;
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

        private static void Consider(int value, string label, ref int best, ref string bestLabel)
        {
            if (value > best)
            {
                best = value;
                bestLabel = label;
            }
        }

        /// <summary>
        /// A company's own display name, resolved the way the game actually names one.
        ///
        /// NameSystem.GetRenderedLabelName(company) is the wrong entity to hand it: a company's
        /// readable name lives on its brand (Game.Companies.CompanyData.m_Brand), a separate
        /// prefab entity carrying the real Localization id - NameSystem.GetName has an explicit
        /// branch for this, but that method returns a Name struct meant for the client to
        /// localise, not a ready string. Passing the brand entity into GetRenderedLabelName
        /// instead reuses that same method's own dictionary lookup correctly, since its internal
        /// id resolution already special-cases an entity carrying BrandData. Without this it
        /// returned the untranslated lookup key verbatim - "Assets.NAME[Commercial_FoodStore]"
        /// rather than the shop's actual name.
        /// </summary>
        private static string SafeName(NameSystem names, EntityManager entities, Entity company)
        {
            try
            {
                var target = company;

                if (entities.HasComponent<CompanyData>(company))
                {
                    var brand = entities.GetComponentData<CompanyData>(company).m_Brand;
                    if (brand != Entity.Null)
                    {
                        target = brand;
                    }
                }

                var label = names.GetRenderedLabelName(target);
                return string.IsNullOrEmpty(label) ? "Company" : label;
            }
            catch
            {
                return "Company";
            }
        }

        /// <summary>Moves the camera to the resource's worst-off company. False if there is none.</summary>
        public bool Jump(int resourceIndex, CameraUpdateSystem camera)
        {
            if (!HasCandidate(resourceIndex))
            {
                return false;
            }

            if (camera == null || camera.activeCameraController == null)
            {
                return false;
            }

            var target = _positions[resourceIndex];
            camera.activeCameraController.pivot = new UnityEngine.Vector3(target.x, target.y, target.z);
            return true;
        }
    }
}
