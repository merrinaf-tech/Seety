const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

test("built traffic panel toggles journeys, renders empty states and opens versioned lines", async () => {
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
    // The action slot holds two buttons now - the road/trains switch and the journey toggle -
    // so they are picked out by the trigger each one sends rather than by position.
    const actions = () => {
      const slot = panel().props.action;
      const kids = slot.props.children;
      return (Array.isArray(kids) ? kids : [kids]).filter(Boolean);
    };
    const buttonFor = (name) => {
      for (const node of actions()) {
        calls.length = 0;
        node.props.onClick();
        const sent = calls.pop();
        if (sent && sent[1] === name) return { node, sent };
      }
      throw new Error("no button sends " + name);
    };
    const transit = buttonFor("setTransitMode");
    assert.equal(transit.node.props.children, "Public transport");
    assert.deepEqual(transit.sent, ["seety", "setTransitMode", true]);
    const journey = buttonFor("setJourneyOn");
    assert.equal(journey.node.props.children, "Selected journey");
    assert.deepEqual(journey.sent, ["seety", "setJourneyOn", true]);
    bindings["seety.journeyOn"] = true;
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
    // In journey mode the road/trains switch is not drawn: there is no list for it to switch.
    assert.equal(actions().length, 1, "no list switch while a journey is showing");
    const back = buttonFor("setJourneyOn");
    assert.equal(back.node.props.children, "Show traffic jams");
    assert.deepEqual(back.sent, ["seety", "setJourneyOn", false]);
  } finally {
    if (previousWindow === undefined) delete global.window; else global.window = previousWindow;
  }
});
