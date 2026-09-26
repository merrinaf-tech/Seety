import { getModule } from "cs2/modding";

/**
 * The game's own floating HUD button - the blue square the top-left row is made of - as the class
 * names the game registered for it. Read from the module registry, so the names follow the game
 * across updates instead of being copied from one build of its stylesheet. Looked up once, on
 * first use, because the registry is filled by the game before mods render. Null if a game update
 * moves or renames the module; every caller then leaves the game's buttons as they are.
 */
export type GameButtonClasses = { button: string; selected: string };

let lookup: GameButtonClasses | null | undefined;

export const gameButtonClasses = (): GameButtonClasses | null => {
  if (lookup === undefined) {
    try {
      const classes = getModule(
        "game-ui/common/input/button/floating-icon-button.module.scss",
        "classes"
      );
      lookup =
        classes && typeof classes.button === "string"
          ? { button: classes.button, selected: "selected" }
          : null;
    } catch {
      lookup = null;
    }
  }
  return lookup;
};
