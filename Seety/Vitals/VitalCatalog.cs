using System.Collections.Generic;

namespace Seety.Vitals
{
    /// <summary>
    /// The vitals Seety knows how to show, and the vanilla infoview each one opens.
    ///
    /// Two rules decide what belongs here.
    ///
    /// 1. It must not already be on the vanilla HUD. Population, money and income sit in the
    ///    bottom bar at all times, so repeating them buys the player nothing and costs width.
    ///    They are not offered even as an option - an option to duplicate the HUD is still
    ///    duplication, just hidden behind a toggle.
    /// 2. It must have a live source. See <see cref="VitalReader"/>.
    ///
    /// The infoview names below are the real prefab names, read out of a running game on
    /// 2026-09-01 and recorded in DESIGN.md - not guesses. The lookup still takes a list per
    /// vital so a future game version that renames one can be handled by adding a candidate
    /// rather than by shipping a click that silently does nothing.
    ///
    /// Icon paths were checked against the files the game ships in
    /// Content\Game\UI\Media\Game. The strip falls back to the text label if one fails to load,
    /// so a wrong path degrades rather than leaving a hole.
    ///
    /// Problems comes first because it is the row you want to notice, and it has no infoview:
    /// there is no single vanilla panel that means "everything wrong at once", so it is honestly
    /// reported as non-clickable rather than sent somewhere arbitrary.
    /// </summary>
    public static class VitalCatalog
    {
        // Default thresholds. These are judgement calls, not values read out of the game, and
        // they are the reason the strip can act as an alert bar without a second list of alerts.
        // They are deliberately loose: a bar that cries wolf gets ignored.
        private static readonly VitalThreshold LowHappiness = new VitalThreshold(50f, 35f, true);
        private static readonly VitalThreshold LowHealth = new VitalThreshold(50f, 35f, true);
        private static readonly VitalThreshold HighUnemployment = new VitalThreshold(12f, 20f, false);
        private static readonly VitalThreshold HighHomelessness = new VitalThreshold(5f, 10f, false);

        // How full a container is: at 85% you should be building, at 100% you already needed to.
        private static readonly VitalThreshold HighUsage = new VitalThreshold(85f, 100f, false);

        // How much of a demand a service meets. Anything short of complete is worth noticing,
        // well short is urgent.
        private static readonly VitalThreshold LowCoverage = new VitalThreshold(99f, 90f, true);

        // Hazard scales, where the number is already a share of the game's own maximum.
        private static readonly VitalThreshold HighHazard = new VitalThreshold(50f, 75f, false);

        // Inverted rows need their own scales. Reusing LowCoverage made "Safety from crime 99%"
        // amber, because for a service anything under 100% is worth noticing - but 99% safe is a
        // quiet city, and a landfill with 50% room left is not an emergency either.
        private static readonly VitalThreshold LowSafety = new VitalThreshold(60f, 35f, true);
        private static readonly VitalThreshold LowSpace = new VitalThreshold(25f, 10f, true);

        // Traffic flow is the one where a low reading is the bad one: nothing is moving.
        private static readonly VitalThreshold LowFlow = new VitalThreshold(50f, 30f, true);

        public static IReadOnlyList<Vital> All()
        {
            return new List<Vital>
            {
                new Vital("problems",     VitalSource.Problems,     "Active problems",    "Problem",  "Media/Game/Icons/Notifications.svg",       Names(),              VitalFormat.Number),
                new Vital("happiness",    VitalSource.Happiness,    "Happiness and demographics", "Happy",    "Media/Game/Icons/Happy.svg",              Names("Happiness"),   VitalFormat.Percentage, LowHappiness),
                new Vital("health",       VitalSource.Health,       "Average health",     "Health",   "Media/Game/Icons/Healthcare.svg",         Names("Healthcare"),  VitalFormat.Percentage, LowHealth)
                    // "Coverage", not "how full the beds are": this is patientCapacity against
                    // sickCount, same Service()/Coverage shape as water or sewage below. A city
                    // with beds to spare reads near 100% here, same as vanilla's own Healthcare
                    // Availability bar does - "capacity" as a title read as occupancy instead and
                    // was reported as nonsensical for exactly that reason.
                    .With(Service("healthcare", "Hospital coverage", "Beds", "Icons/Healthcare.svg",
                        "Healthcare", "healthcareInfo", "patientCapacity", "sickCount")),
                new Vital("unemployment", VitalSource.Unemployment, "Unemployment",       "Jobless",  "Media/Game/Notifications/Unemployed.svg", Names("Workplaces"),  VitalFormat.Percentage, HighUnemployment, null, true, Game.City.StatisticType.Unemployed,    "Unemployed citizens"),
                new Vital("homelessness", VitalSource.Homelessness, "Homelessness",       "Homeless", "Media/Game/Icons/ConditionHomeless.svg",  Names("Residential"), VitalFormat.Percentage, HighHomelessness, null, true, Game.City.StatisticType.HomelessCount, "Homeless citizens"),
                new Vital("workers",      VitalSource.Workers,      "Workers",            "Work",     "Media/Game/Icons/Workers.svg",            Names("Workplaces"),  VitalFormat.Number, null, null, true, Game.City.StatisticType.WorkerCount,   "Workers"),
                new Vital("tourists",     VitalSource.Tourists,     "Tourists",           "Tourist",  "Media/Game/Icons/Tourist.svg",            Names("Tourism"),     VitalFormat.Number, null, null, true, Game.City.StatisticType.TouristCount,  "Tourists"),

                // Services, read as coverage: how much of the demand is actually met. A city with
                // no water pump reads 0%, not 100%.
                // Not production against consumption: that is a city-wide sum, so cutting the cable
                // to half the city leaves it reading 100% while those buildings sit dark. The
                // transmission indicator is demand actually delivered, which is the question.
                // The electricityInfo group publishes nine figures and vanilla's own panel draws
                // almost none of them. Three are worth a window: how much power is banked, and
                // whether the city is buying or selling it. Importing is a bill most players never
                // notice they are paying.
                new Vital("electricity", VitalSource.Vanilla, "Electricity delivered", "Power",
                    "Media/Game/Icons/Electricity.svg", Names("Electricity"),
                    VitalFormat.Percentage, LowCoverage,
                    new VanillaBinding("electricityInfo", string.Empty, "electricityTransmission",
                        VanillaKind.Indicator), false)
                    .With(Charge("battery", "Battery charge", "Battery",
                              "Media/Game/Notifications/BatteryEmpty.svg", "Electricity",
                              "electricityInfo", "batteryCharge"),
                          Plain("powerimport", "Electricity bought in", "In", "Icons/Import.svg",
                              "Electricity", "electricityInfo", "electricityImport", "power"),
                          Plain("powerexport", "Electricity sold out", "Out", "Icons/Export.svg",
                              "Electricity", "electricityInfo", "electricityExport", "power")),
                // Delivered, not produced. See VitalSource.WaterServed: capacity against demand is
                // a city-wide sum and stays healthy while a whole district runs dry.
                new Vital("water",  VitalSource.WaterServed,  "Water delivered",  "Water",
                    "Media/Game/Icons/Water.svg",  Names("WaterPipes"), VitalFormat.Percentage, LowCoverage)
                    // waterInfo publishes the trade figures too, and nothing draws them either.
                    .With(Plain("waterimport", "Water bought in", "In", "Icons/Import.svg",
                              "WaterPipes", "waterInfo", "waterImport"),
                          Plain("waterexport", "Water sold out", "Out", "Icons/Export.svg",
                              "WaterPipes", "waterInfo", "waterExport")),
                new Vital("sewage", VitalSource.SewageServed, "Sewage taken away", "Sewage",
                    "Media/Game/Icons/Sewage.svg", Names("WaterPipes"), VitalFormat.Percentage, LowCoverage)
                    .With(Plain("sewageexport", "Sewage sent away", "Out", "Icons/Export.svg",
                              "WaterPipes", "waterInfo", "sewageExport")),

                // Deathcare, which the city notices only when it stops working. Same Coverage
                // shape and the same renaming as healthcare above - "coverage" of the current
                // death rate, not how full the crematorium is.
                Service("deathcare",   "Crematorium coverage", "Cremate", "Icons/Deathcare.svg",      "Healthcare",  "healthcareInfo",  "processingRate",        "deathRate"),
                Container("cemetery",  "Cemetery space",       "Graves",  "Media/Game/Notifications/HearseServiceNeeded.svg", "Healthcare", "healthcareInfo", "cemeteryCapacity",   "cemeteryUse"),
                // Schools are containers, not coverage. Capacity normally exceeds the eligible
                // intake, so coverage capped every level at 100% and every bar came out full -
                // the exact opposite of vanilla's own panel, where the four differ. How full the
                // schools are is the figure that actually moves.
                //
                // One icon for all four, with the school level as a small numeral over it.
                //
                // Four unrelated icons read as four unrelated things; the game's own education
                // icons cannot help either, since PoorlyEducated.svg and Educated.svg are shipped
                // as byte-identical files. A single family plus a rank says both halves at once:
                // these belong together, and this is the first of them.
                Container("elementary", "Elementary school places", "School",  "Media/Game/Icons/Education.svg", "Education", "educationInfo", "elementaryCapacity", "elementaryEligible", "1"),
                Container("highschool", "High school places",      "High",    "Media/Game/Icons/Education.svg", "Education", "educationInfo", "highSchoolCapacity", "highSchoolEligible", "2"),
                Container("college",    "College places",          "College", "Media/Game/Icons/Education.svg", "Education", "educationInfo", "collegeCapacity",    "collegeEligible",    "3"),
                Container("university", "University places",       "Uni",     "Media/Game/Icons/Education.svg", "Education", "educationInfo", "universityCapacity", "universityEligible", "4"),

                // Rubbish twice over, kept adjacent: how fast it is processed, and how full the
                // hole is. One without the other tells you half the story.
                // Parking, read as room left rather than cars parked. The dropdown splits it into
                // cars and bikes, which vanilla reports through two different panels.
                new Vital("parking", VitalSource.Vanilla, "Free parking", "Park",
                    "Media/Game/Icons/Parking.svg", Names("Roads"), VitalFormat.Percentage, LowSpace,
                    new VanillaBinding("roadsInfo", "parkingCapacity", "parkedCars", VanillaKind.Ratio),
                    false, null, null, null, true),

                Service("garbage",   "Garbage processing", "Waste", "Icons/Garbage.svg",                  "Garbage", "garbageInfo", "processingRate", "productionRate")
                    .With(Container("landfill", "Landfill space", "Dump", "Media/Game/Icons/WasteRecycling.svg",
                        "Garbage", "garbageInfo", "capacity", "storedGarbage")),

                // Hazards: the binding is already a share of the game's own maximum.
                Hazard("crimeprob", "Safety from crime", "Safe", "Icons/Police.svg", "Police",     "policeInfo",        "averageCrimeProbability")
                    .With(new Vital("detention", VitalSource.Vanilla, "Free jail and prison cells", "Cells",
                        "Media/Game/Notifications/CrimeScene.svg", Names("Police"), VitalFormat.Percentage, LowSpace,
                        new VanillaBinding("policeInfo", "jailCapacity", "inJail", VanillaKind.Ratio,
                            "prisonCapacity", "inPrison"), false, null, null, null, true)),


                // The four pollutions as one row, averaged, with the detail behind it. Four cells
                // that usually move together was four times the width for one idea.
                new Vital("pollution", VitalSource.Vanilla, "Environment quality", "Clean",
                    "Media/Game/Icons/Pollution.svg", Names("AirPollution"),
                    VitalFormat.Percentage, LowSafety,
                    new VanillaBinding("pollutionInfo", string.Empty, "averageAirPollution",
                        VanillaKind.PollutionGroup), false, null, null, null, true),

                Hazard("fire",      "Fire safety",       "Fire",  "Icons/FireSafety.svg",         "FireRescue", "fireAndRescueInfo", "averageFireHazard"),

                // Reported by vanilla as headroom rather than as two numbers.
                // Post as a proper coverage row. Vanilla derives postServiceAvailability from
                // these same three bindings - Calculate(delivered + collected, production) - so
                // reading them directly gives the same judgement in a scale players can read,
                // instead of headroom running from -1 to +1.
                //
                // There is deliberately no telecom row. TelecomInfoviewUISystem creates the
                // networkAvailability binding and its PerformUpdate() is empty, so the value never
                // leaves its default of zero. The row read "Signal 0%" in a city with full
                // coverage because there is no number there to read.
                new Vital("post", VitalSource.Vanilla, "Postal service", "Mail",
                    "Media/Game/Icons/PostService.svg", Names("PostService"),
                    VitalFormat.Percentage, LowCoverage,
                    new VanillaBinding("postInfo", "deliveredMail", "mailProductionRate",
                        VanillaKind.Coverage, "collectedMail"), false),

                Flow("traffic",     "Traffic flow",        "Flow",    "Icons/Traffic.svg",     "Traffic",   "trafficInfo",   "trafficFlow"),
                Plain("landvalue",  "Average land value",  "Land",    "Icons/LandValue.svg",   "LandValue", "landValueInfo", "averageLandValue"),

                // Demand as one row. The six figures live behind it - see VanillaKind.DemandGroup.
                new Vital("demand", VitalSource.Vanilla, "Zone demand", "Demand",
                    "Media/Game/Icons/Zones.svg", Names("Residential"), VitalFormat.Percentage, null,
                    new VanillaBinding("cityInfo", string.Empty, "residentialLowDemand",
                        VanillaKind.DemandGroup), false),

                // Passengers and cargo: one row, with every mode behind it. Read C#-side from the
                // same statistics vanilla's own transport summaries use.
                new Vital("transport", VitalSource.Transport, "Transport passengers", "Transit",
                    "Media/Game/Icons/Transportation.svg", Names("Transport"), VitalFormat.Number, null, null, false)
            };
        }

        /// <summary>
        /// A service that provides for a demand, shown as how much of that demand is met. Low is
        /// the bad direction: 0% means nothing is served, not that nothing is needed.
        /// </summary>
        private static Vital Service(string id, string title, string label, string icon, string infoview,
            string group, string supply, string demand)
        {
            return new Vital(id, VitalSource.Vanilla, title, label, "Media/Game/" + icon,
                Names(infoview), VitalFormat.Percentage, LowCoverage,
                new VanillaBinding(group, supply, demand, VanillaKind.Coverage), false);
        }

        /// <summary>A container that fills up, shown as how full. High is the bad direction.</summary>
        private static Vital Container(string id, string title, string label, string icon, string infoview,
            string group, string capacity, string stored, string badge = null)
        {
            // Inverted: the game reports how full, the strip shows the room left.
            return new Vital(id, VitalSource.Vanilla, title, label, icon,
                Names(infoview), VitalFormat.Percentage, LowSpace,
                new VanillaBinding(group, capacity, stored, VanillaKind.Ratio), false, null, null, badge, true);
        }

        /// <summary>A risk figure vanilla already expresses as a share of its own maximum.</summary>
        private static Vital Hazard(string id, string title, string label, string icon, string infoview,
            string group, string binding)
        {
            // Inverted: the game reports a hazard, the strip shows the safety left. See Vital.Invert.
            return new Vital(id, VitalSource.Vanilla, title, label, "Media/Game/" + icon,
                Names(infoview), VitalFormat.Percentage, LowSafety,
                new VanillaBinding(group, string.Empty, binding, VanillaKind.Indicator), false,
                null, null, null, true);
        }

        /// <summary>
        /// An IndicatorValue read as a level on its own scale, with no threshold.
        ///
        /// No threshold on purpose. A city with no batteries at all has max 0 and therefore reads
        /// 0%, which is honest - there is no stored power - but it is not a fault, and painting it
        /// red would be the same cry-wolf mistake the water row taught: see VanillaKind.Coverage.
        /// </summary>
        private static Vital Charge(string id, string title, string label, string icon, string infoview,
            string group, string binding)
        {
            return new Vital(id, VitalSource.Vanilla, title, label, icon,
                Names(infoview), VitalFormat.Percentage, null,
                new VanillaBinding(group, string.Empty, binding, VanillaKind.Indicator), false);
        }

        /// <summary>A plain number with no denominator and no threshold. Purely informational.</summary>
        private static Vital Plain(string id, string title, string label, string icon, string infoview,
            string group, string binding, string unit = null)
        {
            return new Vital(id, VitalSource.Vanilla, title, label, "Media/Game/" + icon,
                Names(infoview), VitalFormat.Number, null,
                new VanillaBinding(group, string.Empty, binding, VanillaKind.Scalar), false,
                null, null, null, false, null, unit);
        }

        /// <summary>Traffic flow, where a LOW number is the bad one: nothing is moving.</summary>
        private static Vital Flow(string id, string title, string label, string icon, string infoview,
            string group, string binding)
        {
            return new Vital(id, VitalSource.Vanilla, title, label, "Media/Game/" + icon,
                Names(infoview), VitalFormat.Percentage, LowFlow,
                new VanillaBinding(group, string.Empty, binding, VanillaKind.FlowArray), false);
        }


        private static string[] Names(params string[] names)
        {
            return names;
        }

        /// <summary>Look one up by its stable id, or null.</summary>
        public static Vital Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var vital in All())
            {
                if (vital.Id == id)
                {
                    return vital;
                }

                // Companions are not on the bar but are still clicked, so they still have to be
                // findable - otherwise every reading folded into a window would open nothing.
                foreach (var companion in vital.Companions)
                {
                    if (companion.Id == id)
                    {
                        return companion;
                    }
                }
            }

            return null;
        }
    }
}
