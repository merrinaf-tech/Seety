const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const ts = require("typescript");

const source = fs.readFileSync(path.join(__dirname, "../src/mods/position.ts"), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS },
}).outputText;
const context = { exports: {} };
vm.runInNewContext(compiled, context);
const { clampPosition, clampSize } = context.exports;

test("a dragged window size stays between its minimum and the room on screen", () => {
  // Copied out of the vm context: its objects have another Object prototype, which deepStrictEqual
  // treats as a difference.
  const fit = (...args) => ({ ...clampSize(...args) });
  const minimum = { width: 380, height: 80 };
  const room = { width: 1600, height: 900 };
  assert.deepEqual(fit({ width: 700, height: 600 }, minimum, room), { width: 700, height: 600 });
  assert.deepEqual(fit({ width: 100, height: 10 }, minimum, room), minimum, "never below the minimum");
  assert.deepEqual(fit({ width: 5000, height: 5000 }, minimum, room), room, "never past the screen");
  assert.deepEqual(fit({ width: 500, height: 500 }, minimum, { width: 200, height: 50 }), minimum,
    "near the edge the minimum still wins");
});

test("panels remain within the viewport, including scaled and oversized layouts", () => {
  for (const scale of [0.5, 1, 2, 4]) {
    const viewport = { width: 1920 / scale, height: 1080 / scale };
    for (const size of [{ width: 380, height: 400 }, { width: 860, height: 560 }]) {
      for (const position of [{ x: -200, y: -100 }, { x: 320, y: 160 }, { x: 9999, y: 9999 }]) {
        const result = clampPosition(position, viewport, size);
        assert.ok(result.x >= 0 && result.y >= 0);
        if (size.width <= viewport.width) assert.ok(result.x + size.width <= viewport.width);
        else assert.equal(result.x, 0);
        if (size.height <= viewport.height) assert.ok(result.y + size.height <= viewport.height);
        else assert.equal(result.y, 0);
      }
    }
  }
  const result = clampPosition({ x: 120, y: 80 },
    { width: 1920, height: 1080 }, { width: 380, height: 400 });
  assert.equal(result.x, 120);
  assert.equal(result.y, 80);
});
