import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import { useLocalization } from "cs2/l10n";
import styles from "./vitals-strip.module.scss";
import { clampPosition } from "./position";

// The game declares UnitSystem in its types but does not export it from cs2/l10n at runtime.
// Match its serialized option value (Metric = 0, Freedom = 1) without importing the enum.
const METRIC_UNIT_SYSTEM = 0;

/** Mirrors the record written by SeetyUISystem.WriteVitals. Keep the two in step. */
interface Vital {
  id: string;
  /** Full name, shown on hover. With icons instead of text, this is what names the row. */
  title: string;
  label: string;
  /** Where to look `title` and `label` up in the player's language. See useT. */
  titleKey: string;
  labelKey: string;
  /** Vanilla icon, relative to the GameUI root. Empty means "use the label". */
  icon: string;
  /** A short rank drawn over the icon, or empty. The four school tiers share one icon. */
  badge: string;
  value: number;
  format: VitalFormat;
  level: VitalLevel;
  clickable: boolean;

  /** Non-empty when the number comes from one of vanilla's own bindings. See VanillaBinding. */
  bindGroup: string;
  bindSupply: string;
  bindDemand: string;
  /** Optional second pair, added to the first before the ratio is taken. */
  bindSupply2: string;
  bindDemand2: string;
  /** Must match Seety.Vitals.VanillaKind. */
  bindKind: VanillaKind;

  warning: number;
  critical: number;
  lowIsBad: boolean;
  hasThreshold: boolean;

  /** True when this row has a recorded series to chart. */
  hasHistory: boolean;
  /** Show 100 minus the reading, so a full green bar always means good. See Vital.Invert. */
  invert: boolean;
  /** A cityInfo binding carrying the reasons behind this figure, or empty. See Vital.Factors. */
  factors: string;
  /** The game unit this reading is in, or empty for a plain count. See formatUnit. */
  unit: string;
  /** Whether the player has this row switched on. Only meaningful in configuration mode. */
  enabled: boolean;

  /**
   * Readings folded into this row's window rather than given their own square on the bar.
   * Always empty on a companion itself - see Vital.Companions.
   */
  companions: Vital[];
}

/** Must match Seety.Vitals.VanillaKind on the C# side. */
const enum VanillaKind {
  Ratio = 0,
  Coverage = 1,
  Indicator = 2,
  Scalar = 3,
  FlowArray = 4,
  Fraction = 5,
  DemandGroup = 6,
  PollutionGroup = 7,
}

/** The four pollution readings behind the Environment row, with the panel each one opens. */
const POLLUTIONS: { binding: string; label: string; icon: string; infoview: string }[] = [
  { binding: "averageAirPollution",    label: "Air",   icon: "Media/Game/Icons/AirPollution.svg",    infoview: "AirPollution" },
  { binding: "averageGroundPollution", label: "Soil",  icon: "Media/Game/Icons/GroundPollution.svg", infoview: "GroundPollution" },
  { binding: "averageNoisePollution",  label: "Noise", icon: "Media/Game/Icons/NoisePollution.svg",  infoview: "NoisePollution" },
  { binding: "averageWaterPollution",  label: "Water", icon: "Media/Game/Icons/WaterPollution.svg",  infoview: "WaterPollution" },
];

const pollution$ = POLLUTIONS.map((x) =>
  bindValue<IndicatorValue | null>("pollutionInfo", x.binding, null)
);

/** An indicator on its own 0..max scale, as a percentage. */
function levelOf(v: IndicatorValue | null): number {
  return v && v.max > 0 ? (v.current / v.max) * 100 : 0;
}

/** The average of the four, which is what the strip shows before inversion. */
function usePollutionAverage(): number {
  const values = pollution$.map((b) => levelOf(useValue(b)));
  return values.reduce((a, b) => a + b, 0) / values.length;
}

/** The row whose window splits the environment into its four readings. */
const POLLUTION_ID = "pollution";

/** The six zone demands, and the factor list behind each. Matches VanillaKind.DemandGroup. */
const DEMANDS: { id: string; key: string; label: string; icon: string; factors: string }[] = [
  { id: "residentialLowDemand",    key: "Seety.ZONE_RES_LOW",    label: "Residential low",    icon: "Media/Game/Icons/ZoneResidentialLow.svg",    factors: "residentialLowFactors" },
  { id: "residentialMediumDemand", key: "Seety.ZONE_RES_MED",    label: "Residential medium", icon: "Media/Game/Icons/ZoneResidentialMedium.svg", factors: "residentialMediumFactors" },
  { id: "residentialHighDemand",   key: "Seety.ZONE_RES_HIGH",   label: "Residential high",   icon: "Media/Game/Icons/ZoneResidentialHigh.svg",   factors: "residentialHighFactors" },
  { id: "commercialDemand",        key: "Seety.ZONE_COMMERCIAL", label: "Commercial",         icon: "Media/Game/Icons/ZoneCommercial.svg",        factors: "commercialFactors" },
  { id: "industrialDemand",        key: "Seety.ZONE_INDUSTRIAL", label: "Industrial",         icon: "Media/Game/Icons/ZoneIndustrial.svg",        factors: "industrialFactors" },
  { id: "officeDemand",            key: "Seety.ZONE_OFFICE",     label: "Office",             icon: "Media/Game/Icons/ZoneOffice.svg",            factors: "officeFactors" },
];

const demand$ = DEMANDS.map((d) => bindValue<number>("cityInfo", d.id, 0));

/** The highest of the six, which is the one glance worth having on the strip. */
function useDemandPeak(): number {
  const values = demand$.map((b) => useValue(b));
  return Math.max(0, ...values) * 100;
}

/** The zero a binding falls back to when a vital does not use that slot. */
const NO_BINDING = "";

/** Vanilla's {min, max, current} shape, used by every *Availability and hazard binding. */
interface IndicatorValue {
  min: number;
  max: number;
  current: number;
}

/** Must match Seety.Vitals.VitalFormat on the C# side. */
const enum VitalFormat {
  Number = 0,
  Percentage = 1,
}

/** Must match Seety.Vitals.VitalLevel on the C# side. */
const enum VitalLevel {
  Normal = 0,
  Warning = 1,
  Critical = 2,
}

/** One row inside an expandable list, as written by SeetyUISystem.WriteRow. */
interface BreakdownRow {
  id: string;
  icon: string;
  count: number;
  level: VitalLevel;
  clickable: boolean;
  /** Appended to the count. "%" for the school list, empty for a plain tally. */
  suffix: string;
  /** "jump", "passenger:X", "cargo:X", "school:N", or empty. See SeetyUISystem.WriteRow. */
  action: string;
  /** A denominator, when the row has one. Zero when it does not. */
  total: number;
  /** The game unit `count` and `total` are in, or empty for a plain tally. See formatUnit. */
  unit: string;
}

/** The rows behind one expandable vital, keyed by that vital's id. */
interface Breakdown {
  id: string;
  rows: BreakdownRow[];
}

/** One education level, counted citizen by citizen. See CitizenCensusSystem. */
interface WorkforceRow {
  level: string;
  total: number;
  children: number;
  students: number;
  /** The part of `students` that is a child or a teenager. See CitizenCensusSystem. */
  childStudents: number;
  seniors: number;
  workingAge: number;
  workers: number;
  unemployed: number;
  under: number;
  outside: number;
  commuters: number;
  jobs: number;
  vacant: number;
}

interface Workforce {
  rows: WorkforceRow[];
}



/** The recorded series behind whichever row is open. Empty when that row has none. */
interface History {
  /** What the series counts. Not necessarily what the row above shows - see Vital.History. */
  label: string;
  values: number[];
}

/**
 * Looks a string up in the player's language, falling back to the English written at the call
 * site.
 *
 * Every call passes that fallback on purpose: a key missing from one of the twelve tables then
 * shows that one string in English instead of leaving a blank cell, which is the difference
 * between a mod that looks untranslated in one place and one that looks broken.
 */
function useT(): (key: string, english: string) => string {
  const { translate } = useLocalization();
  return (key, english) => translate(key, english) ?? english;
}

const vitals$ = bindValue<Vital[]>("seety", "vitals", []);
const breakdowns$ = bindValue<Breakdown[]>("seety", "notifications", []);
const history$ = bindValue<History>("seety", "history", { label: "", values: [] });
const workforce$ = bindValue<Workforce>("seety", "workforce", { rows: [] });
const visible$ = bindValue<boolean>("seety", "visible", true);
const posX$ = bindValue<number>("seety", "posX", 10);
const posY$ = bindValue<number>("seety", "posY", 90);
const iconsHidden$ = bindValue<boolean>("seety", "iconsHidden", false);
const configMode$ = bindValue<boolean>("seety", "configMode", false);
const iconOutline$ = bindValue<boolean>("seety", "iconOutline", true);

/** One age band, split across the five education levels. */
interface AgeRow {
  age: string;
  /** Where to look `age` up in the player's language. Sent by C#, never assembled here. */
  ageKey: string;
  levels: number[];
}

const demographics$ = bindValue<AgeRow[]>("seety", "demographics", []);

/** One good, and how the city is doing at selling or making it. See ResourceEntry. */
interface ResourceRow {
  name: string;
  /** Demand relative to the good the city wants most, 0-100. See ResourceBreakdown.Normalize. */
  demand: number;
  companies: number;
  /** Companies of that trade with nowhere to operate from. */
  noPremises: number;
  /** Shelf stock (commercial) or production against demand (industrial), 0-100. */
  stock: number;
  staff: number;
  /** For the jumpToResource trigger. See EconomyUtils.GetResourceIndex. */
  resourceIndex: number;
  /** The company currently losing the most ground on this resource, or empty. See CompanyPriority. */
  priorityName: string;
  /** The biggest cost behind that, or empty. */
  priorityReason: string;
}

const resources$ = bindValue<{
  commercial: ResourceRow[];
  industrial: ResourceRow[];
  office: ResourceRow[];
}>("seety", "resources", { commercial: [], industrial: [], office: [] });

/** The five education levels, shortened to fit a column head. */
const LEVEL_NAMES = [
  { key: "Seety.EDU_NONE", english: "None" },
  { key: "Seety.EDU_POOR", english: "Poor" },
  { key: "Seety.EDU_EDUCATED", english: "Educated" },
  { key: "Seety.EDU_WELL", english: "Well" },
  { key: "Seety.EDU_HIGHLY", english: "Highly" },
];

/** The row whose window carries the demographics table. Matches the C# side. */
const DEMOGRAPHICS_ID = "happiness";

/** The four school rows. Their windows list the schools of that level. */
const SCHOOL_IDS = ["elementary", "highschool", "college", "university"];

/** The row whose window splits parking into cars and bikes. */
const PARKING_ID = "parking";

const parkingCapacity$ = bindValue<number>("roadsInfo", "parkingCapacity", 0);
const parkedCars$ = bindValue<number>("roadsInfo", "parkedCars", 0);

/**
 * Raw bike parking counts. bikesInfo also exposes bikeParkingAvailability as a pre-reduced
 * IndicatorValue with no numbers behind it - this is the sibling binding vanilla computes the
 * same job's results into, {x: parked, y: capacity}, so bikes can be read the same way cars are
 * instead of as a bare percentage.
 */
const bikeParking$ = bindValue<{ x: number; y: number }>("bikesInfo", "bikeParking", { x: 0, y: 0 });

/** One reason behind a demand figure, as vanilla writes it. */
interface Factor {
  factor: string;
  weight: number;
}

/** Stand-in so useValue always has something to subscribe to. See useVanillaValue. */
const ZERO$ = bindValue<number>("seety", "posX", 0);

/**
 * How far the pointer must travel before a press counts as a drag rather than a click.
 * Without this, every click on an entry would also nudge the strip by a pixel or two.
 */
const DRAG_THRESHOLD = 4;

/**
 * The strip snaps to a multiple of this many rem while being dragged, so it is easy to land it
 * back on the exact same spot, or roughly level with another anchored panel, rather than
 * fighting for a pixel-perfect drop. Not tied to the vanilla toolbar's own button spacing -
 * nothing here reads that - just a plain, even grid.
 */
const DRAG_GRID = 8;

function snapToGrid(value: number): number {
  return Math.round(value / DRAG_GRID) * DRAG_GRID;
}

/**
 * The top edge of the vanilla HUD's own top-left row, in rem.
 *
 * From the game's compiled stylesheet, where its top layout is
 * `position: absolute; top: 10rem; left: 10rem; right: 10rem`. Read from the game rather than
 * eyeballed, but it is the container's edge, not necessarily the buttons' - if they sit inside
 * padding of its own, this needs to be whatever lines up in game. One constant, one place.
 *
 * Like the icon paths, this is a value the game owns and could move in an update. Wrong, it costs
 * a slightly-off magnet, not a broken bar.
 */
const HUD_TOP_Y = 10;

/**
 * How near that line the bar has to be dragged before it locks onto it, in rem.
 *
 * Wider than it sounds: the ordinary grid is 8rem, so anything much larger than this would swallow
 * the two gridlines either side and make the top of the screen feel sticky.
 */
const HUD_SNAP_PULL = 5;

/**
 * Vertical snapping: the plain grid everywhere, except near the vanilla HUD row, where the bar
 * locks flush to it.
 *
 * The grid alone could not reach that line at all - 10 is not a multiple of 8 - so lining the bar
 * up with the game's own buttons was impossible by hand, which is the entire reason this exists.
 * Deliberately one extra target rather than a general magnetic grid: dragging keeps behaving
 * exactly as it did everywhere else, and only the one alignment worth having is made reachable.
 */
function snapY(value: number): number {
  if (Math.abs(value - HUD_TOP_Y) <= HUD_SNAP_PULL) {
    return HUD_TOP_Y;
  }

  return snapToGrid(value);
}

/**
 * How many real pixels one rem is worth right now.
 *
 * Everything here is positioned in rem, but a pointer event is in pixels, and the two are only
 * the same number at 1080p. The game's own stylesheet sets `html { font-size: 0.0925926vh }` -
 * one rem is the viewport height over 1080 - with a `@media (min-height: 56.25vw)` switching it
 * to `vw / 1920` above 16:9. Mixing the two units made the strip run away from the cursor.
 *
 * MEASURED off the layout, not read from getComputedStyle. That was the first attempt and it made
 * the drift worse rather than fixing it: Gameface's getComputedStyle is not the browser's, and
 * against a font-size given in vh it hands back the specified string rather than the resolved
 * pixel value - so parseFloat saw 0.0925926 and the strip flew roughly eleven times further than
 * the pointer. A hidden 100rem box put through getBoundingClientRect is plain layout, which this
 * renderer does do correctly; offsetWidth is already trusted a few lines below for the same
 * reason.
 *
 * Read once when the drag starts - the resolution cannot change mid-gesture, and this touches the
 * DOM twice.
 */
function pxPerRem(): number {
  try {
    const probe = document.createElement("div");
    probe.style.position = "absolute";
    probe.style.top = "-1000rem";
    probe.style.width = "100rem";
    probe.style.height = "0";
    probe.style.visibility = "hidden";
    probe.style.pointerEvents = "none";

    document.body.appendChild(probe);
    const measured = probe.getBoundingClientRect().width / 100;
    document.body.removeChild(probe);

    // Outside this range the measurement failed rather than the screen being unusual: a rem is
    // the viewport height over 1080, so even an 8K display only reaches 4. Falling back to 1
    // restores the old behaviour, which is correct at 1080p and merely wrong elsewhere - better
    // than scaling by a number that came from nowhere.
    return measured >= 0.1 && measured <= 10 ? measured : 1;
  } catch {
    return 1;
  }
}

/** The row whose window carries the workforce-against-workplaces table. Matches the C# side. */
const WORKFORCE_ID = "workers";

/**
 * The row whose window carries the stuck-vehicle list. Listed explicitly here rather than relying
 * on `breakdown !== undefined` below, since that breakdown only exists once at least one jam has
 * already been found - refreshed only while this window is open, the same as the resource tables
 * - which would otherwise leave the row unclickable until the very thing opening it existed.
 */
const TRAFFIC_ID = "traffic";

/**
 * The row whose window lists the city's cemeteries. Listed here for the same reason TRAFFIC_ID is:
 * its breakdown only exists once the window is open, so `breakdown !== undefined` would leave the
 * row unclickable until the very thing that opens it existed.
 */
const CEMETERY_ID = "cemetery";

/** Stands in for a notification type whose own icon file is missing. */
const FALLBACK_ICON = "Media/Game/Icons/Notifications.svg";

/**
 * Short forms, because the strip is glanceable and horizontal space is the scarce resource.
 * 1234 -> 1.2K, 1234567 -> 1.2M.
 */
function abbreviate(value: number): string {
  const sign = value < 0 ? "-" : "";
  const n = Math.abs(value);

  if (n < 1000) {
    return sign + n.toString();
  }
  if (n < 1_000_000) {
    return sign + trim(n / 1000) + "K";
  }
  return sign + trim(n / 1_000_000) + "M";
}

function trim(value: number): string {
  // One decimal, but never a trailing ".0" - it just costs width.
  const rounded = Math.round(value * 10) / 10;
  return Number.isInteger(rounded) ? rounded.toString() : rounded.toFixed(1);
}

/**
 * Turns one of the game's internal units into the text the game itself would show.
 *
 * The game stores several figures in units that mean nothing on screen - electricity in tenths of
 * a kilowatt, freight in kilograms - and converts at the point of display. These are vanilla's own
 * thresholds and divisors, read out of its compiled UI, so a number here reads the same as the
 * same number anywhere else in the game: 6000000 is "600 MW", not an abbreviated "6M" that is not
 * a quantity of anything.
 *
 * The player's own unit system is respected, because vanilla respects it.
 */
interface UnitTier {
  divisor: number;
  suffix: string;
  digits: number;
}

function digits(value: number, count: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: count });
}

/**
 * Which scale a value should be shown at, using vanilla's own thresholds.
 *
 * Split out from formatUnit so that two numbers shown together can share one scale. Formatting
 * them separately gave rows like "280.19 t / 24.3 kt", where working out that the first is about
 * one per cent of the second means converting in your head - which is the arithmetic the row
 * exists to save.
 */
function unitTier(value: number, unit: string, metric: boolean): UnitTier {
  const size = Math.abs(value);

  if (unit === "power") {
    // Watts are watts: vanilla has no imperial variant for this one.
    return size < 1e4
      ? { divisor: 10, suffix: " kW", digits: 1 }
      : { divisor: 1e4, suffix: " MW", digits: 2 };
  }

  if (unit === "weight") {
    if (metric) {
      if (size < 100) return { divisor: 1, suffix: " kg", digits: 1 };
      if (size < 1e6) return { divisor: 1e3, suffix: " t", digits: 2 };
      return { divisor: 1e6, suffix: " kt", digits: 2 };
    }

    // The game stores kilograms either way; only the presentation changes.
    if (size < 100) return { divisor: 1 / 2.204622621848776, suffix: " lb", digits: 1 };
    if (size < 9071847.4) return { divisor: 907.1847, suffix: " tn", digits: 2 };
    return { divisor: 907184.7, suffix: " ktn", digits: 2 };
  }

  return { divisor: 1, suffix: "", digits: 0 };
}

/**
 * Turns one of the game's internal units into the text the game itself would show.
 *
 * The game stores several figures in units that mean nothing on screen - electricity in tenths of
 * a kilowatt, freight in kilograms - and converts at the point of display. These are vanilla's own
 * thresholds and divisors, read out of its compiled UI, so a number here reads the same as the
 * same number anywhere else in the game: 6000000 is "600 MW", not an abbreviated "6M" that is not
 * a quantity of anything.
 *
 * The player's own unit system is respected, because vanilla respects it.
 */
function formatUnit(value: number, unit: string, metric: boolean): string {
  const tier = unitTier(value, unit, metric);
  return tier.suffix
    ? digits(value / tier.divisor, tier.digits) + tier.suffix
    : abbreviate(Math.round(value));
}

/**
 * A value against its ceiling, both at the same scale, with the unit named once.
 *
 * The ceiling picks the scale, because it is the half that does not move: a mode's capacity is
 * fixed by the vehicles running it while the load swings all day, so tying the scale to the load
 * would make the row's units flicker as you watch it.
 *
 * Built as one string rather than as neighbouring pieces of markup. Adjacent text in this renderer
 * loses its leading space, which turned "0 / 960" into "0/ 960".
 */
function formatPair(value: number, total: number, unit: string, metric: boolean): string {
  if (!unit) {
    return `${value.toLocaleString()} / ${total.toLocaleString()}`;
  }

  const tier = unitTier(total, unit, metric);
  return (
    `${digits(value / tier.divisor, tier.digits)} / ` +
    `${digits(total / tier.divisor, tier.digits)}${tier.suffix}`
  );
}

function formatValue(vital: Vital, value: number, metric = true): string {
  if (vital.unit) {
    return formatUnit(value, vital.unit, metric);
  }

  switch (vital.format) {
    case VitalFormat.Percentage:
      // Already on a 0-100 scale, exactly as the vanilla panels use it, so there is nothing to
      // convert here - only to round.
      return `${Math.round(value)}%`;
    default:
      return abbreviate(Math.round(value));
  }
}

/**
 * The icon, or the short text label if it will not load.
 *
 * Vanilla icon paths were checked against the files the game ships, but they are resolved by the
 * UI host at runtime and a game update could move one. Falling back to the label keeps the strip
 * readable instead of leaving a hole where a picture should be, and makes a broken path obvious
 * rather than silent.
 */
const VitalGlyph = ({ vital }: { vital: Vital }) => {
  const [failed, setFailed] = useState(false);
  const t = useT();

  if (!vital.icon || failed) {
    return <span className={styles.label}>{t(vital.labelKey, vital.label)}</span>;
  }

  return (
    <div className={styles.glyph}>
      <img
        className={styles.icon}
        src={vital.icon}
        onError={() => setFailed(true)}
      />
      {vital.badge ? <span className={styles.badge}>{vital.badge}</span> : null}
    </div>
  );
};

/**
 * The grouped problem list the Problems entry opens.
 *
 * One row per notification type with its own vanilla icon and count. Clicking a row takes the
 * camera to one of them, and clicking again moves to the next: the list is meant to be worked
 * through, not just read.
 */
/** A panel row's icon, which simply disappears if the file is not there. */
const RowIcon = ({ src }: { src: string }) => {
  const [failed, setFailed] = useState(false);
  // Not every notification type ships an icon file under that name. A generic marker keeps the
  // rows aligned instead of leaving a ragged column where some have pictures and some do not.
  const shown = !src || failed ? FALLBACK_ICON : src;
  return (
    <img
      className={styles.icon}
      src={shown}
      onError={() => setFailed(true)}
    />
  );
};

/**
 * What a panel row does when clicked.
 *
 * Transport rows talk to vanilla's own binding group rather than to ours: the game already
 * exposes triggers to choose which mode its transportation overview is showing. It does not
 * expose anything to open that panel - its visibility is React state inside the vanilla UI - so
 * the best available is to have it already on the right mode when the player opens it.
 */
function activateRow(row: BreakdownRow) {
  // Schools carry their index in the action, because two can share a name and the camera has to
  // know which one was clicked.
  if (row.action.startsWith("school:")) {
    trigger("seety", "jumpToSchool", parseInt(row.action.slice(7), 10));
    return;
  }

  // The panel can be opened after all: GamePanelUISystem exposes
  // game.showTransportationOverviewPanel, taking the tab as an int. Select the mode first so the
  // panel comes up already showing that mode's lines.
  //
  // The type name after the colon, not row.id: vanilla matches a mode by
  // Enum.GetName(typeof(TransportType), ...), and a row's display label is not always that name
  // (cargo trucks are "Car" underneath). See TransportMode.Action in TransportBreakdown.cs.
  if (row.action.startsWith("passenger:")) {
    trigger("transportationOverview", "setSelectedPassengerType", row.action.slice(10));
    trigger("game", "showTransportationOverviewPanel", 0);
    return;
  }
  if (row.action.startsWith("cargo:")) {
    trigger("transportationOverview", "setSelectedCargoType", row.action.slice(6));
    trigger("game", "showTransportationOverviewPanel", 1);
    return;
  }

  if (row.action.startsWith("cemetery:")) {
    trigger("seety", "jumpToCemetery", parseInt(row.action.slice(9), 10));
    return;
  }

  if (row.action.startsWith("jam:")) {
    trigger("seety", "jumpToJam", row.action.slice(4));
    return;
  }

  if (row.action === "jump") {
    trigger("seety", "jumpToProblem", row.id);
  }
}

/**
 * A school's colour, sliding from green to red as it fills.
 *
 * Three fixed steps put a school at 79% and one at 94% in the same bucket, when the second is the
 * one to act on. A continuous ramp from halfway shows the order of urgency at a glance, which is
 * what a list sorted by fullness is for.
 */
function fillStyle(row: BreakdownRow): React.CSSProperties | undefined {
  if (!row.action.startsWith("school:") && !row.action.startsWith("cemetery:")) {
    return undefined;
  }

  const t = Math.min(1, Math.max(0, (row.count - 50) / 50));
  const r = Math.round(126 + (255 - 126) * t);
  const g = Math.round(214 + (118 - 214) * t);
  const b = Math.round(148 + (105 - 148) * t);
  return { color: `rgb(${r}, ${g}, ${b})` };
}

/** One row inside any breakdown panel: icon, name, number, click if applicable. */
const BreakdownRowItem = ({ row }: { row: BreakdownRow }) => {
  const t = useT();
  const { unitSettings } = useLocalization();
  const metric = unitSettings.unitSystem === METRIC_UNIT_SYSTEM;
  const classes = [styles.panelRow];
  if (row.level === VitalLevel.Critical) {
    classes.push(styles.critical);
  } else if (row.level === VitalLevel.Warning) {
    classes.push(styles.warning);
  }
  if (row.clickable) {
    classes.push(styles.clickable);
  }

  return (
    <Tooltip
      tooltip={
        row.action === "jump" ||
        row.action.startsWith("jam:") ||
        ((row.action.startsWith("school:") || row.action.startsWith("cemetery:")) && row.clickable)
          ? `${row.id} - ${t("Seety.TIP_GO_THERE", "click to go there")}`
          : row.action.startsWith("school:") || row.action.startsWith("cemetery:")
          ? row.id
          : row.clickable
          ? `${row.id} - ${t(
              "Seety.TIP_SELECT_MODE",
              "click to select this mode in the transport overview"
            )}`
          : row.id
      }
    >
      <div
        className={classes.join(" ")}
        onClick={row.clickable ? () => activateRow(row) : undefined}
      >
        <RowIcon src={row.icon} />
        <span className={styles.panelName}>{row.id}</span>
        <span className={styles.value} style={fillStyle(row)}>
          {/* One string, not a row of neighbouring expressions: this renderer drops the leading
              space of adjacent text, which is what made "0 / 960" render as "0/ 960".

              row.suffix rather than a "%" hardcoded for school rows. That special case existed
              because suffix used to arrive shuffled - two C# writers emitted this type with the
              fields in different orders - and hardcoding it here is what hid that from view. */}
          {row.total > 0
            ? formatPair(row.count, row.total, row.unit, metric)
            : (row.unit
                ? formatUnit(row.count, row.unit, metric)
                : row.count.toLocaleString()) + row.suffix}
        </span>
      </div>
    </Tooltip>
  );
};

/**
 * The stuck-vehicle list, which needs a sentence the other breakdowns do not.
 *
 * Its rows are places rather than categories, which is worth saying once: the reader has to know
 * that "Cranberry Street" is where a jam is and not what kind of thing was counted.
 */
const JamRows = ({ rows }: { rows: BreakdownRow[] }) => (
  <>
    <BreakdownRows rows={rows} />
    {rows.length > 0 ? (
      <div className={styles.tableNote}>
        The worst pile-ups on the map right now, named after the street they are on, with the
        number of vehicles stopped in each. Click one to go there. Queues waiting outside the city
        are not counted, and a handful of cars giving way is not a jam.
      </div>
    ) : null}
  </>
);

const BreakdownRows = ({ rows }: { rows: BreakdownRow[] }) => {
  const t = useT();
  if (rows.length === 0) {
    return (
      <div className={styles.panelEmpty}>
        {t("Seety.EMPTY_NOTHING", "Nothing to report")}
      </div>
    );
  }

  return (
    <>
      {rows.map((row) => (
        <BreakdownRowItem key={row.id} row={row} />
      ))}
    </>
  );
};

/**
 * The transport list, split into passengers and cargo rather than one run-on list. The two
 * groups are already distinguishable in the data - see TransportMode.Action - this just draws
 * that distinction instead of throwing it away.
 */
const TransportRows = ({ rows }: { rows: BreakdownRow[] }) => {
  const t = useT();
  if (rows.length === 0) {
    return (
      <div className={styles.panelEmpty}>
        {t("Seety.EMPTY_NOTHING", "Nothing to report")}
      </div>
    );
  }

  const passengers = rows.filter((r) => r.action.startsWith("passenger:"));
  const cargo = rows.filter((r) => r.action.startsWith("cargo:"));

  return (
    <>
      {passengers.length > 0 ? (
        <div className={styles.panelSection}>
          {t("Seety.SEC_PASSENGERS", "Passengers")}
        </div>
      ) : null}
      {passengers.map((row) => (
        <BreakdownRowItem key={row.id} row={row} />
      ))}

      {cargo.length > 0 ? (
        <div className={styles.panelSection}>{t("Seety.SEC_CARGO", "Cargo")}</div>
      ) : null}
      {cargo.map((row) => (
        <BreakdownRowItem key={row.id} row={row} />
      ))}

      <div className={styles.tableNote}>
        Aboard right now, counted vehicle by vehicle, against what the vehicles currently running
        that mode could hold between them. Not a rolling total: an empty line reads empty.
      </div>
    </>
  );
};

/**
 * The recorded series, as a filled line.
 *
 * Hand-drawn SVG rather than a charting library: one polyline is not worth a dependency, and the
 * strip has to stay small. The scale is the series' own range, so a flat line that never moves
 * reads as flat rather than as noise blown up to fill the box.
 */
const HistoryChart = ({ history }: { history: History }) => {
  const values = history.values ?? [];
  if (values.length < 2) {
    return null;
  }

  const width = 200;
  const height = 54;

  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min;

  const x = (i: number) => (i / (values.length - 1)) * width;
  // A constant series would divide by zero; park it on the middle line instead.
  const y = (v: number) => (span <= 0 ? height / 2 : height - ((v - min) / span) * height);

  const line = values.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(" ");
  const area = `0,${height} ${line} ${width},${height}`;

  return (
    <div className={styles.chart}>
      <div className={styles.chartLabel}>
        <span>{history.label}</span>
        <span>{Math.round(max).toLocaleString()}</span>
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" className={styles.chartSvg}>
        <polygon points={area} fill="rgba(130, 190, 255, 0.20)" />
        <polyline points={line} fill="none" stroke="rgb(130, 190, 255)" strokeWidth="1.5" />
      </svg>
    </div>
  );
};

/** Thresholds for vanilla-sourced rows are judged here, because their value never reaches C#. */
function evaluate(vital: Vital, value: number): VitalLevel {
  if (!vital.hasThreshold) {
    return VitalLevel.Normal;
  }
  if (vital.lowIsBad) {
    if (value <= vital.critical) return VitalLevel.Critical;
    return value <= vital.warning ? VitalLevel.Warning : VitalLevel.Normal;
  }
  if (value >= vital.critical) return VitalLevel.Critical;
  return value >= vital.warning ? VitalLevel.Warning : VitalLevel.Normal;
}

/**
 * Reads a row from vanilla's own bindings.
 *
 * These numbers are produced by jobs inside vanilla's infoview UI systems, which skip their work
 * entirely unless something is subscribed to the binding. Subscribing here is what makes the
 * value exist - it cannot be done from C#, which is why this one calculation lives in the UI
 * while every other vital arrives already computed.
 */
function useVanillaValue(vital: Vital): number {
  const supply$ = useMemo(
    () =>
      vital.bindSupply
        ? bindValue<number>(vital.bindGroup, vital.bindSupply, 0)
        : null,
    [vital.bindGroup, vital.bindSupply]
  );
  const demand$ = useMemo(
    () => bindValue<any>(vital.bindGroup, vital.bindDemand, null),
    [vital.bindGroup, vital.bindDemand]
  );

  const supply2$ = useMemo(
    () =>
      vital.bindSupply2
        ? bindValue<number>(vital.bindGroup, vital.bindSupply2, 0)
        : null,
    [vital.bindGroup, vital.bindSupply2]
  );
  const demand2$ = useMemo(
    () =>
      vital.bindDemand2
        ? bindValue<number>(vital.bindGroup, vital.bindDemand2, 0)
        : null,
    [vital.bindGroup, vital.bindDemand2]
  );

  // Hooks must run unconditionally, so unused slots fall back to a constant.
  const supplyA = useValue(supply$ ?? ZERO$);
  const raw = useValue(demand$);
  const supplyB = useValue(supply2$ ?? ZERO$);
  const demandB = useValue(demand2$ ?? ZERO$);

  const supply = supplyA + (vital.bindSupply2 !== NO_BINDING ? supplyB : 0);
  const extraDemand = vital.bindDemand2 !== NO_BINDING ? demandB : 0;

  switch (vital.bindKind) {
    case VanillaKind.Scalar:
      return typeof raw === "number" ? raw : 0;

    case VanillaKind.PollutionGroup:
      // Handled by its own component; this branch keeps the switch total.
      return 0;

    case VanillaKind.DemandGroup:
      // Handled by its own component; this branch keeps the switch total.
      return 0;

    case VanillaKind.Fraction:
      // Demand arrives as 0..1 - vanilla divides the target by 100 in AdvanceSmoothDemand.
      return typeof raw === "number" ? raw * 100 : 0;

    case VanillaKind.FlowArray: {
      // Vanilla repeats the first entry at the end to close its radar chart; averaging that
      // duplicate in would quietly weight one road category twice.
      if (!Array.isArray(raw) || raw.length < 2) {
        return 0;
      }
      const real = raw.slice(0, raw.length - 1);
      const sum = real.reduce((a: number, b: number) => a + b, 0);
      // Already a percentage: TrafficFlowJob multiplies GetTrafficFlowSpeed by 100 before
      // accumulating. Scaling again is what produced "Traffic flow: 9664%".
      return sum / real.length;
    }

    case VanillaKind.Indicator: {
      const indicator = raw as IndicatorValue;
      if (!indicator) {
        return 0;
      }
      // A negative min means vanilla built this with IndicatorValue.Calculate, so current is
      // headroom in -1..+1 rather than a level on a 0..max scale.
      //
      // Read as coverage, to match the Coverage rows: headroom 0 means supply exactly meets
      // demand, which is full coverage; headroom -1 means completely overwhelmed. Mapping
      // (1 + current) is an approximation - the true ratio cannot be recovered once vanilla has
      // clamped it - but it is monotonic and points the right way.
      if (indicator.min < 0) {
        return Math.min(100, (1 + indicator.current) * 100);
      }
      return indicator.max > 0 ? (indicator.current / indicator.max) * 100 : 0;
    }

    case VanillaKind.Coverage: {
      const needed = (typeof raw === "number" ? raw : 0) + extraDemand;
      // Nothing needed is fully covered; something needed with nothing to serve it is not
      // covered at all. The second case is the one that matters: a city with no water pump
      // reads 0%, where utilisation would have claimed a reassuring 100%.
      if (needed <= 0) {
        return 100;
      }
      return Math.min(100, (supply / needed) * 100);
    }

    default: {
      const used = (typeof raw === "number" ? raw : 0) + extraDemand;
      if (supply > 0) {
        return (used / supply) * 100;
      }
      // No capacity at all: there is no room, by definition. Returning 0 here read as "empty",
      // and once inverted the strip claimed 100% free landfill in a city with no landfill and
      // 100% free cells in a city with no prison. Full is the honest answer; inverted it becomes
      // "no space", which is exactly what having no facility means.
      return 100;
    }
  }
}

/**
 * A window that floats over the game and can be dragged anywhere.
 *
 * Panels used to hang under the strip, which meant they moved when the strip moved and could not
 * be put where there was room. A window is separate furniture: it has its own position, its own
 * close button, and it stays where you left it.
 */
const FloatingWindow = ({
  title,
  onClose,
  action,
  children,
}: {
  title: string;
  onClose: () => void;
  /** An optional control in the title bar, next to the close button. */
  action?: React.ReactNode;
  children: React.ReactNode;
}) => {
  const [pos, setPos] = useState({ x: 320, y: 160 });
  const [dragging, setDragging] = useState(false);
  const windowRef = useRef<HTMLDivElement | null>(null);
  const grab = useRef({ pointerX: 0, pointerY: 0, originX: 0, originY: 0, scale: 1 });

  const constrain = useCallback((position: { x: number; y: number }, scale: number) =>
    clampPosition(position,
      { width: window.innerWidth / scale, height: window.innerHeight / scale },
      { width: (windowRef.current?.offsetWidth ?? 0) / scale,
        height: (windowRef.current?.offsetHeight ?? 0) / scale }), []);

  useEffect(() => {
    const keepOnScreen = () => {
      const scale = pxPerRem();
      setPos((current) => {
        const next = constrain(current, scale);
        return next.x === current.x && next.y === current.y ? current : next;
      });
    };
    keepOnScreen();
    window.addEventListener("resize", keepOnScreen);
    return () => window.removeEventListener("resize", keepOnScreen);
  }, [constrain]);

  const onMouseDown = useCallback(
    (event: React.MouseEvent) => {
      event.stopPropagation();
      if (event.button !== 0) return;
      grab.current = {
        pointerX: event.clientX,
        pointerY: event.clientY,
        originX: pos.x,
        originY: pos.y,
        // Pointer deltas are pixels, `pos` is rem. See pxPerRem.
        scale: pxPerRem(),
      };
      setDragging(true);
    },
    [pos.x, pos.y]
  );

  useEffect(() => {
    if (!dragging) {
      return;
    }
    const onMove = (event: MouseEvent) => {
      const scale = grab.current.scale;
      setPos(constrain({
        x: grab.current.originX + (event.clientX - grab.current.pointerX) / scale,
        y: grab.current.originY + (event.clientY - grab.current.pointerY) / scale,
      }, scale));
    };
    const onUp = () => setDragging(false);
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
    return () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
  }, [dragging, constrain]);

  return (
    <div
      className={styles.window}
      ref={windowRef}
      style={{ left: `${pos.x}rem`, top: `${pos.y}rem` }}
      // The strip below is draggable too; without this a drag inside the window moves both.
      onMouseDown={(e) => e.stopPropagation()}
    >
      <div className={styles.windowBar} onMouseDown={onMouseDown}>
        <span className={styles.windowTitle}>{title}</span>
        {action}
        <span className={styles.windowClose} onMouseDown={(e) => e.stopPropagation()} onClick={onClose}>
          {/* A plain capital X: not a multiplication sign, and no longer an SVG.

              The multiplication sign came out as an empty rectangle - the game's font has no
              glyph for it - which is why this was drawn as a path in the first place. That path
              then stayed black through two attempts at colouring it: first inherited through
              `currentColor`, which this renderer does not resolve, then named outright, which it
              ignored too. Whatever it does with an inline stroked path, it is not what a browser
              does.

              So the glyph is a letter now. Text renders and `color` applies - every other word in
              this file proves both - and the button reads as a button from its background rather
              than from anything the SVG engine has to agree to draw. */}
          X
        </span>
      </div>
      <div className={styles.windowBody}>{children}</div>
    </div>
  );
};

/**
 * Workforce and workplaces on the same rows.
 *
 * The comparison is the whole point: reading "176 educated citizens" beside "2,367 open educated
 * jobs" on one line tells you instantly that the city was built for people who do not live in it.
 * Two separate tables leave that to be assembled in the reader's head.
 */
const WorkforceTable = ({ data }: { data: Workforce }) => {
  // Every column arrives already counted from C#, jobs included - see CitizenCensusSystem. An
  // earlier version subscribed to workplaces.workplacesData here, which is why this component
  // used to carry a note about waking that binding; it reads zero unless vanilla's own
  // Workplaces panel is on screen, so nothing is bound here any more.
  const t = useT();
  const rows = data?.rows ?? [];
  if (rows.length === 0) {
    return null;
  }

  // Left of the divider every column counts citizens; right of it they count jobs. Mixing the
  // two without a line between them was the single thing that made the table hard to read.
  // [head, value, rule down its left edge, needs a wider column]
  const columns: [string, (r: WorkforceRow, i: number) => number, boolean, boolean?][] = [
    // The third flag draws a rule down the left of the column, so a line "after Education"
    // belongs to Total. Four groups now: who they are, how many, what they are doing, and the
    // jobs themselves.
    [t("Seety.WF_TOTAL", "Total"), (r) => r.total, true],
    // Kids and Student overlapped: a child at school appeared in both. Kids is the ones who are
    // not studying, so the columns add up - but only against the students who are actually
    // children. Subtracting the whole Student column took university students, who are adults,
    // off the children's total and drove Kids to zero on exactly the levels where people study.
    [t("Seety.WF_KIDS", "Kids"), (r) => Math.max(0, r.children - r.childStudents), true],
    [t("Seety.WF_STUDENT", "Student"), (r) => r.students, false],
    [t("Seety.WF_OLD", "Old"), (r) => r.seniors, false],
    [t("Seety.WF_ADULTS", "Adults"), (r) => r.workingAge, false],
    [t("Seety.WF_EMPLOYED", "Employed"), (r) => r.workers, false],
    // Named for what it reads: Field.Unemployed from the census, not an invention of this table.
    [t("Seety.WF_UNEMPLOYED", "Unemployed"), (r) => r.unemployed, false, true],
    [t("Seety.WF_UNDER", "Under"), (r) => r.under, false],
    [t("Seety.WF_OUT", "Out"), (r) => r.outside, false],
    [t("Seety.WF_IN", "In"), (r) => r.commuters, true],
    // "Posts" was opaque. These are jobs, not people: how many exist at this level, and how many
    // of them nobody is doing.
    [t("Seety.WF_JOBS", "Jobs"), (r) => r.jobs, true],
    [t("Seety.WF_VACANT", "Vacant"), (r) => r.vacant, false],
  ];

  const totals = columns.map(([, get]) =>
    rows.reduce((sum, r, i) => sum + get(r, i), 0)
  );

  const cellClass = (divider: boolean, highlight: boolean, wide = false) =>
    [
      styles.tableCell,
      wide ? styles.tableCellWide : "",
      divider ? styles.tableDivider : "",
      highlight ? styles.tableShort : "",
    ]
      .filter(Boolean)
      .join(" ");

  return (
    <div className={styles.table}>
      <div className={`${styles.tableRow} ${styles.tableHead}`}>
        <span className={styles.tableLevel}>{t("Seety.WF_EDUCATION", "Education")}</span>
        {columns.map(([name, , divider, wide]) => (
          <span key={name} className={cellClass(divider, false, wide)}>
            {name}
          </span>
        ))}
      </div>

      {rows.map((row, i) => {
        // Open posts at a level outnumbering every adult who has it: the city was built for
        // people who are not there. This is the comparison the table exists for.
        const open = row.vacant;
        const short_ = open > row.workingAge;

        return (
          <div key={row.level} className={styles.tableRow}>
            <span className={styles.tableLevel}>{row.level}</span>
            {columns.map(([name, get, divider, wide], c) => (
              <span
                key={name}
                className={cellClass(divider, short_ && c === columns.length - 1, wide)}
              >
                {get(row, i).toLocaleString()}
              </span>
            ))}
          </div>
        );
      })}

      <div className={`${styles.tableRow} ${styles.tableTotal}`}>
        <span className={styles.tableLevel}>{t("Seety.WF_TOTAL", "Total")}</span>
        {totals.map((value, i) => (
          <span key={i} className={cellClass(columns[i][2], false, columns[i][3])}>
            {value.toLocaleString()}
          </span>
        ))}
      </div>

      <div className={`${styles.tableNote} ${styles.tableNoteWide}`}>
        Tourists excluded. Kids are the ones not at school. Out lives here and works outside the
        city; In works here and lives outside it.
      </div>
    </div>
  );
};

/**
 * Why a demand figure is where it is.
 *
 * Vanilla already sorts these by weight and keeps the top five, so this is presentation only.
 * Names arrive as enum identifiers - "TaxRate", "LandValue" - which are spaced out for reading;
 * translating them properly is a job for the localisation pass.
 */
const FactorList = ({ binding }: { binding: string }) => {
  const factors$ = useMemo(
    () => bindValue<Factor[]>("cityInfo", binding, []),
    [binding]
  );
  const factors = useValue(factors$) ?? [];
  const t = useT();

  if (factors.length === 0) {
    return (
      <div className={styles.panelEmpty}>
        {t("Seety.EMPTY_FACTORS", "No factors reported")}
      </div>
    );
  }

  return (
    <div className={styles.table}>
      {factors.map((f) => (
        <div key={f.factor} className={styles.tableRow}>
          <span className={styles.tableLevel}>{spaceOut(f.factor)}</span>
          <span
            className={`${styles.tableCell} ${
              f.weight < 0 ? styles.tableShort : ""
            }`}
          >
            {f.weight > 0 ? `+${f.weight}` : f.weight}
          </span>
        </div>
      ))}
      <div className={styles.tableNote}>
        The five biggest influences on this demand right now, as the game reports them. A negative
        weight is what is holding it back.
      </div>
    </div>
  );
};

/** "LandValue" -> "Land value". Enum names are not written for people. */
function spaceOut(name: string): string {
  const spaced = name.replace(/([a-z])([A-Z])/g, "$1 $2");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

/**
 * The six zone demands, each expandable into the reasons behind it.
 *
 * One strip row instead of six: they are usually all low together, and six cells for that was a
 * third of the bar spent on one idea.
 */
/**
 * The blue bar, taken apart.
 *
 * Vanilla gives one number for commercial demand and one for industrial, which says whether to
 * zone but never what for. The game tracks all of this per resource already - it just never draws
 * it. See Seety.Vitals.ResourceBreakdown.
 *
 * No icons: resource artwork lives on the resource prefabs rather than at a fixed path, so a
 * guessed filename would have shown a column of broken images.
 */
type ResourceKind = "commercial" | "industrial" | "office";

/**
 * Per-kind labels. Office reads the industrial columns - it is the same production-against-
 * demand shape, not a shop with shelves - see Seety.Vitals.ResourceBreakdown.RefreshOffice.
 */
const RESOURCE_TABLE_TEXT: Record<
  ResourceKind,
  { companyKey: string; companyHead: string; stockKey: string; stockHead: string; note: string }
> = {
  commercial: {
    companyKey: "Seety.RES_SHOPS",
    companyHead: "Shops",
    stockKey: "Seety.RES_STOCK",
    stockHead: "Stock",
    note: 'Wanted is demand relative to the good the city wants most - 100% is the top of the list, not "fully satisfied". Amber stock means the shops are there but the shelves are empty - that is a supply problem, not a zoning one. Homeless companies want premises.',
  },
  industrial: {
    companyKey: "Seety.RES_PLANTS",
    companyHead: "Plants",
    stockKey: "Seety.RES_MADE",
    stockHead: "Made",
    note: "Wanted is demand relative to the good the city wants most; Made is production against that demand. Amber means the city is asking for more than anyone is producing.",
  },
  office: {
    companyKey: "Seety.RES_OFFICES",
    companyHead: "Offices",
    stockKey: "Seety.RES_MADE",
    stockHead: "Made",
    note: "The same reading as industrial, for the four resources - software, telecom, financial services, media - the game itself counts as office work rather than manufacturing.",
  },
};

const ResourceTable = ({ kind }: { kind: ResourceKind }) => {
  const t = useT();
  const all = useValue(resources$);
  const rows = all?.[kind] ?? [];

  if (rows.length === 0) {
    return (
      <div className={styles.panelEmpty}>
        {t("Seety.EMPTY_TRADE", "Nothing traded yet")}
      </div>
    );
  }

  const text = RESOURCE_TABLE_TEXT[kind];

  return (
    <div className={styles.table}>
      <div className={`${styles.tableRow} ${styles.tableHead}`}>
        <span className={styles.tableLevel}>{t("Seety.RES_RESOURCE", "Resource")}</span>
        <span className={styles.tableCell}>{t("Seety.RES_WANTED", "Wanted")}</span>
        <span className={styles.tableCell}>{t(text.companyKey, text.companyHead)}</span>
        <span className={styles.tableCell}>{t(text.stockKey, text.stockHead)}</span>
        <span className={styles.tableCell}>{t("Seety.RES_STAFF", "Staff")}</span>
      </div>

      {rows.map((r) => {
        const clickable = r.priorityName !== "";
        const row = (
          <div
            className={`${styles.tableRow} ${clickable ? styles.clickable : ""}`}
            onClick={
              clickable
                ? () => trigger("seety", "jumpToResource", kind, r.resourceIndex)
                : undefined
            }
          >
            <span className={styles.tableLevel}>{spaceOut(r.name)}</span>
            <span className={styles.tableCell}>{Math.round(r.demand)}%</span>
            <span className={styles.tableCell}>{r.companies}</span>
            {/* Wanted but unstocked is the supply-chain case: the shops exist and the shelves
                are empty, so zoning more of them would not help. */}
            <span
              className={`${styles.tableCell} ${
                r.demand > 0 && r.stock < 25 ? styles.tableShort : ""
              }`}
            >
              {Math.round(r.stock)}%
            </span>
            <span
              className={`${styles.tableCell} ${r.staff < 50 ? styles.tableShort : ""}`}
            >
              {Math.round(r.staff)}%
            </span>
          </div>
        );

        // Homeless companies want premises - worth knowing, but writing it into the row itself
        // made every table row a different length and read as noise. It goes in the tooltip
        // instead, alongside the priority reason that is already hover-only for the same reason.
        const homeless =
          r.noPremises > 0
            ? `${r.noPremises} ${r.noPremises === 1 ? "company" : "companies"} homeless, waiting for premises`
            : "";

        // Not every resource has a company at all - a row with nobody trading in it is not
        // clickable, and wrapping it in a Tooltip that says nothing would just be noise.
        if (!clickable) {
          return homeless ? (
            <Tooltip key={r.name} tooltip={homeless}>
              {row}
            </Tooltip>
          ) : (
            <div key={r.name}>{row}</div>
          );
        }

        const reason = r.priorityReason ? ` - ${r.priorityReason}` : "";
        const homelessNote = homeless ? ` (${homeless})` : "";

        return (
          <Tooltip
            key={r.name}
            tooltip={`${r.priorityName}${reason}${homelessNote} - click to go there`}
          >
            {row}
          </Tooltip>
        );
      })}

      <div className={styles.tableNote}>{text.note}</div>
    </div>
  );
};

const DemandList = () => {
  const t = useT();
  const values = demand$.map((b) => useValue(b));
  const [open, setOpen] = useState<string | null>(null);

  return (
    <div className={styles.table}>
      {DEMANDS.map((d, i) => (
        <div key={d.id}>
          <div
            className={`${styles.panelRow} ${styles.clickable}`}
            onClick={() => setOpen((o) => (o === d.id ? null : d.id))}
          >
            <img className={styles.icon} src={d.icon} />
            <span className={styles.panelName}>{t(d.key, d.label)}</span>
            <span className={styles.value}>
              {Math.round((values[i] ?? 0) * 100)}%
            </span>
          </div>
          {open === d.id ? <FactorList binding={d.factors} /> : null}
          {open === d.id && d.id === "commercialDemand" ? (
            <ResourceTable kind="commercial" />
          ) : null}
          {open === d.id && d.id === "industrialDemand" ? (
            <ResourceTable kind="industrial" />
          ) : null}
          {open === d.id && d.id === "officeDemand" ? (
            <ResourceTable kind="office" />
          ) : null}
        </div>
      ))}
      <div className={styles.tableNote}>
        How much appetite the city has for each kind of zone, from the same readings that drive the
        game&apos;s own demand bars. Click one for the five biggest influences on it.
      </div>
    </div>
  );
};

/**
 * Who lives in the city: age against education.
 *
 * The same census that fills the workforce table, asked a different question. The bar under each
 * age band is its share of the whole population, so the shape of the city reads before any of the
 * numbers do - a wide band of seniors is a wave of retirements coming, a wide band of teens is a
 * school problem arriving in a few years.
 */
const DemographicsTable = ({ rows }: { rows: AgeRow[] }) => {
  const t = useT();
  if (rows.length === 0) {
    return null;
  }

  const rowTotal = (r: AgeRow) => (r.levels ?? []).reduce((a, b) => a + b, 0);
  const population = rows.reduce((sum, r) => sum + rowTotal(r), 0);

  return (
    <div className={styles.table}>
      <div className={`${styles.tableRow} ${styles.tableHead}`}>
        <span className={styles.tableLevel}>{t("Seety.DEMO_AGE", "Age")}</span>
        {LEVEL_NAMES.map((level) => (
          <span key={level.key} className={styles.tableCell}>
            {t(level.key, level.english)}
          </span>
        ))}
        <span className={`${styles.tableCell} ${styles.tableDivider}`}>
          {t("Seety.WF_TOTAL", "Total")}
        </span>
      </div>

      {rows.map((row) => {
        const total = rowTotal(row);
        const share = population > 0 ? (total / population) * 100 : 0;

        return (
          <div key={row.age} className={styles.tableRow}>
            <span className={styles.tableLevel}>
              {t(row.ageKey, row.age)}
              <span
                className={styles.ageBar}
                style={{ width: `${Math.max(2, share).toFixed(1)}%` }}
              />
            </span>
            {(row.levels ?? []).map((value, i) => (
              <span key={i} className={styles.tableCell}>
                {value.toLocaleString()}
              </span>
            ))}
            <span className={`${styles.tableCell} ${styles.tableDivider}`}>
              {total.toLocaleString()}
            </span>
          </div>
        );
      })}

      <div className={styles.tableNote}>
        Every citizen counted once, tourists excluded. The bar under each age is its share of the
        population. Education for a child is the level they have finished, not the one they are
        attending.
      </div>
    </div>
  );
};

/**
 * Parking split in two, because the game reports it in two places - roadsInfo for cars,
 * bikesInfo for bikes - but both the same shape underneath: a parked count and a capacity, see
 * bikeParking$. Room left, not spaces taken, matching the row on the strip: 100% is empty.
 */
const ParkingList = () => {
  const t = useT();
  const capacity = useValue(parkingCapacity$);
  const parked = useValue(parkedCars$);
  const bikes = useValue(bikeParking$);

  const carsFree = capacity > 0 ? Math.max(0, 100 - (parked / capacity) * 100) : 0;
  const bikesFree = bikes.y > 0 ? Math.max(0, 100 - (bikes.x / bikes.y) * 100) : 0;

  return (
    <div className={styles.table}>
      <div
        className={`${styles.panelRow} ${styles.clickable}`}
        onClick={() => trigger("seety", "openInfoview", "Roads")}
      >
        <img className={styles.icon} src="Media/Game/Icons/Parking.svg" />
        <span className={styles.panelName}>
          {capacity > 0
            ? `${t("Seety.PARK_CARS", "Cars")} - ${parked.toLocaleString()} ${t(
                "Seety.PARKED_OF",
                "parked of"
              )} ${Math.round(capacity).toLocaleString()}`
            : t("Seety.PARK_CARS", "Cars")}
        </span>
        <span className={styles.value}>{Math.round(carsFree)}%</span>
      </div>

      <div
        className={`${styles.panelRow} ${styles.clickable}`}
        onClick={() => trigger("seety", "openInfoview", "Bicycles")}
      >
        <img className={styles.icon} src="Media/Game/Icons/Bicycles.svg" />
        <span className={styles.panelName}>
          {bikes.y > 0
            ? `${t("Seety.PARK_BIKES", "Bikes")} - ${bikes.x.toLocaleString()} ${t(
                "Seety.PARKED_OF",
                "parked of"
              )} ${Math.round(bikes.y).toLocaleString()}`
            : t("Seety.PARK_BIKES", "Bikes")}
        </span>
        <span className={styles.value}>{Math.round(bikesFree)}%</span>
      </div>
    </div>
  );
};

/**
 * The four pollutions, each opening its own map view.
 *
 * Shown as quality rather than pollution, matching the row above: more is better everywhere on
 * the strip, and a row that flipped the rule inside its own window would undo the point of it.
 */
const PollutionList = () => {
  const values = pollution$.map((b) => levelOf(useValue(b)));

  return (
    <div className={styles.table}>
      {POLLUTIONS.map((x, i) => {
        const quality = Math.max(0, 100 - values[i]);
        const classes = [styles.panelRow, styles.clickable];
        if (quality <= 35) {
          classes.push(styles.critical);
        } else if (quality <= 60) {
          classes.push(styles.warning);
        }

        return (
          <Tooltip key={x.binding} tooltip={`${x.label} quality - click to open the map view`}>
            <div
              className={classes.join(" ")}
              onClick={() => trigger("seety", "openInfoview", x.infoview)}
            >
              <img className={styles.icon} src={x.icon} />
              <span className={styles.panelName}>{x.label}</span>
              <span className={styles.value}>{Math.round(quality)}%</span>
            </div>
          </Tooltip>
        );
      })}
      <div className={styles.tableNote}>
        Quality, not pollution: a higher number is a cleaner city, the same direction as every
        other reading on the strip. Click one to open its map view.
      </div>
    </div>
  );
};

/** A row whose number comes from vanilla. Split out so its hooks live in their own component. */
/**
 * One reading inside a window: icon, name, number, and a click that opens its info view.
 *
 * This is how a merged row shows both halves of its pair. The parent is drawn here too, not just
 * its companions, because inside the window the visible reading is no longer privileged - the
 * player opened it to see both.
 */
const ReadingRow = ({ vital }: { vital: Vital }) => {
  const t = useT();
  const { unitSettings } = useLocalization();
  const metric = unitSettings.unitSystem === METRIC_UNIT_SYSTEM;
  const draw = (value: number, level: VitalLevel) => {
    const classes = [styles.panelRow];
    if (level === VitalLevel.Critical) {
      classes.push(styles.critical);
    } else if (level === VitalLevel.Warning) {
      classes.push(styles.warning);
    }
    if (vital.clickable) {
      classes.push(styles.clickable);
    }

    return (
      <Tooltip
        tooltip={
          vital.clickable
            ? `${t(vital.titleKey, vital.title)} - ${t(
                "Seety.TIP_OPEN_INFO",
                "click to open its info view"
              )}`
            : t(vital.titleKey, vital.title)
        }
      >
        <div
          className={classes.join(" ")}
          onClick={
            vital.clickable
              ? () => trigger("seety", "activate", vital.id)
              : undefined
          }
        >
          <RowIcon src={vital.icon} />
          <span className={styles.panelName}>{t(vital.titleKey, vital.title)}</span>
          <span className={styles.value}>{formatValue(vital, value, metric)}</span>
        </div>
      </Tooltip>
    );
  };

  // Same split as the bar itself: vanilla-bound readings subscribe for their own number, the
  // rest arrived with it already computed in C#.
  return vital.bindGroup ? (
    <VanillaEntry vital={vital} render={draw} />
  ) : (
    draw(vital.value, vital.level)
  );
};

/** A merged row's window: the visible reading first, then the ones folded in behind it. */
const MergedReadings = ({ vital }: { vital: Vital }) => (
  <>
    <ReadingRow vital={vital} />
    {vital.companions.map((companion) => (
      <ReadingRow key={companion.id} vital={companion} />
    ))}
  </>
);

const VanillaEntry = ({
  vital,
  render,
}: {
  vital: Vital;
  render: (value: number, level: VitalLevel) => JSX.Element;
}) => {
  const peak = useDemandPeak();
  const dirt = usePollutionAverage();
  const measured = useVanillaValue(vital);

  const raw =
    vital.bindKind === VanillaKind.DemandGroup
      ? peak
      : vital.bindKind === VanillaKind.PollutionGroup
      ? dirt
      : measured;
  // Inverted rows report the good half of the figure, so that across the whole strip a taller,
  // greener bar always means better. See Vital.Invert.
  const value = vital.invert ? Math.max(0, 100 - raw) : raw;
  return render(value, evaluate(vital, value));
};

export const VitalsStrip = () => {
  const t = useT();
  const { unitSettings } = useLocalization();
  const metric = unitSettings.unitSystem === METRIC_UNIT_SYSTEM;
  const vitals = useValue(vitals$);
  const visible = useValue(visible$);
  const savedX = useValue(posX$);
  const savedY = useValue(posY$);

  const breakdowns = useValue(breakdowns$);
  const history = useValue(history$);
  const workforce = useValue(workforce$);
  const demographics = useValue(demographics$);
  const iconsHidden = useValue(iconsHidden$);
  const configMode = useValue(configMode$);
  const outlined = useValue(iconOutline$);

  const [pos, setPos] = useState({ x: savedX, y: savedY });
  const [dragging, setDragging] = useState(false);
  // Which row is expanded, or null. One at a time: two open panels would overlap.
  const [expanded, setExpanded] = useState<string | null>(null);
  const activeExpanded = visible && !configMode && vitals?.some((v) => v.id === expanded && v.enabled)
    ? expanded : null;

  // C# only fetches a series for the row that is actually open, so it has to be told.
  useEffect(() => {
    trigger("seety", "expand", activeExpanded ?? "");
  }, [activeExpanded]);

  useEffect(() => {
    if (expanded && !activeExpanded) setExpanded(null);
  }, [expanded, activeExpanded]);

  useEffect(() => () => trigger("seety", "expand", ""), []);

  const drag = useRef({ pointerX: 0, pointerY: 0, originX: 0, originY: 0, moved: false, scale: 1 });
  const elementRef = useRef<HTMLDivElement | null>(null);

  // The C# side is the owner of the position; follow it except while the pointer is down, when
  // the local state is ahead of what has been saved.
  useEffect(() => {
    if (!dragging) {
      setPos({ x: savedX, y: savedY });
    }
  }, [savedX, savedY, dragging]);

  const onMouseDown = useCallback(
    (event: React.MouseEvent) => {
      if (event.button !== 0) return;
      drag.current = {
        pointerX: event.clientX,
        pointerY: event.clientY,
        originX: pos.x,
        originY: pos.y,
        moved: false,
        // Pointer deltas are pixels, `pos` is rem. See pxPerRem.
        scale: pxPerRem(),
      };
      setDragging(true);
    },
    [pos.x, pos.y]
  );

  useEffect(() => {
    if (!dragging) {
      return;
    }

    const onMove = (event: MouseEvent) => {
      // The threshold is about how far the pointer travelled, so it stays in pixels.
      const movedX = event.clientX - drag.current.pointerX;
      const movedY = event.clientY - drag.current.pointerY;

      if (!drag.current.moved && Math.abs(movedX) + Math.abs(movedY) < DRAG_THRESHOLD) {
        return;
      }
      drag.current.moved = true;

      // Everything from here down is rem, because that is what `pos` and the grid are in.
      const scale = drag.current.scale;
      const dx = movedX / scale;
      const dy = movedY / scale;

      // Keep a grabbable part of the strip on screen whatever the player does with it. offsetWidth
      // and innerWidth are both pixels, so both are converted before meeting a rem coordinate.
      const width = (elementRef.current?.offsetWidth ?? 0) / scale;
      const height = (elementRef.current?.offsetHeight ?? 0) / scale;
      const maxX = Math.max(0, window.innerWidth / scale - width);
      const maxY = Math.max(0, window.innerHeight / scale - height);

      setPos({
        x: Math.min(maxX, Math.max(0, snapToGrid(drag.current.originX + dx))),
        // Horizontal keeps the plain grid; only the vertical has a line worth locking onto.
        y: Math.min(maxY, Math.max(0, snapY(drag.current.originY + dy))),
      });
    };

    const onUp = () => {
      setDragging(false);

      if (drag.current.moved) {
        setPos((current) => {
          trigger("seety", "setPosition", Math.round(current.x), Math.round(current.y));
          return current;
        });
      }
    };

    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);

    return () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
  }, [dragging]);

  if (!visible || !vitals || vitals.length === 0) {
    return null;
  }

  const openVital = activeExpanded ? vitals.find((v) => v.id === activeExpanded) : undefined;
  const openBreakdown = activeExpanded
    ? (breakdowns ?? []).find((b) => b.id === activeExpanded)
    : undefined;

  return (
    <div
      ref={elementRef}
      className={[
        styles.strip,
        dragging ? styles.dragging : "",
        configMode ? styles.stripConfig : "",
        outlined ? styles.outlined : "",
      ]
        .filter(Boolean)
        .join(" ")}
      style={{ left: `${pos.x}rem`, top: `${pos.y}rem` }}
      onMouseDown={onMouseDown}
    >
      {configMode ? (
        <span className={styles.configBanner}>
          {t("Seety.CONFIG_BANNER", "Choosing what to show")}
        </span>
      ) : null}

      {vitals
        .filter((vital) => configMode || vital.enabled)
        // Bars first, counts last. Every bar-format entry is the same fixed box with no text in
        // it; a count carries a number of its own and reads differently, so grouping them at one
        // end keeps the left side a clean, uniform row instead of a number breaking it up every
        // few entries. Stable sort, so within each group the game's own ordering is unchanged.
        .sort((a, b) => {
          const aBar = a.format === VitalFormat.Percentage ? 0 : 1;
          const bBar = b.format === VitalFormat.Percentage ? 0 : 1;
          return aBar - bBar;
        })
        .map((vital) => {
        const breakdown = (breakdowns ?? []).find((b) => b.id === vital.id);
        // A row is expandable if it has a list behind it, a chart, a table, factors, or any
        // combination. In configuration mode nothing expands: a click means "show this one".
        const hasPanel =
          !configMode &&
          (breakdown !== undefined ||
            vital.hasHistory ||
            vital.factors !== "" ||
            vital.bindKind === VanillaKind.DemandGroup ||
            vital.id === DEMOGRAPHICS_ID ||
            vital.id === PARKING_ID ||
            vital.id === POLLUTION_ID ||
            vital.companions.length > 0 ||
            SCHOOL_IDS.indexOf(vital.id) >= 0 ||
            vital.id === WORKFORCE_ID ||
            vital.id === TRAFFIC_ID ||
            vital.id === CEMETERY_ID);

        const row = (value: number, level: VitalLevel) => {
          const classes = [styles.entry];
          // A row with a list behind it has no infoview, so C# reports it as non-clickable -
          // but it does open that list, so it still needs to look pressable.
          if (configMode || vital.clickable || hasPanel) {
            classes.push(styles.clickable);
          }
          if (configMode && !vital.enabled) {
            classes.push(styles.disabled);
          }
          if (level === VitalLevel.Warning) {
            classes.push(styles.warning);
          } else if (level === VitalLevel.Critical) {
            classes.push(styles.critical);
          }

          // Percentages are shown as a filled bar behind the icon instead of a number: thirty
          // numbers in a row is a spreadsheet, thirty bars is a glance. The exact figure is one
          // hover away. Counts keep their number - a bar needs a ceiling, and "284 workers" has
          // none.
          const asBar = vital.format === VitalFormat.Percentage;
          if (asBar) {
            classes.push(styles.barEntry);
          }

          return (
            <Tooltip
              tooltip={`${t(vital.titleKey, vital.title)}: ${formatValue(vital, value, metric)}`}
            >
              <div
                className={classes.join(" ")}
                onClick={() => {
                  // A press that turned into a drag must not also open anything.
                  if (drag.current.moved) {
                    return;
                  }
                  if (configMode) {
                    trigger("seety", "toggleVital", vital.id);
                    return;
                  }
                  if (hasPanel) {
                    setExpanded((open) => (open === vital.id ? null : vital.id));
                    return;
                  }
                  if (vital.clickable) {
                    trigger("seety", "activate", vital.id);
                  }
                }}
              >
                {asBar ? (
                  <div
                    className={styles.bar}
                    style={{ height: `${Math.max(0, Math.min(100, value))}%` }}
                  />
                ) : null}
                <VitalGlyph vital={vital} />
                {asBar ? null : (
                  <span className={styles.value}>{formatValue(vital, value, metric)}</span>
                )}
              </div>
            </Tooltip>
          );
        };

        // Service and hazard rows subscribe to vanilla themselves; every other row already
        // arrived with its value and level computed in C#.
        return vital.bindGroup ? (
          <VanillaEntry key={vital.id} vital={vital} render={row} />
        ) : (
          <React.Fragment key={vital.id}>{row(vital.value, vital.level)}</React.Fragment>
        );
        })}

      <Tooltip
        tooltip={
          configMode
            ? t("Seety.CONFIG_ON", "Done choosing")
            : t(
                "Seety.CONFIG_OFF",
                "Choose which readings to show: click the ones you want"
              )
        }
      >
        <div
          className={`${styles.entry} ${styles.clickable} ${
            configMode ? styles.configOn : ""
          }`}
          onClick={() => {
            if (!drag.current.moved) {
              trigger("seety", "setConfigMode", !configMode);
            }
          }}
        >
          <img className={styles.icon} src="Media/Glyphs/Gear.svg" />
        </div>
      </Tooltip>

      {openVital ? (
        <FloatingWindow
          title={t(openVital.titleKey, openVital.title)}
          onClose={() => setExpanded(null)}
          action={
            openVital.id === "problems" ? (
              <Tooltip
                tooltip={
                  iconsHidden
                    ? t("Seety.ICONS_SHOW_TIP", "Show the notification icons over the city")
                    : t("Seety.ICONS_HIDE_TIP", "Hide the notification icons over the city")
                }
              >
                <span
                  className={styles.windowAction}
                  onClick={() => trigger("seety", "setIconsHidden", !iconsHidden)}
                >
                  {iconsHidden
                    ? t("Seety.ICONS_SHOW", "Show icons")
                    : t("Seety.ICONS_HIDE", "Hide icons")}
                </span>
              </Tooltip>
            ) : undefined
          }
        >
          {openVital.id === DEMOGRAPHICS_ID && demographics ? (
            <DemographicsTable rows={demographics} />
          ) : null}
          {openVital.id === PARKING_ID ? <ParkingList /> : null}
          {openVital.id === POLLUTION_ID ? <PollutionList /> : null}
          {openVital.bindKind === VanillaKind.DemandGroup ? <DemandList /> : null}
          {openVital.factors ? <FactorList binding={openVital.factors} /> : null}
          {openVital.hasHistory && history ? <HistoryChart history={history} /> : null}
          {openVital.id === WORKFORCE_ID && workforce ? (
            <WorkforceTable data={workforce} />
          ) : null}
          {openVital.companions.length > 0 ? (
            <MergedReadings vital={openVital} />
          ) : null}
          {openBreakdown ? (
            openBreakdown.id === "transport" ? (
              <TransportRows rows={openBreakdown.rows} />
            ) : openBreakdown.id === TRAFFIC_ID ? (
              <JamRows rows={openBreakdown.rows} />
            ) : (
              <BreakdownRows rows={openBreakdown.rows} />
            )
          ) : null}
        </FloatingWindow>
      ) : null}
    </div>
  );
};
