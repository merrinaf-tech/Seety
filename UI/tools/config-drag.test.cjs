const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");

test("the built strip only moves in configuration mode and cancels interrupted drags", async () => {
  const slots = [];
  let cursor = 0, pending = [], dirty = false, tree;
  const listeners = new Map();
  const calls = [];
  const bindings = {
    "seety.visible": true, "seety.configMode": false,
    "seety.posX": 100, "seety.posY": 100,
    "seety.vitals": [{
      id: "test", title: "Test", label: "Test", titleKey: "", labelKey: "",
      icon: "", badge: "", value: 1, format: 0, level: 0, clickable: true,
      bindGroup: "", hasHistory: false, factors: "", unit: "", enabled: true,
      companions: [],
    }],
  };
  const previousWindow = global.window;
  global.window = {
    React: {
      ...React,
      useState(initial) {
        const index = cursor++;
        if (!(index in slots)) slots[index] = initial;
        return [slots[index], (next) => {
          const value = typeof next === "function" ? next(slots[index]) : next;
          if (!Object.is(value, slots[index])) { slots[index] = value; dirty = true; }
        }];
      },
      useRef(initial) {
        const index = cursor++;
        return slots[index] ?? (slots[index] = { current: initial });
      },
      useCallback: (callback) => callback,
      useEffect(effect, deps) {
        const index = cursor++;
        const old = slots[index];
        if (!old || deps.some((dep, i) => !Object.is(dep, old.deps[i]))) {
          pending.push(() => {
            old?.cleanup?.();
            slots[index] = { deps, cleanup: effect() };
          });
        }
      },
    },
    "cs2/api": {
      bindValue: (group, name, fallback) => ({ key: `${group}.${name}`, fallback }),
      useValue: ({ key, fallback }) => bindings[key] ?? fallback,
      trigger: (...args) => {
        calls.push(args);
        if (args[1] === "setPosition") {
          bindings["seety.posX"] = args[2]; bindings["seety.posY"] = args[3];
        }
      },
    },
    "cs2/ui": { Tooltip: ({ children }) => children },
    "cs2/l10n": { useLocalization: () => ({
      translate: (_key, fallback) => fallback, unitSettings: { unitSystem: 0 },
    }) },
    innerWidth: 1920, innerHeight: 1080,
    addEventListener: (name, fn) => listeners.set(name, fn),
    removeEventListener: (name, fn) => { if (listeners.get(name) === fn) listeners.delete(name); },
  };
  try {
    const bundle = await import(pathToFileURL(path.resolve(__dirname, "../dist/Seety.mjs")));
    let Strip;
    bundle.default({ append: (_anchor, component) => { Strip = component; } });
    const render = () => {
      let passes = 0;
      do {
        assert.ok(++passes < 10, "render settles");
        cursor = 0; pending = []; dirty = false;
        tree = Strip();
        for (const effect of pending) effect();
      } while (dirty);
      return tree;
    };
    const down = (button = 0) => {
      tree.props.onMouseDown({ button, clientX: 200, clientY: 200 }); render();
    };
    const move = () => { listeners.get("mousemove")?.({ clientX: 240, clientY: 240 }); render(); };
    const up = () => { listeners.get("mouseup")?.(); render(); };
    const saved = () => calls.filter((call) => call[1] === "setPosition");
    const entry = () => tree.props.children[1][0].props.children.props.children;

    render();
    const original = { ...tree.props.style };
    down(); move(); up();
    assert.deepEqual(tree.props.style, original);
    assert.equal(listeners.size, 0);
    assert.equal(saved().length, 0);
    entry().props.onClick();
    assert.deepEqual(calls.at(-1), ["seety", "activate", "test"]);

    bindings["seety.configMode"] = true; render();
    assert.match(tree.props.children[0].props.children, /Choose readings or move the bar/);
    down(2);
    assert.equal(listeners.size, 0, "right button does not drag");
    down(); up(); entry().props.onClick();
    assert.deepEqual(calls.at(-1), ["seety", "toggleVital", "test"]);
    down(); move();
    assert.notDeepEqual(tree.props.style, original);
    up();
    assert.equal(saved().length, 1);
    const count = calls.length;
    entry().props.onClick();
    assert.equal(calls.length, count, "drag does not also toggle a reading");

    bindings["seety.configMode"] = false; render();
    down(); entry().props.onClick();
    assert.deepEqual(calls.at(-1), ["seety", "activate", "test"], "click after a drag still works");
    const lastSaved = { ...tree.props.style };
    for (const interruptedBinding of ["seety.configMode", "seety.visible"]) {
      bindings["seety.configMode"] = true; render();
      down(); move();
      bindings[interruptedBinding] = false; render();
      assert.equal(listeners.size, 0, "interrupted drag releases listeners");
      up();
      assert.equal(saved().length, 1, "unfinished position is not saved");
      bindings["seety.visible"] = true; render();
      assert.deepEqual(tree.props.style, lastSaved, "saved position is restored");
    }
  } finally {
    if (previousWindow === undefined) delete global.window;
    else global.window = previousWindow;
  }
});
