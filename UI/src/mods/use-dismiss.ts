import { useEffect, useRef } from "react";

/**
 * One dismissal per press of the game's Tool / Cancel binding.
 *
 * The mouse half reads the player's resolved
 * binding in C# - see CancelKeyUISystem - so somebody who has moved Cancel off the right button
 * does not have to configure Seety as well.
 *
 * Surfaces register only while they are actually open, and exactly one is closed per press: the
 * highest priority, and among equals the most recently opened, so one press never closes two
 * windows at once.
 */
export enum DismissPriority {
  /** A floating window opened from the bar. */
  Window = 10,
}

interface DismissEntry {
  priority: DismissPriority;
  dismiss: () => void;
}

const entries = new Map<number, DismissEntry>();
const listeners = new Set<() => void>();
let nextId = 0;

export const hasDismissEntries = () => entries.size > 0;

export const subscribeDismissEntries = (listener: () => void) => {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
};

const notify = () => listeners.forEach((listener) => listener());

/** Called once by the shared input bridge, not by each open surface. */
export const dismissTop = (): boolean => {
  let chosenId = -1;
  let chosen: DismissEntry | undefined;

  entries.forEach((entry, id) => {
    if (!chosen || entry.priority > chosen.priority ||
        (entry.priority === chosen.priority && id > chosenId)) {
      chosen = entry;
      chosenId = id;
    }
  });

  if (!chosen) {
    return false;
  }

  chosen.dismiss();
  return true;
};

export function useDismissOnCancel(
  active: boolean,
  dismiss: () => void,
  priority: DismissPriority = DismissPriority.Window,
) {
  // Callers pass a fresh closure on every render. Keeping the latest one in a ref means the
  // surface's place in the stack is decided by when it opened, not by when it last re-rendered.
  const latest = useRef(dismiss);
  latest.current = dismiss;

  useEffect(() => {
    if (!active) {
      return;
    }

    const id = ++nextId;
    entries.set(id, { priority, dismiss: () => latest.current() });
    notify();

    return () => {
      entries.delete(id);
      notify();
    };
  }, [active, priority]);
}
