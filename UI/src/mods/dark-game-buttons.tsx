import { useEffect } from "react";
import { bindValue, useValue } from "cs2/api";
import { gameButtonClasses } from "./game-button";

const enabled$ = bindValue<boolean>("seety", "darkGameButtons", false);

/** Marks the one style element this owns, so turning the option off removes exactly it. */
const STYLE_ID = "seety-dark-game-buttons";

/**
 * The option "Dark game buttons": the game's blue floating buttons, drawn like the fields of the
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
 * The resting colour is --toolbarBottomBgColor, the bottom bar's own background (#1c2936,
 * measured on screen to match). --toolbarFieldColor was tried first and is the darker inset of the
 * bar's fields - near black in the default theme, too dark next to the bar. Hover and pressed are
 * the game's panel-button shades of the same blue. Tried in game on 2026-09-26: 37 buttons
 * carried the class and all of them changed.
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
      `${b}{background-color:var(--toolbarBottomBgColor)}` +
      `${b}:hover{background-color:var(--panelButtonColor-hover)}` +
      `${b}:active{background-color:var(--panelButtonColor-active)}`;
    (document.head || document.body).appendChild(style);
  }, [enabled]);

  return null;
};
