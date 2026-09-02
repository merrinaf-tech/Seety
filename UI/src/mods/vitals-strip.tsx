import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import styles from "./vitals-strip.module.scss";

/** Mirrors the record written by SeetyUISystem.WriteVitals. Keep the two in step. */
interface Vital {
  id: string;
  /** Full name, shown on hover. With icons instead of text, this is what names the row. */
  title: string;
  label: string;
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
  /** Whether the player has this row switched on. Only meaningful in configuration mode. */
  enabled: boolean;
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
const DEMANDS: { id: string; label: string; icon: string; factors: string }[] = [
  { id: "residentialLowDemand",    label: "Residential low",    icon: "Media/Game/Icons/ZoneResidentialLow.svg",    factors: "residentialLowFactors" },
  { id: "residentialMediumDemand", label: "Residential medium", icon: "Media/Game/Icons/ZoneResidentialMedium.svg", factors: "residentialMediumFactors" },
  { id: "residentialHighDemand",   label: "Residential high",   icon: "Media/Game/Icons/ZoneResidentialHigh.svg",   factors: "residentialHighFactors" },
  { id: "commercialDemand",        label: "Commercial",         icon: "Media/Game/Icons/ZoneCommercial.svg",        factors: "commercialFactors" },
  { id: "industrialDemand",        label: "Industrial",         icon: "Media/Game/Icons/ZoneIndustrial.svg",        factors: "industrialFactors" },
  { id: "officeDemand",            label: "Office",             icon: "Media/Game/Icons/ZoneOffice.svg",            factors: "officeFactors" },
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
  /** "jump", "passenger", "cargo", "school:N", or empty. See SeetyUISystem.WriteRow. */
  action: string;
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

const vitals$ = bindValue<Vital[]>("seety", "vitals", []);
const breakdowns$ = bindValue<Breakdown[]>("seety", "notifications", []);
const history$ = bindValue<History>("seety", "history", { label: "", values: [] });
const workforce$ = bindValue<Workforce>("seety", "workforce", { rows: [] });
const visible$ = bindValue<boolean>("seety", "visible", true);
const posX$ = bindValue<number>("seety", "posX", 10);
const posY$ = bindValue<number>("seety", "posY", 90);
const iconsHidden$ = bindValue<boolean>("seety", "iconsHidden", false);
const configMode$ = bindValue<boolean>("seety", "configMode", false);

/** One age band, split across the five education levels. */
interface AgeRow {
  age: string;
  levels: number[];
}

const demographics$ = bindValue<AgeRow[]>("seety", "demographics", []);

/** The five education levels, shortened to fit a column head. */
const LEVEL_NAMES = ["None", "Poor", "Educated", "Well", "Highly"];

/** The row whose window carries the demographics table. Matches the C# side. */
const DEMOGRAPHICS_ID = "happiness";

/** The four school rows. Their windows list the schools of that level. */
const SCHOOL_IDS = ["elementary", "highschool", "college", "university"];

/** The row whose window splits parking into cars and bikes. */
const PARKING_ID = "parking";

const parkingCapacity$ = bindValue<number>("roadsInfo", "parkingCapacity", 0);
const parkedCars$ = bindValue<number>("roadsInfo", "parkedCars", 0);
const bikeParking$ = bindValue<IndicatorValue | null>("bikesInfo", "bikeParkingAvailability", null);

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

/** The row whose window carries the workforce-against-workplaces table. Matches the C# side. */
const WORKFORCE_ID = "workers";

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

function formatValue(vital: Vital, value: number): string {
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

  if (!vital.icon || failed) {
    return <span className={styles.label}>{vital.label}</span>;
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

  switch (row.action) {
    case "jump":
      trigger("seety", "jumpToProblem", row.id);
      return;
    // The panel can be opened after all: GamePanelUISystem exposes
    // game.showTransportationOverviewPanel, taking the tab as an int. Select the mode first so
    // the panel comes up already showing that mode's lines.
    case "passenger":
      trigger("transportationOverview", "setSelectedPassengerType", row.id);
      trigger("game", "showTransportationOverviewPanel", 0);
      return;
    case "cargo":
      trigger("transportationOverview", "setSelectedCargoType", row.id);
      trigger("game", "showTransportationOverviewPanel", 1);
      return;
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
  if (!row.action.startsWith("school:")) {
    return undefined;
  }

  const t = Math.min(1, Math.max(0, (row.count - 50) / 50));
  const r = Math.round(126 + (255 - 126) * t);
  const g = Math.round(214 + (118 - 214) * t);
  const b = Math.round(148 + (105 - 148) * t);
  return { color: `rgb(${r}, ${g}, ${b})` };
}

const BreakdownRows = ({ rows }: { rows: BreakdownRow[] }) => {
  if (rows.length === 0) {
    return <div className={styles.panelEmpty}>Nothing to report</div>;
  }

  return (
    <>
      {rows.map((row) => {
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
            key={row.id}
            tooltip={
              row.action === "jump"
                ? `${row.id} - click to go there`
                : row.action.startsWith("school:")
                ? `${row.id} - click to go there`
                : row.clickable
                ? `${row.id} - click to select this mode in the transport overview`
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
                {row.count}
                {row.action.startsWith("school:") ? "%" : row.suffix}
              </span>
            </div>
          </Tooltip>
        );
      })}
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
  const grab = useRef({ pointerX: 0, pointerY: 0, originX: 0, originY: 0 });

  const onMouseDown = useCallback(
    (event: React.MouseEvent) => {
      event.stopPropagation();
      grab.current = {
        pointerX: event.clientX,
        pointerY: event.clientY,
        originX: pos.x,
        originY: pos.y,
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
      setPos({
        x: Math.max(0, grab.current.originX + (event.clientX - grab.current.pointerX)),
        y: Math.max(0, grab.current.originY + (event.clientY - grab.current.pointerY)),
      });
    };
    const onUp = () => setDragging(false);
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
    return () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
  }, [dragging]);

  return (
    <div
      className={styles.window}
      style={{ left: `${pos.x}rem`, top: `${pos.y}rem` }}
      // The strip below is draggable too; without this a drag inside the window moves both.
      onMouseDown={(e) => e.stopPropagation()}
    >
      <div className={styles.windowBar} onMouseDown={onMouseDown}>
        <span className={styles.windowTitle}>{title}</span>
        {action}
        <span className={styles.windowClose} onClick={onClose}>
          {/* Drawn rather than typed: the game's font has no glyph for a multiplication sign,
              so the character came out as an empty rectangle. */}
          <svg viewBox="0 0 12 12" className={styles.windowCloseIcon}>
            <path
              d="M2 2 L10 10 M10 2 L2 10"
              stroke="currentColor"
              strokeWidth="1.6"
              strokeLinecap="round"
            />
          </svg>
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
  // Subscribing here is what makes vanilla compute these: the Workplaces infoview system skips
  // its job unless something is listening. See VanillaBinding on the C# side.

  const rows = data?.rows ?? [];
  if (rows.length === 0) {
    return null;
  }

  // Left of the divider every column counts citizens; right of it they count jobs. Mixing the
  // two without a line between them was the single thing that made the table hard to read.
  const columns: [string, (r: WorkforceRow, i: number) => number, boolean][] = [
    ["Total", (r) => r.total, false],
    // Kids and Student overlapped: a child at school appeared in both. Kids is now the ones who
    // are not studying, so the columns add up.
    ["Kids", (r) => Math.max(0, r.children - r.students), false],
    ["Student", (r) => r.students, false],
    ["Old", (r) => r.seniors, false],
    ["Adults", (r) => r.workingAge, false],
    ["Employed", (r) => r.workers, false],
    ["Idle", (r) => r.unemployed, false],
    ["Under", (r) => r.under, false],
    ["Out", (r) => r.outside, false],
    ["In", (r) => r.commuters, false],
    // "Posts" was opaque. These are jobs, not people: how many exist at this level, and how many
    // of them nobody is doing.
    ["Jobs", (r) => r.jobs, true],
    ["Vacant", (r) => r.vacant, false],
  ];

  const totals = columns.map(([, get]) =>
    rows.reduce((sum, r, i) => sum + get(r, i), 0)
  );

  const cellClass = (divider: boolean, highlight: boolean) =>
    [styles.tableCell, divider ? styles.tableDivider : "", highlight ? styles.tableShort : ""]
      .filter(Boolean)
      .join(" ");

  return (
    <div className={styles.table}>
      <div className={`${styles.tableRow} ${styles.tableHead}`}>
        <span className={styles.tableLevel}>Education</span>
        {columns.map(([name, , divider]) => (
          <span key={name} className={cellClass(divider, false)}>
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
            {columns.map(([name, get, divider], c) => (
              <span
                key={name}
                className={cellClass(divider, short_ && c === columns.length - 1)}
              >
                {get(row, i).toLocaleString()}
              </span>
            ))}
          </div>
        );
      })}

      <div className={`${styles.tableRow} ${styles.tableTotal}`}>
        <span className={styles.tableLevel}>Total</span>
        {totals.map((value, i) => (
          <span key={i} className={cellClass(columns[i][2], false)}>
            {value.toLocaleString()}
          </span>
        ))}
      </div>

      <div className={styles.tableNote}>
        Everything left of the line counts citizens; everything right of it counts jobs. Counted
        citizen by citizen, tourists excluded. Kids are the ones not at school. Idle is anyone of
        working age without a job in the city; Under holds a job below their education; Out lives
        here and works outside; In commutes in from outside and is in no other column. Jobs and
        Vacant come from the game&apos;s own Workplaces panel.
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

  if (factors.length === 0) {
    return <div className={styles.panelEmpty}>No factors reported</div>;
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
const DemandList = () => {
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
            <span className={styles.panelName}>{d.label}</span>
            <span className={styles.value}>
              {Math.round((values[i] ?? 0) * 100)}%
            </span>
          </div>
          {open === d.id ? <FactorList binding={d.factors} /> : null}
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
  if (rows.length === 0) {
    return null;
  }

  const rowTotal = (r: AgeRow) => (r.levels ?? []).reduce((a, b) => a + b, 0);
  const population = rows.reduce((sum, r) => sum + rowTotal(r), 0);

  return (
    <div className={styles.table}>
      <div className={`${styles.tableRow} ${styles.tableHead}`}>
        <span className={styles.tableLevel}>Age</span>
        {LEVEL_NAMES.map((name) => (
          <span key={name} className={styles.tableCell}>
            {name}
          </span>
        ))}
        <span className={`${styles.tableCell} ${styles.tableDivider}`}>Total</span>
      </div>

      {rows.map((row) => {
        const total = rowTotal(row);
        const share = population > 0 ? (total / population) * 100 : 0;

        return (
          <div key={row.age} className={styles.tableRow}>
            <span className={styles.tableLevel}>
              {row.age}
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
 * Parking split in two, because the game reports it in two places.
 *
 * Cars come from the Roads panel as a capacity and a count; bikes come from the Bikes panel as an
 * indicator with no raw numbers behind it, so that row shows a percentage only. Both are read as
 * room left, matching the row on the strip.
 */
const ParkingList = () => {
  const capacity = useValue(parkingCapacity$);
  const parked = useValue(parkedCars$);
  const bikes = useValue(bikeParking$);

  const carsFree = capacity > 0 ? Math.max(0, 100 - (parked / capacity) * 100) : 0;
  const bikesFree =
    bikes && bikes.max > bikes.min
      ? ((bikes.current - bikes.min) / (bikes.max - bikes.min)) * 100
      : 0;

  return (
    <div className={styles.table}>
      <div
        className={`${styles.panelRow} ${styles.clickable}`}
        onClick={() => trigger("seety", "openInfoview", "Roads")}
      >
        <img className={styles.icon} src="Media/Game/Icons/Parking.svg" />
        <span className={styles.panelName}>
          Cars {capacity > 0 ? `- ${parked.toLocaleString()} of ${Math.round(capacity).toLocaleString()}` : ""}
        </span>
        <span className={styles.value}>{Math.round(carsFree)}%</span>
      </div>

      <div
        className={`${styles.panelRow} ${styles.clickable}`}
        onClick={() => trigger("seety", "openInfoview", "Bicycles")}
      >
        <img className={styles.icon} src="Media/Game/Icons/Bicycles.svg" />
        <span className={styles.panelName}>Bikes</span>
        <span className={styles.value}>{Math.round(bikesFree)}%</span>
      </div>

      <div className={styles.tableNote}>
        Room left, not spaces taken. Cars come from the game&apos;s Roads panel with the raw counts;
        bikes come from the Bikes panel, which reports only a level. Click either to open its map
        view.
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

  const [pos, setPos] = useState({ x: savedX, y: savedY });
  const [dragging, setDragging] = useState(false);
  // Which row is expanded, or null. One at a time: two open panels would overlap.
  const [expanded, setExpanded] = useState<string | null>(null);

  // C# only fetches a series for the row that is actually open, so it has to be told.
  useEffect(() => {
    trigger("seety", "expand", expanded ?? "");
  }, [expanded]);

  const drag = useRef({ pointerX: 0, pointerY: 0, originX: 0, originY: 0, moved: false });
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
      drag.current = {
        pointerX: event.clientX,
        pointerY: event.clientY,
        originX: pos.x,
        originY: pos.y,
        moved: false,
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
      const dx = event.clientX - drag.current.pointerX;
      const dy = event.clientY - drag.current.pointerY;

      if (!drag.current.moved && Math.abs(dx) + Math.abs(dy) < DRAG_THRESHOLD) {
        return;
      }
      drag.current.moved = true;

      // Keep a grabbable part of the strip on screen whatever the player does with it.
      const width = elementRef.current?.offsetWidth ?? 0;
      const height = elementRef.current?.offsetHeight ?? 0;
      const maxX = Math.max(0, window.innerWidth - width);
      const maxY = Math.max(0, window.innerHeight - height);

      setPos({
        x: Math.min(Math.max(0, drag.current.originX + dx), maxX),
        y: Math.min(Math.max(0, drag.current.originY + dy), maxY),
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

  const openVital = expanded ? vitals.find((v) => v.id === expanded) : undefined;
  const openBreakdown = expanded
    ? (breakdowns ?? []).find((b) => b.id === expanded)
    : undefined;

  return (
    <div
      ref={elementRef}
      className={[
        styles.strip,
        dragging ? styles.dragging : "",
        configMode ? styles.stripConfig : "",
      ]
        .filter(Boolean)
        .join(" ")}
      style={{ left: `${pos.x}rem`, top: `${pos.y}rem` }}
      onMouseDown={onMouseDown}
    >
      {configMode ? (
        <span className={styles.configBanner}>Choosing what to show</span>
      ) : null}

      {vitals
        .filter((vital) => configMode || vital.enabled)
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
            SCHOOL_IDS.indexOf(vital.id) >= 0 ||
            vital.id === WORKFORCE_ID);

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
            <Tooltip tooltip={`${vital.title}: ${formatValue(vital, value)}`}>
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
                  <span className={styles.value}>{formatValue(vital, value)}</span>
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
            ? "Done choosing"
            : "Choose which readings to show: click the ones you want"
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
          title={openVital.title}
          onClose={() => setExpanded(null)}
          action={
            openVital.id === "problems" ? (
              <Tooltip
                tooltip={
                  iconsHidden
                    ? "Show the notification icons over the city"
                    : "Hide the notification icons over the city"
                }
              >
                <span
                  className={styles.windowAction}
                  onClick={() => trigger("seety", "setIconsHidden", !iconsHidden)}
                >
                  {iconsHidden ? "Show icons" : "Hide icons"}
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
          {openBreakdown ? <BreakdownRows rows={openBreakdown.rows} /> : null}
        </FloatingWindow>
      ) : null}
    </div>
  );
};
