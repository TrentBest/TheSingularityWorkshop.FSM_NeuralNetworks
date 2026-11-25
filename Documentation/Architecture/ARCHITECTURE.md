# NeuralNetworks Architecture

**Status:** Proposed  
**Scope:** Initial domain architecture and implementation boundaries  
**Branch policy:** Active work on `development`; preserve `master`  
**Publication:** No NuGet publication is authorized by this document.

## 1. Purpose

FSM_NeuralNetworks is the Workshop domain for representing neural models and executing their computational behavior. It should support understandable, inspectable experiments first, while establishing contracts that can scale to GPU-backed workloads.

The central design separation is:

- **Model semantics** describe what a network is and how its computation is defined.
- **Execution** evaluates that model against input data.
- **Backends** determine where and how operations execute.
- **Consumers** decide why a model is evaluated and what to do with its outputs.

The architecture must not confuse neural computation with autonomous agency, physical simulation, or application policy.

## 2. Domain boundaries

### NeuralNetworks owns

- Model topology and layer/connection descriptions.
- Trainable and fixed parameters: weights, biases, and related model state.
- Tensor and numerical operation contracts required by neural computation.
- Activation and loss function definitions.
- Inference and, in a later milestone, training algorithms.
- Execution requests, results, diagnostics, and inspectable traces.
- Backend-neutral capability contracts and backend selection policy.

### NeuralNetworks does not own

- Goals, permissions, planning, or action authority.
- Environment state or physical outcome simulation.
- Embodiment, skeletal structure, inverse kinematics, or movement execution.
- Application UI, visualization policy, or user interaction.
- Data-set governance, experiment objectives, or the decision that a model should learn.

### Initial ecosystem relationships

- **Hermit** is the first intended consumer. It owns objectives, observations, policy, permissions, and decisions. NeuralNetworks provides bounded computation and inspectable results.
- **Gestures** may later provide embodiment and movement-control interfaces for experiments.
- **Physics** may later provide measurable environment outcomes for controlled experiments.
- These integrations are consumers of explicit contracts, not reasons to make NeuralNetworks depend directly on those domains.

## 3. Architectural principles

1. **GPU-first, not GPU-only.** Design workloads and dataflow to use accelerators where they are practical. Retain a CPU reference implementation for correctness, portability, and small workloads.
2. **CPU orchestrates; compute backends execute.** Lifecycle, validation, scheduling, dispatch, coordination, and I/O belong in orchestration. Large parallel numerical workloads belong in an appropriate compute backend.
3. **No invisible fallback.** Backend capability, selection, and fallback must be observable. A caller must be able to require a capability or backend and receive an explicit unsupported result rather than silently receiving a different execution path.
4. **Semantics are backend-neutral.** Model definitions and mathematical meaning must not depend on CUDA, DirectML, Vulkan, or another vendor API.
5. **Correctness precedes optimization.** Establish a tested reference path before introducing accelerated execution.
6. **Inspection is a first-class capability.** The system should explain the inputs, parameters, intermediate values, outputs, and execution path that produced a result.
7. **Inference and learning are distinct.** Fixed-parameter evaluation must remain useful without training facilities. Training adds state, algorithms, data requirements, and reproducibility concerns that deserve their own contracts.
8. **Educational examples use production contracts.** Demonstrations should exercise the same model and execution abstractions as larger workloads.

## 4. Conceptual model

The initial model vocabulary should distinguish these concepts without committing prematurely to concrete public types:

- **Tensor:** a shaped collection of numerical values with a defined element type and layout.
- **Operation:** a mathematical transformation over one or more tensors.
- **Layer:** a model component that applies a defined transformation.
- **Connection:** the dataflow relationship between model components.
- **Parameter:** model-owned numerical state such as weights or biases.
- **Model:** a validated topology plus its parameter state and declared semantics.
- **Inference request:** input values and execution requirements for evaluating a model.
- **Inference result:** output values, execution metadata, and optional diagnostic trace.
- **Backend:** an implementation capable of executing a declared set of operations and data types.
- **Training session:** a later workflow that evaluates a model against data, computes an objective and gradients, and updates parameters under an explicit optimization policy.

This vocabulary is conceptual. The first implementation should validate the smallest useful subset rather than implement every concept at once.

## 5. Tensor and numerical contracts

Before selecting a GPU API or tensor library, define and test the observable tensor contract:

- Shape and dimension ordering.
- Element type and supported numerical precision.
- Storage ownership and lifetime.
- Layout/stride semantics, including whether values are contiguous.
- Validation rules for incompatible shapes and operations.
- Numerical behavior for exceptional values and invalid inputs.
- Copy and transfer semantics between host and backend memory.
- Whether an operation is synchronous or may complete asynchronously.

Do not assume a single tensor layout or precision is optimal for every backend. The contract must make layout and precision explicit enough that backend adapters can translate or reject unsupported combinations.

The initial CPU reference path should use the simplest representation that makes correctness and inspection clear. More advanced layouts, mixed precision, batching, and zero-copy paths should be introduced only when measured workloads justify them.

## 6. Model and parameter contracts

A model must be inspectable and valid independently of any execution backend.

At minimum, validation should establish:

- Every referenced layer or operation exists.
- Connections are structurally valid.
- Input and output shapes are compatible.
- Required parameters exist and have compatible shapes.
- Activation and operation choices are supported by the model definition.
- Cycles or recurrent structures are either explicitly supported or rejected; they must not be accidentally accepted by an acyclic evaluator.
- Parameter mutation has defined ownership and synchronization behavior.

The first educational model can be a small feed-forward network. Recurrent networks, dynamic graphs, convolutional models, attention, and model import formats remain future scope until a concrete experiment requires them.

Fixed and trainable parameters should be distinguishable in the model semantics even if the first inference implementation treats all parameters as read-only.

## 7. Execution architecture

An inference execution should follow a visible sequence:

1. Validate the model and request.
2. Resolve the requested execution requirements.
3. Select a backend that declares the required capabilities.
4. Prepare or transfer inputs and parameters as needed.
5. Dispatch the model operations.
6. Synchronize only where required by the result contract.
7. Return outputs and execution metadata.
8. Provide an optional trace suitable for inspection and education.

The execution API should make backend selection and fallback policy explicit. A caller should be able to express requirements such as preferred backend, required capability, precision constraints, and whether fallback is permitted. Exact option names and public API shapes are implementation decisions for the contract review.

The result should identify the backend actually used and relevant execution characteristics. Errors should distinguish invalid models or requests from unsupported backend capabilities and execution failures.

## 8. Backend boundary and memory lifecycle

A backend adapter is responsible for translating backend-neutral operations into its execution environment. It should declare capabilities rather than imply universal support.

Capability discovery should eventually cover at least:

- Supported operation families.
- Supported element types and precision modes.
- Supported tensor layouts or constraints.
- Device availability and relevant limits.
- Asynchronous execution and synchronization behavior.
- Memory allocation, transfer, and interoperability capabilities.

Memory ownership must be explicit. The architecture must answer who owns host buffers, device buffers, parameter storage, temporary workspaces, and completion/fence objects, and when each may be released or reused.

Avoid designing a universal abstraction around one vendor's memory model. The core contract should express required semantics; adapters may implement them differently.

## 9. CPU reference and accelerated implementations

The CPU implementation is the initial correctness oracle and portability path. It is not evidence that GPU-first goals have been met, nor should it become an invisible substitute when an accelerator is requested.

A future GPU backend must:

- Pass the same semantic conformance tests as the reference implementation, allowing documented floating-point tolerances.
- Report the actual backend and device used.
- Make transfers, dispatch, synchronization, and fallback observable.
- Demonstrate measured benefit on named hardware and workloads before performance claims are made.

Do not select CUDA, DirectML, Vulkan, or another backend until repository/runtime reconnaissance establishes target hardware, supported platforms, deployment constraints, and maintenance costs.

## 10. Inspection and educational dataflow

The first demonstration is a fixed-weight, inference-only network with two inputs and two outputs.

The example should expose:

- Input values.
- Layer and connection topology.
- Each relevant weight and bias.
- Weighted sums and activation outputs.
- Final output values.
- The effect of changing a parameter or input.

An optional trace should be generated from actual execution values, not reconstructed by a separate visualization-only implementation. Trace collection should be controllable so that educational detail does not impose mandatory overhead on production workloads.

Hermit Pages can use this trace to teach the real dataflow. The visualization is a consumer of execution evidence; it must not become a second source of mathematical truth.

## 11. Learning and training boundary

Training is a later milestone, separate from fixed-weight inference.

A training workflow will need explicit contracts for:

- Data samples, batches, and their ownership.
- Objective/loss evaluation.
- Gradient calculation and propagation.
- Optimizer state and parameter updates.
- Training/evaluation modes.
- Randomness and seed management.
- Checkpointing and experiment reproducibility.
- Numerical stability, convergence, and overfitting diagnostics.

Do not add a training framework, model zoo, or broad dataset pipeline before the inference contracts and first educational experiment are stable.

## 12. Consumer integration

The first Hermit integration should be narrow and bounded. Candidate capabilities are:

- Inspect a model and its declared capabilities.
- Evaluate a model with validated input.
- Retrieve an execution result and optional trace.
- Run a bounded experiment whose limits and resource use are explicit.

Hermit retains authority over goals, permissions, planning, and actions. NeuralNetworks must not independently grant itself access to tools, environments, or external actions.

Gestures and Physics integrations should follow only after the core inference and experiment contracts are proven. A future motor-control experiment should compare models under the same bounded observations and environment conditions, with Physics supplying outcomes and Gestures supplying movement capabilities.



[Workshop runtime integration](WORKSHOP_RUNTIME_INTEGRATION.md) defines the concrete ownership and handoff boundaries described below.

### Workshop execution and composition boundary

NeuralNetworks must use the Workshop runtime rather than introduce a competing executor.

- **MicroBundleDomain defines the capability contract.** A neural capability may be authored as a MicroBundle when it needs to participate in composition. NeuralNetworks owns neural semantics; it must not depend on FSM_COS or FSM_Execution merely to define a model or operation.
- **FSM_COS composes.** It resolves, validates, loads, and arbitrates requested MicroBundles, then returns a RuntimeAssembly. It does not schedule application work or execute a neural model.
- **FSM_Execution executes.** It owns authoritative execution lifecycle, scheduling orchestration, eligibility, dispatch, cancellation, cleanup, observable state, and outcomes. Concrete execution providers perform work on supported resources.
- **FSM_API supplies FSM mechanics** where the chosen FSM_Execution integration actually uses them. Neural graph nodes are not automatically FSM states, and FSM_API process-group ticks are not a substitute for a neural graph evaluator.
- **Hosts adapt and authorize.** AnyApp, WebApp, ServerApp, MyVR, headless/CI hosts, and The can provide host capabilities and present results. GUI is presentation, not execution authority.

The intended path is:

~~~text
Experience manifest + configuration
                |
                v
       FSM_COS composition
                |
                v
          RuntimeAssembly
                |
                v
        FSM_Execution execution
          /          \
  execution provider  lifecycle / scheduling
          |
          v
 NeuralNetworks evaluation
          |
          v
 result + optional execution trace
          |
          v
 host / Hermit / Visualize presentation
~~~

This is an architectural target, not a claim that the end-to-end adapter already exists. FSM_Execution issue #23 tracks the typed RuntimeAssembly handoff; FSM_Execution issue #2 tracks scheduling composed MicroBundle work from declared intent. The current FSM_Execution workflow runner is a sequential named-handler workflow executor; it is not yet the RuntimeAssembly executor. Do not pretend that a neural graph, a FSM_Execution workflow, an FSM_API process group, and a RuntimeAssembly are interchangeable.

For the first neural slice, keep the neural computation graph internal to NeuralNetworks. FSM_Execution should schedule a bounded neural evaluation capability through its supported execution/provider seam. The neural evaluator validates and executes its own DAG in deterministic topological order. Only expose graph operations as separately schedulable work when the upstream scheduling contract, dependency semantics, and measured benefit justify that granularity. Producer/consumer dependencies must never be reordered by priority; independent operations may be parallelized only under an explicit, tested synchronization and resource contract.

The CPU reference evaluator is a correctness implementation, not a new lifecycle manager or hidden fallback for a requested accelerator. A future GPU implementation belongs behind a NeuralNetworks-owned backend-neutral semantic contract and a concrete execution-provider adapter. Backend selection, capability reporting, transfers, completion, and fallback must be visible to FSM_Execution/host callers. Do not add a direct GPU SDK dependency until hardware/runtime reconnaissance supports it.

### Reuse policy

Reuse existing Workshop contracts where their real interfaces fit. Use FSM_COS for composition, not as a neural scheduler. Use FSM_Execution for execution lifecycle and scheduling, not as a replacement for neural graph semantics. Use MicroBundleDomain for capability identity, dependencies, providers, and lifecycle participation; do not make the neural core depend on the composition host. Use FSM_API only for actual state/context mechanics; do not encode every neural operation as an FSM state.

Keep FSM_Serialization, HeadlessAi, ProtocolAi, GrammarAi, Hermit, Gestures, Physics, and Visualize optional integrations at their respective boundaries. None is a mandatory dependency of the neural mathematical core. Hermit remains the owner of goals, permissions, planning, and action authority. Gestures and Physics are experiment interfaces; Visualize consumes execution evidence and must not recompute neural values.


## 13. Testing and benchmarks

Testing should proceed in layers:

1. Tensor shape, indexing, and numerical operation tests.
2. Model validation and topology tests.
3. Deterministic CPU inference tests with hand-checkable expected values.
4. Backend conformance tests against the reference semantics.
5. Explicit tests for unsupported capabilities and fallback policy.
6. Trace consistency tests proving inspection values match actual computation.
7. Integration tests for bounded consumer requests.

Benchmarks must separate:

- Model preparation and compilation, where applicable.
- Host-to-device transfer.
- Dispatch and scheduling overhead.
- Kernel or operation execution time.
- Device-to-host transfer.
- Synchronization cost.
- Peak and steady-state memory use.
- End-to-end latency and throughput.

Record workload shape, batch size, precision, backend, device, software/runtime version, warm-up policy, and measurement method. Compare against the CPU reference using equivalent workloads. Do not claim acceleration from kernel time alone when end-to-end transfer and dispatch dominate.

## 14. Proposed implementation milestones

| Milestone | Deliverable | Exit condition |
|---|---|---|
| A. Contract review | Approved tensor, model, execution, and backend boundaries | Open design decisions recorded; no vendor lock-in |
| B. CPU correctness | Minimal tensor operations and fixed feed-forward inference | Deterministic tests pass; 2-input / 2-output example is inspectable |
| C. Execution diagnostics | Backend identity, capability reporting, optional trace | Trace reflects actual execution; unsupported paths are explicit |
| D. GPU reconnaissance | Hardware/runtime assessment and backend proposal | Chosen target justified by supported environments and measured constraints |
| E. First accelerator | One backend implementation | Conformance tests pass; transfers and dispatch are measured |
| F. Hermit experiment | Bounded model inspection and evaluation capability | Hermit can invoke and inspect inference without gaining action authority |
| G. Learning foundation | Loss, gradients, optimizer contracts and a small example | Training is separately tested and documented |
| H. Embodied experiment | Controlled Gestures/Physics integration | Repeatable experiment with measurable outcomes |

Milestones are proposed sequencing, not claims that implementation exists.

## 15. Open decisions requiring evidence

The following should remain open until a focused investigation resolves them:

- Target operating systems, hardware, and minimum GPU capability.
- Backend candidates and their deployment/licensing implications.
- Tensor element types and precision policy for the first implementation.
- Tensor layout and ownership model.
- Whether graph construction is static, dynamic, or initially restricted to a simple feed-forward topology.
- Required determinism guarantees and acceptable cross-device tolerances.
- Whether backend compilation/caching is needed for the first accelerator.
- The minimum Hermit capability contract and resource limits.

Each decision should record alternatives, evidence, trade-offs, and the reason for selection.

## 16. Non-goals for the initial implementation

- Publishing a NuGet package.
- Committing to a vendor-specific GPU API before reconnaissance.
- Building a general-purpose deep-learning framework.
- Implementing a large model zoo or importing arbitrary model formats.
- Giving neural models autonomous authority over tools or environments.
- Claiming GPU performance without reproducible measurements.
- Building visualization logic that duplicates the actual inference computation.

## 17. Definition of architectural readiness

The architecture is ready to move into the first implementation when:

- Tensor, model, inference, and backend responsibilities are agreed.
- CPU reference behavior can be specified with hand-checkable examples.
- Backend capability and fallback behavior are explicit.
- Memory ownership and synchronization responsibilities are understood.
- The 2-input / 2-output example can be described end-to-end.
- Hermit's first bounded integration surface is defined.
- Test and benchmark methodology is agreed.
- Unresolved decisions are recorded rather than hidden in implementation assumptions.

This document is a proposal for review. It does not authorize publication or a change to `master`.
