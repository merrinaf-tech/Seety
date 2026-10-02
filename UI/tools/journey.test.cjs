const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

test("built traffic panel switches pages by tab, cycles street names, renders journeys and opens versioned lines", async () => {
  const calls = [];
  const bindings = {
    "seety.journeyOn": false,
    "seety.vitals": [{ id: "traffic", title: "Traffic", label: "Traffic", titleKey: "", labelKey: "",
      icon: "", badge: "", value: 1, format: 0, level: 0, clickable: false,
      bindGroup: "", hasHistory: false, factors: "", unit: "", enabled: true, companions: [] }],
    "seety.notifications": [{ id: "traffic", rows: [] }],
  };
  const previousWindow = global.window;
  const dictionary = {
    "Brands.DENNY": "Denny Denims",
    "Routes.SUBWAY": "Subway Line {NUMBER}",
  };
  const localizeName = (value) => {
    if (value.__Type === "names.CustomName") return value.name;
    let text = dictionary[value.nameId] ?? value.nameId;
    for (const [key, arg] of Object.entries(value.nameArgs ?? {}))
      text = text.replace(`{${key}}`, dictionary[arg] ?? arg);
    return text;
  };
  global.window = {
    React: { ...React, useState: (initial) => [initial === null ? "traffic" : initial, () => {}],
      useRef: (value) => ({ current: value }), useCallback: (fn) => fn, useEffect: () => {} },
    "cs2/api": {
      bindValue: (group, name, fallback) => ({ key: `${group}.${name}`, fallback }),
      useValue: ({ key, fallback }) => bindings[key] ?? fallback,
      trigger: (...args) => calls.push(args),
    },
    "cs2/ui": { Tooltip: ({ children }) => children },
    "cs2/l10n": {
      useLocalization: () => ({ translate: (_key, fallback) => fallback, unitSettings: { unitSystem: 0 } }),
      LocalizedEntityName: ({ value }) => localizeName(value),
    },
  };
  try {
    const bundle = await import(pathToFileURL(path.resolve(__dirname, "../dist/Seety.mjs")));
    // The strip is the first registration; the stylesheet owner appended after it renders null.
    let Strip; bundle.default({ append: (_anchor, component) => { Strip ??= component; }, extend: () => {} });
    const panel = () => Strip().props.children.at(-1);
    // The title bar: three page tabs, then the street name switch. Each sits in a Tooltip (the
    // mock renders its child), so the buttons are its children; the tooltip text names them.
    const actions = () => {
      const el = panel().props.action;
      const tree = el.type(el.props);
      return tree.props.children.flat().filter(Boolean)
        .map((tip) => ({ tip: tip.props.tooltip, button: tip.props.children }));
    };
    const click = (index) => {
      calls.length = 0;
      actions()[index].button.props.onClick();
      return calls.splice(0);
    };
    const pressed = () => actions().slice(0, 3).map((a) => a.button.props["aria-pressed"]);
    const icon = (index) => actions()[index].button.props.children.props.src;

    assert.deepEqual(actions().map((a) => a.tip),
      ["Road traffic", "Public transport", "Selected journey"], "street names hidden when unavailable");
    bindings["seety.streetNamesAvailable"] = true;
    assert.equal(actions().length, 4);
    assert.deepEqual(pressed(), [true, false, false], "road traffic is the starting page");
    assert.deepEqual(click(1), [["seety", "setTransitMode", true]]);
    assert.deepEqual(click(2), [["seety", "setJourneyOn", true]]);
    assert.deepEqual(click(0), [], "already on road traffic");

    // From a journey on the transit list: back to roads leaves both, transit only the journey.
    bindings["seety.journeyOn"] = true; bindings["seety.transitMode"] = true;
    assert.deepEqual(pressed(), [false, false, true]);
    assert.deepEqual(click(0), [["seety", "setJourneyOn", false], ["seety", "setTransitMode", false]]);
    assert.deepEqual(click(1), [["seety", "setJourneyOn", false]]);
    assert.deepEqual(click(2), [], "already on the journey");
    bindings["seety.transitMode"] = false;

    // The street name button steps through the three modes, its icon showing the current one.
    for (const [mode, glyph, next] of [[0, "EyeOpen", 1], [1, "Pen", 2], [2, "EyeClosed", 0]]) {
      bindings["seety.streetNames"] = mode;
      assert.match(icon(3), new RegExp(glyph));
      assert.match(actions()[3].tip, /Street names: .*Click to change/);
      assert.deepEqual(click(3), [["seety", "setStreetNames", next]]);
    }
    bindings["seety.streetNames"] = 0;

    const journeyTree = () => {
      const child = panel().props.children.at(-1);
      return child.type(child.props);
    };
    assert.match(renderToStaticMarkup(journeyTree()), /Select a citizen or vehicle/);
    bindings["seety.journey"] = {
      hasSubject: true, subject: "Jane", here: "Main Street", destination: "School", truncated: false,
      legs: [{ kind: "road", name: "Main Street", route: "" }, { kind: "transit", name: "Bus 1", route: "42:3" }],
    };
    const tree = journeyTree();
    const html = renderToStaticMarkup(tree);
    for (const text of ["Jane", "Main Street", "School", "Remaining journey", "Bus 1"]) assert.ok(html.includes(text));
    const buttons = [];
    const walk = (node) => {
      if (Array.isArray(node)) return node.forEach(walk);
      if (!node || typeof node !== "object") return;
      if (node.type === "button") buttons.push(node);
      walk(node.props?.children);
    };
    walk(tree);
    assert.equal(buttons.length, 1, "only transport lines are clickable");
    buttons[0].props.onClick();
    assert.deepEqual(calls.pop(), ["seety", "openJourneyLine", "42:3"]);
    bindings["seety.journey"] = {
      hasSubject: true, subject: "Jane", subjectName: { __Type: "names.CustomName", name: "Jane Smith" },
      here: "Assets.NAME[Station]", hereName: { __Type: "names.CustomName", name: "Magnolia Station" }, hereMetres: 21,
      destination: "Assets.NAME[Commercial_FashionStore]", destinationRef: "50:2",
      destinationName: { __Type: "names.LocalizedName", nameId: "Brands.DENNY" }, truncated: false,
      legs: [{ kind: "transit", name: "Subway Line Tool", route: "42:3", metres: 26,
        displayName: { __Type: "names.FormattedName", nameId: "Routes.SUBWAY", nameArgs: { NUMBER: "7" } } }],
    };
    const namedTree = journeyTree();
    const namedHtml = renderToStaticMarkup(namedTree);
    for (const text of ["Jane Smith", "Magnolia Station", "Denny Denims", "Subway Line 7", "21 m"])
      assert.ok(namedHtml.includes(text), "full name or distance is missing: " + text);
    assert.ok(!namedHtml.includes("Assets.NAME[") && !namedHtml.includes("Subway Line Tool"));
    buttons.length = 0; walk(namedTree);
    assert.equal(buttons.length, 2, "line and named destination remain clickable");
    buttons[1].props.onClick();
    assert.deepEqual(calls.pop(), ["seety", "flyToJourneyPlace", "50:2"]);
    bindings["seety.journey"] = { hasSubject: true, subject: "Bus", here: "", destination: "", legs: [], truncated: true };
    const partial = renderToStaticMarkup(journeyTree());
    assert.match(partial, /Unavailable/); assert.match(partial, /No remaining route/); assert.match(partial, /first part/);
    assert.ok(!partial.includes("Jane") && !partial.includes("School"));
    bindings["seety.journey"] = { hasSubject: false, subject: "", here: "", destination: "", legs: [], truncated: false };
    assert.match(renderToStaticMarkup(journeyTree()), /Select a citizen/);
    // Every tab stays on screen in journey mode, so getting back is one click on the page wanted.
    assert.equal(actions().length, 4, "tabs stay while a journey is showing");
  } finally {
    if (previousWindow === undefined) delete global.window; else global.window = previousWindow;
  }
});
