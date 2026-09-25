const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { test } = require("node:test");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");

const statPath = "game-ui/game/components/toolbar/components/stat-field/stat-field.tsx";
const populationPath = "game-ui/game/components/toolbar/bottom/population-field/population-field.tsx";
const moneyPath = "game-ui/game/components/toolbar/bottom/money-field/money-field.tsx";

// Model the game's actual accessor exports: the field setters assign to constants, whereas
// StatFieldTrend is mutable. The loader calls registrars in order without catching their errors.
function registry(native) {
  const population = native;
  const money = native;
  let stat = native;
  const modules = {
    [populationPath]: { get PopulationField() { return population; }, set PopulationField(v) { population = v; } },
    [moneyPath]: { get MoneyField() { return money; }, set MoneyField(v) { money = v; } },
    [statPath]: { get StatFieldTrend() { return stat; }, set StatFieldTrend(v) { stat = v; } },
  };
  const originals = new Map();
  return {
    modules,
    appended: [],
    append(anchor, component) { this.appended.push([anchor, component]); },
    extend(modulePath, name, extend) {
      const module = modules[modulePath];
      if (!module) throw new Error("Module not found");
      if (!originals.has(modulePath)) originals.set(modulePath, [name, module[name]]);
      module[name] = extend(module[name]);
    },
    reset() {
      for (const [modulePath, [name, original]] of originals) modules[modulePath][name] = original;
      originals.clear();
      this.appended.length = 0;
    },
  };
}

test("built toolbar extension preserves native fields and subsequent mod registration", async (t) => {
  let enabled = true;
  const previousWindow = global.window;
  global.window = {
    React,
    "cs2/api": {
      bindValue: (group, name, fallback) => ({
        get value() { return group === "seety" && name === "toolbarTrends" ? enabled : fallback; },
      }),
      useValue: (binding) => binding.value,
      trigger: () => {},
    },
    "cs2/ui": { Tooltip: ({ children }) => children },
    "cs2/l10n": { useLocalization: () => ({ translate: (_key, fallback) => fallback }) },
  };
  try {
    const { default: register } = await import(pathToFileURL(path.resolve(__dirname, "../dist/Seety.mjs")));
    let original;
    const onSelect = () => {};
    const ref = React.createRef();
    const Native = (props) => (original = React.createElement("div", {
      className: "native-field", key: "native", ref, onClick: onSelect,
      "data-shortcut": "population", "data-unlimited": props.unlimited || undefined,
    }, React.createElement("span", { className: "native-value" }, "104857"),
    React.createElement("span", { className: "native-arrow" }, "arrow")));
    const r = registry(Native);

    await t.test("the regression fixture reproduces the old constant-assignment crash", () => {
      assert.throws(() => r.modules[populationPath].PopulationField = Native, TypeError);
      assert.throws(() => r.modules[moneyPath].MoneyField = Native, TypeError);
    });
    await t.test("other mods register after Seety, including after a registry reset", () => {
      for (let pass = 0; pass < 2; pass++) {
        r.reset();
        for (const registrar of [register,
          (reg) => reg.append("GameTopLeft", "another-mod"),
          (reg) => reg.append("GameBottomRight", "find-it-and-picker")]) registrar(r);
        assert.equal(r.appended[0][0], "Game");
        assert.deepEqual(r.appended.slice(1), [
          ["GameTopLeft", "another-mod"], ["GameBottomRight", "find-it-and-picker"],
        ]);
        assert.notEqual(r.modules[statPath].StatFieldTrend, Native);
      }
    });

    const Wrapped = r.modules[statPath].StatFieldTrend;
    const binding = { value: 123, subscribe() {} };
    const props = { icon: "Media/Game/Icons/Citizen.svg", trend: binding };
    const html = (overrides = {}) => renderToStaticMarkup(Wrapped({ ...props, ...overrides }));

    await t.test("delta is inside the same field with native content, handlers and ref intact", () => {
      const field = Wrapped(props);
      assert.equal(field.type, original.type);
      assert.equal(field.key, original.key);
      assert.equal(field.ref, ref);
      assert.equal(field.props.onClick, onSelect);
      assert.equal(field.props["data-shortcut"], "population");
      assert.equal(field.props.className, "native-field");
      assert.equal(field.props.children[0], original.props.children);
      assert.equal(original.props.children.length, 2, "original element was not mutated");
      assert.match(renderToStaticMarkup(field), /native-arrow.*arrow.*\+123.*<\/div>$/);
    });
    await t.test("population and money use their supplied live binding, showing positive, negative and zero", () => {
      for (const icon of ["Media/Game/Icons/Citizen.svg", "Media/Game/Icons/Money.svg"]) {
        for (const [value, expected] of [[123, "+123"], [-45, "−45"], [0, "0"]]) {
          binding.value = value;
          assert.ok(html({ icon }).includes(`>${expected}</div>`));
        }
      }
    });
    await t.test("disabled, unlimited, unrelated and missing-binding fields remain untouched", () => {
      enabled = false;
      assert.equal(Wrapped(props), original);
      enabled = true;
      for (const extra of [{ unlimited: true }, { icon: "Other.svg" }, { trend: undefined }, { trend: {} }]) {
        assert.equal(Wrapped({ ...props, ...extra }), original);
      }
    });
    await t.test("unavailable values stay absent and can recover without changing the binding", () => {
      for (const value of [NaN, Infinity, undefined, null, "123"]) {
        binding.value = value;
        assert.equal(html(), renderToStaticMarkup(Native(props)));
      }
      binding.value = 9;
      assert.match(html(), />\+9<\/div>/);
    });
    await t.test("native function hooks can render with the extension enabled or disabled", () => {
      const hooked = registry(() => {
        const [value] = React.useState("native-state");
        return React.createElement("div", null, value);
      });
      register(hooked);
      for (enabled of [false, true]) {
        const result = renderToStaticMarkup(React.createElement(hooked.modules[statPath].StatFieldTrend, props));
        assert.ok(result.includes("native-state"));
        assert.equal(result.includes("+9"), enabled);
      }
    });
    await t.test("a missing optional hook does not abort later mod registrars", () => {
      const missing = registry(Native);
      delete missing.modules[statPath];
      const warn = console.warn;
      const warnings = [];
      console.warn = (...args) => warnings.push(args);
      try {
        register(missing);
        missing.append("GameBottomRight", "another-mod");
        assert.equal(missing.appended.length, 2);
        assert.equal(warnings.length, 1);
      } finally { console.warn = warn; }
    });
  } finally {
    if (previousWindow === undefined) delete global.window;
    else global.window = previousWindow;
  }
});
