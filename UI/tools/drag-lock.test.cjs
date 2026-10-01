const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");

test("the built strip moves only when unlocked, cancels interrupted drags and splits columns", async () => {
  const slots = [];
  let cursor = 0, pending = [], dirty = false, tree;
  const listeners = new Map();
  const calls = [];
  const bindings = {
    "seety.visible": true,
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
    // This suite exercises the strip; the shared toolbar extension has its own regression test.
    bundle.default({ append: (_anchor, component) => { Strip ??= component; }, extend: () => {} });
    const render = () => {
      let passes = 0;
      do {
        assert.ok(++passes < 10, "render settles");
        cursor = 0; pending = []; dirty = false;
        tree = Strip();
        // The bar and its window are siblings; the bar is the first.
        if (tree?.type === React.Fragment) tree = tree.props.children[0];
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
    // Found by shape rather than by index: the strip's children have shifted twice now when
    // something new was added in front of them, and each time this test failed for a reason that
    // had nothing to do with dragging.
    const kids = () => tree.props.children.filter(Boolean);
    const rows = () => kids().find((child) => Array.isArray(child));
    const entry = () => rows()[0].props.children.props.children;

    render();
    const original = { ...tree.props.style };
    const lockedClass = tree.props.className;

    // Locked by default: a press never moves the bar, and a click still opens.
    down(); move(); up();
    assert.deepEqual(tree.props.style, original);
    assert.equal(listeners.size, 0);
    assert.equal(saved().length, 0);
    entry().props.onClick();
    assert.deepEqual(calls.at(-1), ["seety", "activate", "test"]);

    bindings["seety.positionLocked"] = false; render();
    const plainClass = tree.props.className;
    assert.notEqual(plainClass, lockedClass, "an unlocked bar has its own cursor");
    down(2);
    assert.equal(listeners.size, 0, "right button does not drag");
    down();
    assert.equal(tree.props.className, plainClass, "a plain press does not look like a drag");
    up(); entry().props.onClick();
    assert.deepEqual(calls.at(-1), ["seety", "activate", "test"], "a click still opens while unlocked");
    down(); move();
    assert.notDeepEqual(tree.props.style, original);
    assert.notEqual(tree.props.className, plainClass, "a real drag shows its cue");
    up();
    assert.equal(saved().length, 1);
    // 100 + 40 lands on 145: the grid starts at the first vanilla button's centre, 30, and steps
    // a quarter of their 46rem pitch, so 30 + 10 * 11.5. With nothing measured the first cell's
    // centre counts as the bar's corner.
    assert.deepEqual(saved()[0].slice(2), [145, 145], "lands on the vanilla button grid");
    const count = calls.length;
    entry().props.onClick();
    assert.equal(calls.length, count, "drag does not also activate a reading");
    down(); entry().props.onClick(); up();
    assert.deepEqual(calls.at(-1), ["seety", "activate", "test"], "click after a drag still works");

    const lastSaved = { ...tree.props.style };
    for (const [key, value] of [["seety.positionLocked", true], ["seety.visible", false]]) {
      bindings["seety.positionLocked"] = false; render();
      down(); move();
      bindings[key] = value; render();
      assert.equal(listeners.size, 0, "interrupted drag releases listeners");
      up();
      assert.equal(saved().length, 1, "unfinished position is not saved");
      bindings["seety.visible"] = true; render();
      assert.deepEqual(tree.props.style, lastSaved, "saved position is restored");
    }

    // Columns. C# sends only the readings that are switched on, and the catalogue size apart.
    bindings["seety.positionLocked"] = true;
    bindings["seety.verticalStrip"] = true; render();
    assert.notEqual(tree.props.className, lockedClass, "the column has its own class");
    const columnSizes = () => rows().map((column) => column.props.children.length);
    const one = bindings["seety.vitals"][0];
    const readings = (n) => Array.from({ length: n }, (_, i) => ({ ...one, id: "r" + i }));
    bindings["seety.catalogSize"] = 26;
    bindings["seety.vitals"] = readings(26); render();
    assert.deepEqual(columnSizes(), [13, 13], "the full catalogue is two even columns");
    bindings["seety.vitals"] = readings(18); render();
    assert.deepEqual(columnSizes(), [18], "up to 18 stays one column");
    bindings["seety.vitals"] = readings(19); render();
    assert.deepEqual(columnSizes(), [13, 6], "past 18 the first column is always 13");
    bindings["seety.vitals"] = readings(14); render();
    assert.deepEqual(columnSizes(), [14], "no first column of 13 beside a stub of one");
    bindings["seety.vitals"] = readings(26);
    bindings["seety.verticalStrip"] = false; render();
    assert.equal(tree.props.className, lockedClass);
    assert.equal(rows().length, 26, "a row is never split");
  } finally {
    if (previousWindow === undefined) delete global.window;
    else global.window = previousWindow;
  }
});
