const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

test("transport passenger switch shows line stops, excludes cargo and sends versioned clicks", async () => {
  const calls = [];
  const vital = { id: "transport", title: "Transport", label: "Transport", titleKey: "", labelKey: "",
    icon: "", badge: "", value: 1, format: 0, level: 0, clickable: false, bindGroup: "",
    hasHistory: false, factors: "", unit: "", enabled: true, companions: [] };
  const row = (id, action, unit = "") => ({ id, action, unit, icon: "", count: 5, total: 20,
    level: 0, clickable: true, suffix: "", lineName: null });
  const bindings = {
    "seety.vitals": [vital],
    "seety.notifications": [{ id: "transport", rows: [row("Bus", "passenger:Bus"), row("Cargo train", "cargo:Train", "weight")] }],
    "seety.transportStopsMode": false,
    "seety.waitingStops": [],
  };
  const previousWindow = global.window;
  global.window = {
    React: { ...React, useState: (initial) => [initial === null ? "transport" : initial, () => {}],
      useRef: (value) => ({ current: value }), useCallback: (fn) => fn, useEffect: () => {} },
    "cs2/api": { bindValue: (group, name, fallback) => ({ key: `${group}.${name}`, fallback }),
      useValue: ({ key, fallback }) => bindings[key] ?? fallback, trigger: (...args) => calls.push(args) },
    "cs2/ui": { Tooltip: ({ children }) => children },
    "cs2/l10n": { useLocalization: () => ({ translate: (_key, fallback) => fallback, unitSettings: { unitSystem: 0 } }),
      LocalizedEntityName: ({ value }) => value.name },
  };
  try {
    const bundle = await import(pathToFileURL(path.resolve(__dirname, "../dist/Seety.mjs")));
    let Strip;
    bundle.default({ append: (_anchor, component) => { Strip ??= component; }, extend: () => {} });
    const tree = () => {
      const node = Strip().props.children.at(-1).props.children.at(-1);
      return node.type(node.props);
    };
    const html = () => renderToStaticMarkup(tree());
    const nodes = (root, predicate) => {
      const result = [];
      const walk = (node) => {
        if (Array.isArray(node)) return node.forEach(walk);
        if (!node || typeof node !== "object") return;
        if (predicate(node)) result.push(node);
        walk(node.props?.children);
      };
      walk(root); return result;
    };
    assert.match(html(), /Busiest stops/);
    assert.match(html(), /Bus/);
    nodes(tree(), (node) => node.type === "button")[0].props.onClick();
    assert.deepEqual(calls.pop(), ["seety", "setTransportStopsMode", true]);
    bindings["seety.transportStopsMode"] = true;
    bindings["seety.waitingStops"] = [
      { id: "12:3", name: { __Type: "names.CustomName", name: "Central Platform" },
        lineName: { __Type: "names.CustomName", name: "Bus 7" }, colour: "#ffcc00", number: 7,
        count: 180 },
      { id: "15:2", name: null, lineName: null, colour: "", number: 0, count: 25 },
    ];
    const rendered = html();
    for (const text of ["By transport mode", "Refresh", "Central Platform", "Bus 7", "180 waiting", "Unnamed stop"])
      assert.ok(rendered.includes(text), "missing " + text);
    const badges = nodes(tree(), (node) => node.props.style?.backgroundColor === "#ffcc00");
    assert.equal(badges.length, 1, "only a stop with a line colour has a badge");
    assert.equal(badges[0].props.children, 7);
    assert.equal(badges[0].props.style.color, "rgb(0, 0, 0)", "light line colours use dark badge text");
    assert.ok(!rendered.includes(">Bus<"), "passenger modes are replaced by stops");
    assert.ok(!rendered.includes("Cargo train"), "cargo lines are hidden in the passenger stop view");
    const clickable = nodes(tree(), (node) => node.type === "div" && node.props.onClick);
    assert.equal(clickable.length, 2, "every passenger stop can open its line");
    assert.equal(clickable[0].props.title, "Open transport line");
    clickable[0].props.onClick();
    assert.deepEqual(calls.pop(), ["seety", "openWaitingStopLine", "12:3"]);
    const buttons = nodes(tree(), (node) => node.type === "button");
    buttons[1].props.onClick();
    assert.deepEqual(calls.pop(), ["seety", "refreshWaitingStops"]);
    buttons[0].props.onClick();
    assert.deepEqual(calls.pop(), ["seety", "setTransportStopsMode", false]);
    bindings["seety.waitingStops"] = [];
    bindings["seety.notifications"][0].rows = [];
    assert.match(html(), /No passengers waiting/);
    assert.equal(nodes(tree(), (node) => node.type === "button").length, 2,
      "the switch and refresh remain available even with no vehicles or waiting passengers");
  } finally {
    if (previousWindow === undefined) delete global.window; else global.window = previousWindow;
  }
});
