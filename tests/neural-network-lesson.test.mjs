import test from "node:test";
import assert from "node:assert/strict";
import { evaluateDenseRelu } from "../assets/neural-network-lesson.mjs";

test("matches the documented two-input Dense + ReLU fixture", () => {
  const result = evaluateDenseRelu(2, 1);
  assert.deepEqual(result.dense, [4, 3]);
  assert.deepEqual(result.output, [4, 3]);
});

test("ReLU clamps negative Dense outputs to zero", () => {
  const result = evaluateDenseRelu(-10, -10);
  assert.deepEqual(result.output, [0, 0]);
});

test("rejects non-finite inputs", () => {
  assert.throws(() => evaluateDenseRelu(Number.NaN, 1), RangeError);
  assert.throws(() => evaluateDenseRelu(1, Number.POSITIVE_INFINITY), RangeError);
});

test("rejects non-finite intermediate values", () => {
  assert.throws(() => evaluateDenseRelu(1e308, 1), RangeError);
});
