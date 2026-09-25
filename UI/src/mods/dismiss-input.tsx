import React, { useEffect, useRef, useState } from "react";
import { bindValue, useValue } from "cs2/api";
import { InputActionConsumer } from "cs2/input";
import { dismissTop, hasDismissEntries, subscribeDismissEntries } from "./use-dismiss";

const cancelRevision$ = bindValue<number>("seety", "cancelRevision", 0);

/**
 * Follows the player's actual Tool / Cancel binding, by both routes it can arrive on.
 *
 * The C# side reads that action out of InputManager and bumps a revision for every press of a
 * mouse button bound to it; this turns a new revision into one dismissal. Holding the previous
 * value in a ref matters: without it, a press made while nothing was open would close the first
 * thing opened afterwards.
 *
 * Back covers Escape and the gamepad back button, which already reach the UI on their own. The
 * registry picks one surface per press, so a window over configuration mode never closes both.
 *
 * Rendered only while something is open, so Seety consumes nothing when it has nothing to close.
 */
export const DismissInput = () => {
  const [active, setActive] = useState(hasDismissEntries);
  const revision = useValue(cancelRevision$);
  const previous = useRef(revision);

  useEffect(() => subscribeDismissEntries(() => setActive(hasDismissEntries())), []);

  useEffect(() => {
    if (revision !== previous.current) {
      previous.current = revision;
      dismissTop();
    }
  }, [revision]);

  if (!active) {
    return null;
  }

  return <InputActionConsumer actions={{ Back: dismissTop }} ignoreFocusState />;
};
