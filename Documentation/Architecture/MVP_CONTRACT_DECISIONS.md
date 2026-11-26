# NeuralNetworks MVP Contract Decisions

**Status:** Accepted for implementation  
**Applies to:** Initial CPU inference slice and later backend conformance  
**Parent architecture:** [ARCHITECTURE.md](./ARCHITECTURE.md)  
**Branch policy:** Active work on development only; preserve master  
**Publication:** No NuGet publication is authorized.

This record responds to the seven contract gaps raised in the architecture review. The user has accepted the MVP direction: retain the bounded stateless DAG evaluator and CPU reference semantics; add a representative embodied motor-control workload as a second conformance/benchmark target; and keep recurrent/stateful execution behind a separately specified contract. Acceptance authorizes implementation of the scoped evaluator, not NuGet publication, release, deployment, or changes to `master`.

## Decision summary

| Review item | MVP disposition | Rationale |
|---|---|---|
| Model graph vs. layer stack | **Decide** | Use a validated directed acyclic computation graph; layered feed-forward networks are examples built on it. |
| Logical tensor vs. physical storage | **Decide boundary; defer backend details** | Keep logical tensor meaning independent of storage, allocation, and transfer handles. |
| Numerical semantics | **Decide CPU reference; defer accelerator tolerance** | Make the CPU fixture hand-checkable and operation order explicit; set cross-backend tolerances only with a selected backend and measured behavior. |
| Model/parameter consistency | **Decide** | Each inference observes one immutable model/parameter version. |
| Bounded execution | **Decide required budget dimensions; defer numeric ceilings** | The API must carry enforceable limits; concrete ceilings require consumer and hardware evidence. |
| Trace contract | **Decide** | Stable node/port identities; optional bounded trace from the actual execution. |
| MVP acceptance fixture | **Decide** | Adopt the exact two-input/two-output ReLU fixture below as a shared conformance example. |

## 1. Model graph versus layer stack

**Disposition: Decided for the MVP.**

- The execution model is a directed acyclic computation graph (DAG).
- Every operation node has a stable identity within a model definition and explicitly declared input and output ports.
- Edges connect a source node/port to a destination node/port. Port shape and element-type compatibility are validated before execution.
- A feed-forward layered network is the first educational model expressed using this graph. A layer is a useful model-level grouping, not a requirement that all future models be represented as a simple layer list.
- The MVP evaluator rejects cycles. Recurrent/stateful execution requires a separately specified execution and state contract; it must not be approximated by allowing cycles into a DAG evaluator.
- Stable identities are model-definition identities, not runtime memory addresses or backend handles.

## 2. Logical tensor versus physical storage

**Disposition: Logical/physical boundary decided; backend-specific allocation deferred.**

A logical tensor describes:
- Shape and dimension ordering.
- Element type.
- Logical indexing and the meaning of each value.

Physical storage describes:
- Concrete allocation and memory location.
- Layout, strides, alignment, and padding.
- Host/device transfer resources and backend-specific handles.
- Temporary workspaces and synchronization primitives.

The logical tensor and model contracts must not expose vendor buffer types. A backend adapter may use any internal representation that preserves the declared logical semantics.

For the first CPU reference implementation:
- Inputs are caller-provided values validated and read during the request.
- A model version owns its parameter values; inference treats that version as read-only.
- The execution owns intermediate values and the returned result values.
- The result remains valid independently of temporary evaluator storage.
- No asynchronous execution or device-memory lifetime is implied by the CPU MVP.

For a future asynchronous backend, the backend execution owns its temporary/device allocations and exposes an explicit completion object or equivalent contract. Those allocations cannot be reused or released until completion; the exact API and disposal pattern remain deferred until a backend is selected.

## 3. Numerical semantics

**Disposition: CPU reference semantics decided; cross-backend tolerance deferred.**

For the first CPU fixture and reference path:
- Use IEEE 754 binary64 (double) values.
- Evaluate each dense output in a fixed, documented input-index order.
- Compute each affine output as the ordered sum of input-value times weight terms, followed by the bias.
- Define ReLU as max(0, x) for finite x.
- Reject NaN and positive/negative infinity in inputs and parameters during request/model validation.
- Detect and report non-finite intermediate or output values as numerical execution failures rather than returning them as ordinary successful inference results.
- Do not claim bitwise equality across future GPU backends. Their conformance policy must specify supported precision, accumulation behavior, operation ordering where controllable, and measured absolute/relative tolerances.

The binary64 choice is for a transparent correctness baseline and hand-checkable teaching example; it does not prescribe the precision or layout of a future accelerator implementation.

## 4. Model and parameter consistency

**Disposition: Decided.**

An inference request observes exactly one immutable model version:
- A model version contains a validated graph and a consistent parameter set.
- Parameter updates produce a new version; they do not mutate parameter storage visible to an in-flight inference.
- An inference captures its model version before execution and uses that version for every operation.
- Results and traces identify the model version used.
- Implementations may use immutable value objects, copy-on-write storage, or another mechanism, provided the observable snapshot guarantee holds.

This avoids an inference observing a mixture of old and new weights or biases. The internal versioning mechanism is an implementation detail.

## 5. Bounded execution contract

**Disposition: Required contract dimensions decided; concrete numeric ceilings deferred.**

Every consumer-facing inference or experiment request must be subject to explicit limits. The contract must be able to express:
- Maximum input elements and accepted tensor shapes.
- Maximum graph nodes/operations and estimated or actual work.
- Maximum temporary and result memory.
- A deadline or execution-time budget.
- Cancellation behavior and the point at which cancellation can be observed.
- Maximum trace bytes, values, or records.
- Whether batching is allowed and its maximum size, when batching is introduced.

The MVP must reject requests that exceed configured limits rather than silently dropping limits. Initial numeric defaults should be chosen from actual first-consumer needs and measured resource behavior; this record does not invent universal ceilings. Cancellation cannot promise instantaneous interruption inside an indivisible backend operation; the backend contract must state its cancellation granularity.

NeuralNetworks supplies bounded computation and evidence. Hermit continues to own goals, permissions, planning, and action authority. Gestures and Physics remain external experiment interfaces, not dependencies of the neural core.

## 6. Trace contract

**Disposition: Decided.**

- Each trace record identifies the model version, stable node identity, output-port identity, element type, and logical shape.
- A trace is captured from the same execution that produced the inference result; it is not recomputed by a separate visualization path.
- Trace capture is opt-in.
- Trace modes may include full values, summaries, or sampled values, but the selected mode is explicit in the request/result.
- Trace collection is bounded by the request's trace budget. Exceeding the budget must produce an explicit truncation/status indication, never unbounded allocation.
- The MVP educational fixture uses full values because its tensors are deliberately tiny.
- Trace data is evidence about computation, not a second source of model semantics.

## 7. MVP acceptance fixture

**Disposition: Decided.**

Use one dense 2-input / 2-output operation followed by ReLU.

Inputs: x = [2, 1]

Weights (rows are output units; columns are input features): W = [[2, 1], [-1, 3]]

Bias: b = [-1, 2]

Affine calculation:
- Output 0: (2 × 2) + (1 × 1) + (-1) = 4
- Output 1: (2 × -1) + (1 × 3) + 2 = 3

Activation:
- ReLU(4) = 4
- ReLU(3) = 3

Expected output: y = [4, 3]

The fixture has the following logical dataflow:
1. Input node input-0, output port values, shape [2].
2. Dense node dense-0, input port values, output port pre-activation, shape [2].
3. ReLU node relu-0, input port values, output port values, shape [2].

The graph edges connect input-0.values to dense-0.values, then dense-0.pre-activation to relu-0.values. The final result is relu-0.values = [4, 3].

This fixture must be reused for:
- CPU inference expected-value tests.
- Graph and shape validation tests.
- Trace/result consistency tests.
- Future backend semantic conformance tests, with the documented backend tolerance policy.
- Educational documentation showing inputs, weights, biases, pre-activation values, activation, and outputs.

## 8. Final MVP precision decisions

**Disposition: Proposed clarifications responding to the follow-up review.**

### Element type

The MVP evaluator accepts only IEEE 754 binary64 (double) tensors and parameters. Any other element type is rejected during model/request validation. Future backends may advertise additional types only through separately named conformance profiles; they do not silently widen the MVP contract.

### Dense operation

For an input vector of length n, output width m, weights shaped [m,n], and bias shaped [m], output unit j is:

    z_j = b_j + sum(i = 0..n-1, x_i * W[j,i])

The evaluator accumulates input features in ascending index order, then adds the bias once. Weight row j belongs to output unit j; weight column i belongs to input feature i. The input shape is [n], weight shape is [m,n], bias shape is [m], and output shape is [m]. The acceptance fixture therefore has input [2], weights [2,2], bias [2], and output [2].

### MVP operation set and graph validation

The first evaluator supports exactly these operation kinds:

- Input: declares a named input port and its fixed logical shape/type.
- Dense: consumes one input tensor plus immutable weights and bias, and produces one affine output.
- ReLU: consumes one tensor and produces one tensor of the same shape/type.

Before execution, validation must establish unique stable node identities; known operation kinds; required ports; valid edge endpoints; one producer per non-input port; no missing required inputs; no duplicate/ambiguous edges; compatible shapes and element types; and a valid topological order. Cycles, unknown operations, unsupported element types, malformed ports, and shape mismatches fail validation. The evaluator must not skip or reinterpret unknown nodes.

### Enforceable request budget

Every evaluation requires an enforceable finite budget. It may be supplied by the request or inherited from an explicitly configured evaluator policy. If neither supplies a complete enforceable budget, the request is rejected before execution. The policy must bound at least input elements, graph nodes/operations, intermediate/result memory, trace records or bytes, and execution time/cancellation observation. The selected values are configuration, not universal library constants; the evaluator must enforce them and return a budget-exceeded outcome when a limit is crossed. An unbounded/default-unlimited mode is not part of the MVP.

A Foundry host may derive and pass an inference budget from its own run policy, but Foundry's workflow context JSON size limits are not neural-compute budgets and must not be reused as if they were. Foundry remains responsible for run lifecycle and scheduling; NeuralNetworks enforces the computation-specific limits within its evaluator/provider boundary.

### Numerical failure

Any non-finite value detected during multiplication/accumulation, bias addition, or activation is a numerical execution failure (category: NumericalFailure). The evaluator must not return a successful partial result. It may return bounded diagnostic/trace evidence that identifies the failing node/port and model version, subject to the trace budget. Invalid non-finite inputs/parameters remain validation failures; non-finite values generated during evaluation are execution failures.


## 9. Embodied motor-control conformance workload

**Disposition: Accepted as a second workload target; implementation follows the core evaluator.**

The motor-control workload exercises the same stateless DAG evaluator. It does not change the MVP graph semantics or make the neural core depend on Hermit, Gestures, Physics, Unity, or Renderer.

The workload should model one bounded control observation and produce a bounded vector of actuator proposals:

- Inputs are a fixed-shape numeric observation assembled by the consuming experiment from stable joint/actuator identifiers, joint state, local supervisor mode, target/achieved movement error, and other explicitly selected sensor values.
- The graph is an ordinary acyclic feed-forward model using only operations supported by the MVP profile.
- Outputs are finite numeric proposals interpreted by the consumer as bounded activation/effort/trajectory requests. They are not direct physical state mutations, permissions, or authoritative joint commands.
- The consumer applies its own safety bounds and local FSM/supervisor rules; Physics or the relevant physical simulator remains authoritative for outcomes.
- A deterministic, hand-checkable small fixture is required first. A representative wider/batched workload may then be added for performance measurement.
- The workload must report graph size, input/output dimensions, batch size, evaluation count, trace mode, and budget settings so benchmark results are reproducible.

The motor-control target is a second conformance and benchmark workload, not a second evaluator implementation. The original dense/ReLU acceptance fixture remains the small numerical correctness oracle. Benchmark arithmetic separately from validation, allocations, tracing, orchestration, observation adaptation, and any device transfer.

### Recurrent and stateful execution boundary

Recurrent networks, feedback edges, hidden state, temporal windows, and state carry-over are **not** part of this accepted stateless DAG implementation. They require a separate contract covering at least state ownership and lifetime, sequence/batch semantics, reset behavior, deterministic replay, concurrency/isolation, bounded memory/work, cancellation, and trace representation. Do not smuggle recurrence into the DAG evaluator or treat an ordinary input/output loop in a consumer as an internal recurrent graph.

## Deferred evidence items

These are intentionally not selected by this record:
- GPU API, hardware targets, driver/runtime versions, and deployment constraints.
- Accelerator precision and cross-backend numerical tolerances.
- Device allocation, transfer, queue, fence, and asynchronous disposal APIs.
- Numeric default budgets for input size, work, memory, duration, cancellation, and trace volume.
- Whether future model formats require graph features beyond the initial DAG.
- Any recurrent/stateful execution semantics.

Each deferred item should be resolved with alternatives, evidence, trade-offs, and a recorded decision before it becomes an implementation assumption.

## Gate to the first implementation

**Gate satisfied:** the user accepted the MVP direction. Implementation may begin on `development`. The first CPU inference slice should implement only the DAG subset, logical tensor boundary, immutable model-version behavior, finite-value validation, bounded optional trace, and the acceptance fixture. The embodied motor-control workload follows as a second conformance/benchmark target on the same evaluator. It should not introduce a GPU dependency, training framework, broad model zoo, or consumer-specific authority.

No NuGet publication, master change, or additional branch is authorized by this record.
