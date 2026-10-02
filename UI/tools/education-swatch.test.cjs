const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

test("education levels carry the game's own colour square, read from the game", async () => {
  // Deliberately not the game's real values: if these show up, they came from the registry and
  // not from the copy the mod keeps for when the export is missing.
  const colours = ["#000001", "#000002", "#000003", "#000004", "#000005"];
  const lookups = [];
  let expanded = null;
  const vital = (id) => ({
    id, title: id, label: id, titleKey: "", labelKey: "", icon: "", badge: "", value: 1,
    format: 1, level: 0, clickable: false, bindGroup: "", hasHistory: false, factors: "",
    unit: "", enabled: true, companions: [],
  });
  const row = (level) => ({
    level, total: 10, children: 1, childStudents: 1, students: 2, seniors: 1, workingAge: 7,
    workers: 5, unemployed: 2, under: 1, outside: 0, commuters: 0, jobs: 6, vacant: 1,
  });
  const bindings = {
    "seety.vitals": [vital("workers"), vital("happiness")],
    "seety.workforce": { rows: ["L0", "L1", "L2", "L3", "L4"].map(row) },
    "seety.demographics": [{ age: "Adults", ageKey: "", levels: [1, 2, 3, 4, 5] }],
  };
  const previousWindow = global.window;
  global.window = {
    React: {
      ...React,
      useState: (initial) => React.useState(initial === null ? expanded : initial),
      useEffect: () => {},
    },
    "cs2/api": {
      bindValue: (group, name, fallback) => ({ key: `${group}.${name}`, fallback }),
      useValue: ({ key, fallback }) => bindings[key] ?? fallback,
      trigger: () => {},
    },
    "cs2/ui": { Tooltip: ({ children }) => children },
    "cs2/l10n": {
      useLocalization: () => ({
        translate: (_key, fallback) => fallback,
        unitSettings: { unitSystem: 0, timeFormat: 0, temperatureUnit: 0 },
      }),
    },
    "cs2/modding": {
      getModule: (modulePath, exportName) => {
        lookups.push(`${modulePath}#${exportName}`);
        if (exportName === "educationPieChartColors") return colours;
        if (modulePath.endsWith("color-legend.module.scss")) return { symbol: "game-symbol" };
        return undefined;
      },
    },
    innerWidth: 1920,
    innerHeight: 1080,
    addEventListener: () => {},
    removeEventListener: () => {},
  };
  try {
    const bundle = await import(pathToFileURL(path.resolve(__dirname, "../dist/Seety.mjs")));
    let Strip;
    bundle.default({ append: (_anchor, component) => { Strip ??= component; }, extend: () => {} });

    const swatches = (html) =>
      [...html.matchAll(/class="([^"]*)" style="background-color:(#[0-9a-f]{6})"/g)]
        .map((m) => [m[1], m[2]]);

    // Workforce: one square per row, in education order, none on the head or total rows.
    expanded = "workers";
    const workforce = swatches(renderToStaticMarkup(React.createElement(Strip)));
    assert.deepEqual(workforce.map(([, colour]) => colour), colours);
    for (const [className] of workforce) {
      assert.match(className, /\bgame-symbol\b/, "the game's own legend square");
    }

    // Demographics: the levels are the column heads.
    expanded = "happiness";
    const demographics = swatches(renderToStaticMarkup(React.createElement(Strip)));
    assert.deepEqual(demographics.map(([, colour]) => colour), colours);

    assert.ok(lookups.includes(
      "game-ui/common/charts/pie-chart/education-pie-chart.tsx#educationPieChartColors"));
  } finally {
    if (previousWindow === undefined) delete global.window;
    else global.window = previousWindow;
  }
});
