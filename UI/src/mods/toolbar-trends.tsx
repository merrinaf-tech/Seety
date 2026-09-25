import React from "react";
import { bindValue, useValue } from "cs2/api";
import styles from "./toolbar-trends.module.scss";

/**
 * The change beside the population and money figures on the vanilla bottom bar.
 *
 * The game already computes both numbers and publishes them: `toolbarBottom.populationDelta` and
 * `toolbarBottom.moneyDelta` are what its own tooltip shows when you hover the figure. Seety
 * reads those bindings rather than deriving anything from the statistics series, for the usual
 * reason - a number that disagrees with the game's own panel is worse than no number - and here
 * it also means the figure cannot drift from the tooltip sitting a few pixels away from it.
 *
 * Deriving it was the alternative and it would have been worse in a way that is not obvious:
 * CityStatisticsSystem samples 32 times a day, so the finest window it can answer is 45 minutes
 * of game time, and any "per hour" label built on it would have been a rounding of something the
 * game already knows exactly.
 *
 * This is the only part of Seety that draws outside its own strip. It extends two vanilla
 * components, which ties it to their module paths - see index.tsx - so it is off unless the
 * player turns it on, and turning it off is the whole of the fix if a game update moves them.
 */

const populationDelta$ = bindValue<number>("toolbarBottom", "populationDelta", 0);
const moneyDelta$ = bindValue<number>("toolbarBottom", "moneyDelta", 0);
const enabled$ = bindValue<boolean>("seety", "toolbarTrends", false);

/**
 * The delta, signed.
 *
 * It used to render nothing when the change was zero, on the grounds that a "+0" beside a figure
 * that has not moved is noise. That was wrong for a reason worth keeping: a setting whose effect
 * is invisible in the common case is indistinguishable from a setting that does not work, and
 * that is exactly how it was reported. A steady city now reads "0", which is a fact.
 */
const Delta = ({ value }: { value: number }) => {
  const enabled = useValue(enabled$);

  if (!enabled) {
    return null;
  }

  const sign = value > 0 ? "+" : value < 0 ? "−" : "";
  const className = `${styles.delta} ${value > 0 ? styles.up : value < 0 ? styles.down : styles.flat}`;

  // One string rather than neighbouring nodes: this renderer drops the leading space between
  // adjacent pieces of text, which is the bug that once turned "0 / 960" into "0/ 960".
  return <div className={className}>{`${sign}${Math.abs(value).toLocaleString()}`}</div>;
};

export const PopulationTrend = () => <Delta value={useValue(populationDelta$)} />;
export const MoneyTrend = () => <Delta value={useValue(moneyDelta$)} />;

/**
 * Wraps a vanilla toolbar field so the delta rides along beside it.
 *
 * The vanilla component is rendered untouched and the label is a sibling, rather than reaching
 * inside to place it next to the digits. Reaching inside would mean depending on that component's
 * internal markup as well as on its path, and the internals are the half far more likely to be
 * rearranged by a patch without anyone noticing.
 */
export function withTrend(
  Vanilla: React.ComponentType<any>,
  Trend: React.ComponentType,
): (props: any) => JSX.Element {
  // Returns a plain function component, not a ComponentType: that is what ModuleRegistryExtend
  // declares, and a class component would satisfy the looser type while failing the registry's.
  //
  // A fragment, not a wrapping element. Wrapping the vanilla field put an extra box between it
  // and the toolbar's own layout, which is a second way this can fail invisibly - the label is
  // rendered but the row gives it no room. As siblings, both sit in the row the game laid out.
  return (props: any) => (
    <>
      <Vanilla {...props} />
      <Trend />
    </>
  );
}
