import React from "react";
import { bindValue, useValue } from "cs2/api";
import type { ValueBinding } from "cs2/api";
import styles from "./toolbar-trends.module.scss";

const enabled$ = bindValue<boolean>("seety", "toolbarTrends", false);

interface TrendProps {
  icon?: string;
  trend?: ValueBinding<number>;
  unlimited?: boolean;
}

// Subscribe to the same binding as the native arrow and tooltip, including when it starts at
// zero or is temporarily unavailable. Never replace an unavailable value with a fabricated zero.
const Delta = ({ binding }: { binding: ValueBinding<number> }) => {
  const value = useValue(binding);
  if (typeof value !== "number" || !Number.isFinite(value)) return null;

  const sign = value > 0 ? "+" : value < 0 ? "−" : "";
  const className = `${styles.delta} ${value > 0 ? styles.up : value < 0 ? styles.down : styles.flat}`;
  return <div className={className}>{`${sign}${Math.abs(value).toLocaleString()}`}</div>;
};

/** Add the change inside the native field, preserving the toolbar's direct children and width. */
export function withTrend(Vanilla: React.ComponentType<any>): (props: any) => JSX.Element {
  // The current game exports a function. Leave a future class/memo replacement untouched.
  if (typeof Vanilla !== "function" || Vanilla.prototype?.isReactComponent) {
    return (props: any) => <Vanilla {...props} />;
  }
  const render = Vanilla as (props: TrendProps) => JSX.Element;
  return (props: TrendProps) => {
    // Always invoke the native function, even with trends disabled, so its hooks keep their
    // order. Cloning its returned Field retains the native click handler, tooltip and arrow.
    const field = render(props);
    const enabled = useValue(enabled$);
    if (!enabled || props.unlimited ||
        (props.icon !== "Media/Game/Icons/Citizen.svg" && props.icon !== "Media/Game/Icons/Money.svg") ||
        !props.trend || typeof props.trend.subscribe !== "function" ||
        !React.isValidElement<{ children?: React.ReactNode }>(field) || field.type === React.Fragment) {
      return field;
    }
    return React.cloneElement(field, undefined,
      field.props.children,
      <Delta key="seety-toolbar-delta" binding={props.trend} />,
    );
  };
}
