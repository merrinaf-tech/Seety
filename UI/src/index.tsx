import { ModRegistrar } from "cs2/modding";
import { VitalsStrip } from "mods/vitals-strip";
import { withTrend } from "mods/toolbar-trends";

/**
 * Seety mounts a single strip over the game HUD.
 *
 * The anchor is "Game", not "GameTopLeft", because the strip is draggable: mounted in the
 * top-left container it sat in that container's flow, wedged between the vanilla buttons and the
 * other mods' ones, and could not be moved out. From the full-screen anchor it positions itself.
 *
 * Everything the strip shows comes from the C# side over the "seety" binding group; the UI holds
 * no state of its own beyond React's. Clicking an entry sends its id back and the C# side opens
 * the matching vanilla infoview.
 */
const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("Game", VitalsStrip);

  // PopulationField and MoneyField have setters that assign to const declarations in the
  // game bundle. Extending either throws and aborts registration of later mods' HUD buttons,
  // even with trends disabled. StatFieldTrend is a mutable export shared by both fields.
  try {
    moduleRegistry.extend(
      "game-ui/game/components/toolbar/components/stat-field/stat-field.tsx",
      "StatFieldTrend",
      withTrend,
    );
  } catch (error) {
    // A missing hook after a game update must not prevent other mods from registering.
    console.warn("[Seety] Bottom-bar trends unavailable:", error);
  }
};

export default register;
