# NeuralNetworks MVP Contract Decisions

**Status:** Proposed decisions for review  
**Applies to:** Initial CPU inference slice and later backend conformance  
**Parent architecture:** [ARCHITECTURE.md](./ARCHITECTURE.md)  
**Branch policy:** Active work on development only; preserve master  
**Publication:** No NuGet publication is authorized.

This record responds to the seven contract gaps raised in the architecture review. It distinguishes decisions appropriate for the first educational inference slice from choices that require target-hardware or workload evidence. These are proposed contracts, not claims of implemented behavior.

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

The first CPU inference slice may begin after review accepts or amends these proposed contracts. It should implement only the DAG subset, logical tensor boundary, immutable model-version behavior, finite-value validation, bounded optional trace, and the acceptance fixture. It should not introduce a GPU dependency, training framework, broad model zoo, or consumer-specific authority.

No NuGet publication, master change, or additional branch is authorized by this record.
