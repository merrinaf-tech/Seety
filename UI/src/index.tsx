import { ModRegistrar } from "cs2/modding";
import { VitalsStrip } from "mods/vitals-strip";

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
};

export default register;
