const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

test("built UI renders metric and imperial readings without a runtime UnitSystem export", async () => {
  let unitSystem = 0;
  let expanded = null;
  let effects = [];
  const triggers = [];
  const vital = {
    id: "test-weight", title: "Weight", label: "Weight", titleKey: "", labelKey: "",
    icon: "", badge: "", value: 10, format: 0, level: 0, clickable: false,
    bindGroup: "", bindSupply: "", bindDemand: "", bindSupply2: "", bindDemand2: "",
    bindKind: 3, warning: 0, critical: 0, lowIsBad: false, hasThreshold: false,
    hasHistory: false, invert: false, factors: "", unit: "weight", enabled: true,
    companions: [],
  };
  vital.companions = [{ ...vital, id: "companion", value: 20 }];
  const bindings = {
    "seety.vitals": [vital],
    "seety.notifications": [{ id: vital.id, rows: [{
      id: "Breakdown", icon: "", count: 30, total: 40, level: 0,
      clickable: false, suffix: "", action: "", unit: "weight",
    }] }],
  };
  const previousWindow = global.window;
  global.window = {
    React: {
      ...React,
      // Select the expanded panel while retaining React's real rendering and other hooks.
      useState: (initial) => React.useState(initial === null ? expanded : initial),
      useEffect: (effect) => { effects.push(effect); },
    },
    "cs2/api": {
      bindValue: (group, name, fallback) => ({ key: `${group}.${name}`, fallback }),
      useValue: ({ key, fallback }) => bindings[key] ?? fallback,
      trigger: (...args) => triggers.push(args),
    },
    "cs2/ui": { Tooltip: ({ children }) => children },
    "cs2/l10n": {
      // Match the game's public API: useLocalization exists, UnitSystem does not.
      useLocalization: () => ({
        translate: (_key, fallback) => fallback,
        unitSettings: { unitSystem, timeFormat: 0, temperatureUnit: 0 },
      }),
    },
    innerWidth: 1920,
    innerHeight: 1080,
    addEventListener: () => {},
    removeEventListener: () => {},
  };
  try {
    const bundle = await import(pathToFileURL(path.resolve(__dirname, "../dist/Seety.mjs")));
    let Strip;
    const extended = [];
    bundle.default({
      append: (anchor, component) => {
        assert.equal(anchor, "Game");
        Strip = component;
      },
      // Only the shared, mutable export may be extended; the field-specific setters throw.
      extend: (modulePath, exportName) => { extended.push(modulePath + "#" + exportName); },
    });
    assert.deepEqual(extended, [
      "game-ui/game/components/toolbar/components/stat-field/stat-field.tsx#StatFieldTrend",
    ]);
    assert.equal(typeof Strip, "function");
    for (const system of [0, 1]) {
      unitSystem = system;
      for (const panel of [null, vital.id]) {
        expanded = panel;
        const html = renderToStaticMarkup(React.createElement(Strip));
        // Converted here rather than by calling the mod's own formatter: a test that reuses the
        // code under test only proves it agrees with itself.
        const shown = (kg) => (system === 0 ? kg : kg * 2.204622621848776)
          .toLocaleString(undefined, { maximumFractionDigits: 1 });
        const unit = system === 0 ? "kg" : "lb";

        // A lone reading names its unit.
        assert.ok(html.includes(`${shown(10)} ${unit}`),
          `Missing the strip reading for system ${system}, panel ${panel}`);

        if (panel) {
          assert.ok(html.includes(`${shown(20)} ${unit}`),
            `Missing the companion reading for system ${system}`);
          // A pair shares one scale and names the unit once, so the two halves can be compared
          // without converting between them.
          assert.ok(html.includes(`${shown(30)} / ${shown(40)} ${unit}`),
            `Missing the paired reading for system ${system}`);
        }

        assert.ok(!html.includes(system === 0 ? " lb" : " kg"));
      }
    }

    // Hiding or configuring the HUD must also release its expensive backend subscription.
    for (const mode of ["open", "hidden", "configuring", "removed", "disabled"]) {
      expanded = vital.id;
      bindings["seety.visible"] = mode !== "hidden";
      bindings["seety.configMode"] = mode === "configuring";
      bindings["seety.vitals"] = mode === "removed" ? [] : [vital];
      vital.enabled = mode !== "disabled";
      effects = [];
      triggers.length = 0;
      const html = renderToStaticMarkup(React.createElement(Strip));
      const cleanups = effects.map((effect) => effect());
      assert.deepEqual(triggers.filter((call) => call[1] === "expand"),
        [["seety", "expand", mode === "open" ? vital.id : ""]], mode);
      if (mode !== "open") assert.ok(!html.includes("Breakdown"), mode);
      for (const cleanup of cleanups) if (typeof cleanup === "function") cleanup();
      assert.deepEqual(triggers.at(-1), ["seety", "expand", ""], "unmount closes the panel");
    }

    vital.id = "elementary";
    vital.enabled = true;
    vital.companions = [];
    expanded = vital.id;
    bindings["seety.visible"] = true;
    bindings["seety.configMode"] = false;
    bindings["seety.vitals"] = [vital];
    bindings["seety.notifications"] = [{ id: vital.id, rows: [] }];
    assert.match(renderToStaticMarkup(React.createElement(Strip)), /Nothing to report/);
  } finally {
    if (previousWindow === undefined) delete global.window;
    else global.window = previousWindow;
  }
});
