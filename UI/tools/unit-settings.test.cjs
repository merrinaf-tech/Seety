const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

test("built UI renders metric and imperial readings without a runtime UnitSystem export", async () => {
  let unitSystem = 0;
  let expanded = null;
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
    },
    "cs2/api": {
      bindValue: (group, name, fallback) => ({ key: `${group}.${name}`, fallback }),
      useValue: ({ key, fallback }) => bindings[key] ?? fallback,
      trigger: () => {},
    },
    "cs2/ui": { Tooltip: ({ children }) => children },
    "cs2/l10n": {
      // Match the game's public API: useLocalization exists, UnitSystem does not.
      useLocalization: () => ({
        translate: (_key, fallback) => fallback,
        unitSettings: { unitSystem, timeFormat: 0, temperatureUnit: 0 },
      }),
    },
  };
  try {
    const bundle = await import(pathToFileURL(path.resolve(__dirname, "../dist/Seety.mjs")));
    let Strip;
    bundle.default({ append: (anchor, component) => {
      assert.equal(anchor, "Game");
      Strip = component;
    } });
    assert.equal(typeof Strip, "function");
    for (const system of [0, 1]) {
      unitSystem = system;
      for (const panel of [null, vital.id]) {
        expanded = panel;
        const html = renderToStaticMarkup(React.createElement(Strip));
        const values = panel ? [10, 20, 30, 40] : [10];
        for (const value of values) {
          const number = (system === 0 ? value : value * 2.204622621848776)
            .toLocaleString(undefined, { maximumFractionDigits: 1 });
          assert.ok(html.includes(`${number} ${system === 0 ? "kg" : "lb"}`),
            `Missing ${value} kg converted for system ${system}, panel ${panel}`);
        }
        assert.ok(!html.includes(system === 0 ? " lb" : " kg"));
      }
    }
  } finally {
    if (previousWindow === undefined) delete global.window;
    else global.window = previousWindow;
  }
});
