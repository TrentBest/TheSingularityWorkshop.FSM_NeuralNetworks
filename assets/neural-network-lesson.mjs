export function evaluateDenseRelu(input1, input2) {
  if (!Number.isFinite(input1) || !Number.isFinite(input2)) {
    throw new RangeError("Inputs must be finite numbers.");
  }

  const dense = [
    (2 * input1) + input2 - 1,
    (-1 * input1) + (3 * input2) + 2
  ];

  if (!dense.every(Number.isFinite)) {
    throw new RangeError("Dense layer produced a non-finite value.");
  }

  return Object.freeze({
    input: Object.freeze([input1, input2]),
    dense: Object.freeze(dense),
    output: Object.freeze(dense.map(value => Math.max(0, value)))
  });
}
