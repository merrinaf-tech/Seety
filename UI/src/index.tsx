import { ModRegistrar } from "cs2/modding";
import { VitalsStrip } from "mods/vitals-strip";
import { MoneyTrend, PopulationTrend, withTrend } from "mods/toolbar-trends";

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

  // The one place Seety draws outside its own strip: the population and money figures on the
  // bottom bar, which know their own change but only show it on hover.
  //
  // These two paths and export names are the mod's only dependency on the game's internal UI
  // layout, and a game update can move or rename them. When that happens `extend` finds nothing
  // and the fields render exactly as vanilla does - the bar does not break, the addition simply
  // stops appearing. That is why this is a lookup by path rather than a patch of the markup, and
  // why the feature is behind a setting that is off by default.
  moduleRegistry.extend(
    "game-ui/game/components/toolbar/bottom/population-field/population-field.tsx",
    "PopulationField",
    (Vanilla) => withTrend(Vanilla, PopulationTrend),
  );

  moduleRegistry.extend(
    "game-ui/game/components/toolbar/bottom/money-field/money-field.tsx",
    "MoneyField",
    (Vanilla) => withTrend(Vanilla, MoneyTrend),
  );
};

export default register;
