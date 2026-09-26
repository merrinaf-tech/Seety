import { useEffect } from "react";
import { bindValue, useValue } from "cs2/api";
import { gameButtonClasses } from "./game-button";

const enabled$ = bindValue<boolean>("seety", "darkGameButtons", false);

/** Marks the one style element this owns, so turning the option off removes exactly it. */
const STYLE_ID = "seety-dark-game-buttons";

/**
 * The option "Dark game buttons": the game's blue floating buttons, drawn in the dark blue of the
 * bottom bar instead.
 *
 * It restyles the game's floating-button class itself, so every button built from it changes
 * together - the vanilla rows and any mod that uses the game's own button - and a mod that draws
 * its own blue square does not. Nothing is written anywhere: turning the option off removes the
 * style element and the game's own rules apply again.
 *
 * Only the resting, hover and pressed backgrounds are replaced. The selected state keeps the
 * game's lighter accent, because which infoview or panel is open is information, not decoration.
 * Rules are appended after the game's stylesheet with the same specificity, so they win without
 * !important, and the game's more specific .selected rule still wins over them.
 *
 * #223141 at 85% is --commonDarkBlue, the colour of the bottom bar, written out because combining
 * a var() with an alpha needs color-mix(), which this renderer is not known to support.
 */
export const DarkGameButtons = () => {
  const enabled = useValue(enabled$);

  useEffect(() => {
    const classes = gameButtonClasses();
    const existing = document.getElementById(STYLE_ID);
    if (existing && existing.parentNode) {
      existing.parentNode.removeChild(existing);
    }
    if (!enabled || !classes) {
      if (enabled) console.warn("[Seety] Dark game buttons: the game's button class was not found.");
      return;
    }

    const b = "." + classes.button;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent =
      `${b}{background-color:rgba(34,49,65,0.85)}` +
      `${b}:hover{background-color:rgba(58,80,104,0.92)}` +
      `${b}:active{background-color:rgba(78,106,136,0.95)}`;
    (document.head || document.body).appendChild(style);

    // A trial, so say what happened: how many buttons carry the class, and whether the first one
    // actually took the new colour. Written to the game's UI log.
    const found = document.getElementsByClassName(classes.button);
    const first = found.length > 0 ? (found[0] as HTMLElement) : null;
    const applied = first ? window.getComputedStyle(first).backgroundColor : "(no button on screen)";
    console.log(
      `[Seety] Dark game buttons on: class ${classes.button}, ${found.length} button(s), first background ${applied}.`
    );
  }, [enabled]);

  return null;
};
