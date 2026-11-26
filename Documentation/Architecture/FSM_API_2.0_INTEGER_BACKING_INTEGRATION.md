# FSM_API 2.0 Integer-Backed Neural Execution: Integration and Measurement Plan

**Status:** Owner-directed implementation plan (2026-11-26)
**Active branch:** `development`
**Publication:** No NuGet publication is authorized.
**Scope:** Establish a measurable neural-network inference foundation using FSM_API 2.0 where stateful orchestration is useful, without confusing integer-backed FSM identity with neural numerical data.

## 1. The important distinction

FSM_API 2.0's integer-backed runtime is an execution/state-management representation. It is not a replacement for neural tensor values.

The first neural evaluator therefore keeps these representations separate:

| Concern | Representation | Owner |
|---|---|---|
| Model/node/port semantic identity | Stable domain identity, resolved to local integer indexes for execution | NeuralNetworks |
| Neural inputs, weights, biases, activations, gradients | Explicit tensor element type; MVP uses IEEE 754 binary64 | NeuralNetworks |
| Evaluator lifecycle and bounded experiment phases | FSM_API 2.0 integer-backed state machine where its measured behavior helps | NeuralNetworks using FSM_API |
| Bulk tensor and parameter storage | DataWarehouse-owned storage/region contracts | DataWarehouse |
| GPU device resources and dispatch | Backend adapter; selected only after hardware reconnaissance | NeuralNetworks/backend |
| Goals, permissions, and action authority | Consumer/host policy | Hermit / host |

Do not quantize weights or activations to integers merely because FSM_API supports integer-backed state identity. Do not encode every neural graph operation as an FSM state. The DAG is the mathematical model; FSM_API is an optional lifecycle/orchestration substrate around bounded work.

## 2. First proof: a real, inspectable inference engine

Implement the already specified MVP graph and evaluator:

1. `Input` — declared shape and element type.
2. `Dense` — ordered affine transform using immutable weights and bias.
3. `ReLU` — elementwise `max(0, x)`.
4. A validated acyclic graph with stable node/port identities and deterministic topological execution.
5. One immutable model-version snapshot per inference.
6. Finite-value validation, enforceable finite budgets, cancellation observation, and optional bounded trace.
7. The shared fixture: input `[2, 1]`, weights `[[2, 1], [-1, 3]]`, bias `[-1, 2]`, expected output `[4, 3]`.

The first CPU implementation is a correctness reference, not a claim that CPU is the desired high-throughput destination. It provides an oracle against which later accelerator implementations can be compared.

## 3. Where FSM_API 2.0 belongs

Use the integer-backed FSM runtime only for a real stateful lifecycle that benefits from explicit transitions, such as:

```text
Created -> Validating -> Ready -> Executing -> Completed
                    \-> Rejected
                               \-> Cancelled
                               \-> Failed
```

The exact state IDs are local implementation indexes. Keep their semantic names/mapping in NeuralNetworks and do not persist raw indexes as durable identities without a versioned mapping.

The evaluator must remain callable without forcing every consumer to construct an FSM. The FSM-backed coordinator is an adapter/implementation detail, not a mandatory neural model representation. If the FSM overhead exceeds a simpler coordinator for tiny inference, measure and retain the simpler path where appropriate.

Before coding against the package, inspect the actual FSM_API 2.0 development public contract and pin the compatible project/package reference. Do not infer an API from legacy 1.x examples or invent integer overloads. Keep string-backed and integer-backed behavioral parity as a separate conformance question.

## 4. Performance questions we will answer

Measure, do not assume:

- Integer-backed FSM lifecycle/update overhead versus string-backed equivalent.
- FSM-coordinated inference overhead versus direct evaluator invocation.
- Dense kernel throughput and scaling across input/output dimensions and batch sizes.
- Allocations and retained memory per inference; warm versus cold execution.
- Trace-disabled versus trace-enabled overhead.
- Validation/build cost versus steady-state inference cost.
- DataWarehouse allocation/access costs separately from numerical compute.
- Later, host/device transfer, dispatch, synchronization, and kernel execution separately on named GPU hardware.

Every result must record exact commit, runtime, machine/CPU/GPU, workload dimensions, warmup/repetition policy, allocations, and statistical spread. Do not compare unrelated machines or claim acceleration from theoretical operation counts.

## 5. Work sequence

### Phase A — Contract and project audit
- Confirm the active project/solution layout and target frameworks.
- Confirm the actual FSM_API 2.0 integer-backed creation, state registration, instance creation, update, and lifecycle APIs from development source.
- Confirm the DataWarehouse APIs available today; do not assume device allocations or GPU fences exist.
- Resolve any contract ambiguity before public API design.

### Phase B — CPU reference inference
- Implement immutable model definitions, tensor shape/type validation, DAG validation/topological order, Dense, and ReLU.
- Implement the exact acceptance fixture and malformed-graph/numerical-failure tests.
- Add finite enforceable request budgets and bounded execution traces.
- Preserve one model-version snapshot throughout inference.

### Phase C — Integer-backed orchestration experiment
- Add a small opt-in evaluator lifecycle coordinator using FSM_API 2.0 integer backing.
- Compare it against direct invocation and a semantically equivalent string-backed FSM.
- Keep the coordinator only where its state observability/composition value justifies measured cost.

### Phase D — Benchmarks and learning
- Establish repeatable CPU baselines before selecting GPU technology.
- Add interactive educational experiments that expose actual model weights, pre-activations, activations, outputs, and trace evidence.
- Add training only after inference is correct and measured: loss, gradient computation, optimizer, dataset contract, deterministic seed/reproducibility, and bounded training budgets each need explicit tests.

### Phase E — Accelerator
- Reconnoiter actual target hardware and deployment constraints.
- Select a backend only with evidence; expose backend/device selection and explicit unsupported/fallback outcomes.
- Compare accelerator outputs against the CPU reference using a documented precision/tolerance profile.
- Keep GPU resource lifetime and synchronization with the appropriate storage/backend owner.

## 6. Acceptance gates

- The [MVP contract](MVP_CONTRACT_DECISIONS.md) remains the numerical and graph acceptance source.
- CPU fixture produces exactly the documented expected result under the specified ordered binary64 arithmetic.
- Invalid shapes, cycles, missing ports, unknown operations, NaN/infinity, budget violations, and numerical overflow fail explicitly.
- Trace values come from the execution that produced the result.
- Integer-backed orchestration has tests and benchmarks proving the actual FSM_API 2.0 path used.
- No GPU acceleration, training, DataWarehouse GPU allocation, or Hermit integration is claimed before implemented and verified.
- No master writes, branch deletion, merge, release, or NuGet publication as part of this work.

## 7. What success looks like

We will be able to show a network, edit its weights and inputs, inspect every intermediate value, watch a deterministic inference execute, and measure the costs of the neural math and FSM orchestration independently. Then we can add learning and GPU execution without losing a trustworthy reference implementation.
