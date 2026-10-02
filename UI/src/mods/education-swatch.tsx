import React from "react";
import { getModule } from "cs2/modding";
import styles from "./vitals-strip.module.scss";

/**
 * The game's own colours for the five education levels, uneducated to highly educated, as its
 * education pie chart and workplace charts draw them. Read from the module registry so they follow
 * the game; the copy below is the same five values from game 1.6.2f1, used if a game update moves
 * the export.
 */
const FALLBACK_COLOURS = ["#808080", "#b09868", "#368a2e", "#b981c0", "#5796d1"];

let colours: string[] | undefined;

const educationColours = (): string[] => {
  if (colours === undefined) {
    try {
      const found = getModule(
        "game-ui/common/charts/pie-chart/education-pie-chart.tsx",
        "educationPieChartColors"
      );
      colours =
        Array.isArray(found) && found.length >= 5 && found.every((c) => typeof c === "string")
          ? found
          : FALLBACK_COLOURS;
    } catch {
      colours = FALLBACK_COLOURS;
    }
  }
  return colours;
};

/**
 * The square the game draws beside each entry of a chart legend: the `symbol` class of its
 * color-legend stylesheet. Null if a game update renames it; .levelSwatchShape then draws the same
 * square from a copy of the rule.
 */
let symbolClass: string | null | undefined;

const legendSymbolClass = (): string | null => {
  if (symbolClass === undefined) {
    try {
      const classes = getModule("game-ui/common/charts/legends/color-legend.module.scss", "classes");
      symbolClass = classes && typeof classes.symbol === "string" ? classes.symbol : null;
    } catch {
      symbolClass = null;
    }
  }
  return symbolClass ?? null;
};

/** The game's coloured square for one education level, 0 (uneducated) to 4 (highly educated). */
export const EducationSwatch = ({ level }: { level: number }) => {
  const colour = educationColours()[level];
  if (!colour) {
    return null;
  }
  const symbol = legendSymbolClass();
  return (
    <span
      className={[styles.levelSwatch, symbol ?? styles.levelSwatchShape].join(" ")}
      style={{ backgroundColor: colour }}
    />
  );
};
