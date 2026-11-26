<!-- The Singularity Workshop animated moniker -->
<p align="center"><a href="https://github.com/TrentBest"><img src="https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Visualize/development/docs/assets/moniker.svg" alt="The Singularity Workshop animated moniker" width="720" /></a></p>

# TheSingularityWorkshop.FSM_NeuralNetworks

[![Sith Policy](assets/sith-policy.svg)](https://github.com/TrentBest/TheSingularityWorkshop.FSM_NeuralNetworks/actions/workflows/sith-policy.yml)

GPU-first neural computation and educational experimentation for The Singularity Workshop.

## 01 — Explore NeuralNetworks

**[Open the interactive NeuralNetworks learning experience](https://trentbest.github.io/TheSingularityWorkshop.FSM_NeuralNetworks/)** — experiment with the documented two-input/two-output Dense + ReLU fixture and inspect the architecture boundaries. The browser lesson is an educational calculation, not the library evaluator, a GPU backend, or a live Hermit integration.


## 1. Purpose and status

NeuralNetworks owns backend-neutral neural model semantics and bounded computation: tensors, operations, model graphs, inference, later learning, and execution evidence.

The design is intentionally **GPU-first, not GPU-only**. The CPU reference path establishes correctness and portability; it is not evidence of GPU acceleration and must never silently replace a requested accelerator.


**Current status:** the owner has accepted the MVP contract on `development`. The first implementation slice now exists as a .NET 10 library and test project: bounded stateless DAG, binary64 CPU reference, immutable parameter snapshots, finite-value validation, explicit input/work/trace/time budgets, and opt-in traces from the evaluated graph. The dense/ReLU `[4, 3]` fixture and initial validation/budget tests are included. Exact-commit CI verification is now green: the .NET 10 restore/build succeeded with zero warnings and errors, and all 11 initial tests passed. A second deterministic planar-reach workload has now been added to exercise the same evaluator with joint-error observations, bounded kinematic target proposals, repeatability, trace inspection, and consumer-side command clamping. This is a conformance fixture, not a physical simulation or a live Hermit controller; its new test is awaiting the next exact-commit workflow result. Recurrent/stateful execution remains deferred behind a separate contract. No GPU backend has been selected, and no NuGet publication is authorized.

## 2. Architecture at a glance

- **Model semantics:** validated directed acyclic computation graphs with stable node and port identities.
- **Initial evaluator:** `Input`, `Dense`, and `ReLU`; binary64 values; immutable model-version snapshots; finite enforceable execution budgets.
- **Execution evidence:** optional, bounded traces captured from the same execution that produces the result.
- **Backend boundary:** backend-neutral contracts; actual backend/device and fallback behavior must be observable.
- **Memory:** DataWarehouse is the storage foundation. NeuralNetworks does not create a competing general-purpose memory manager.
- **Authority:** NeuralNetworks computes; Hermit owns goals, planning, permissions, and action authority.

The initial hand-checkable fixture is:

```text
x = [2, 1]
W = [[ 2, 1],
     [-1, 3]]
b = [-1, 2]

Dense(x, W, b) = [4, 3]
ReLU([4, 3])   = [4, 3]
```

The expected output is `[4, 3]`. This fixture is intended to become the shared CPU, trace, and future backend-conformance example.

## 3. Source and verification

- Solution: [`FsmNeuralNetworks.sln`](FsmNeuralNetworks.sln)
- CPU reference evaluator: [`src/FsmNeuralNetworks/NeuralGraph.cs`](src/FsmNeuralNetworks/NeuralGraph.cs)
- Acceptance and boundary tests: [`tests/FsmNeuralNetworks.Tests/EvaluatorTests.cs`](tests/FsmNeuralNetworks.Tests/EvaluatorTests.cs)
- CI: [NeuralNetworks .NET workflow](.github/workflows/neuralnetworks-dotnet.yml)

The current implementation is an initial reference slice, not yet a GPU backend, training engine, recurrent runtime, or Hermit controller. Do not treat a commit as proof of a passing build; use the exact-commit workflow result.

## 4. Documentation

Start with the [Neural Networks theory and experiments guide](Documentation/Architecture/NEURAL_NETWORKS_THEORY_AND_EXPERIMENTS.md) for the mathematics, graph model, tensor semantics, inference-versus-training boundary, and planned hands-on experiments.


| Document | Responsibility |
|---|---|
| [Architecture](Documentation/Architecture/ARCHITECTURE.md) | Domain boundaries, model vocabulary, execution flow, backend strategy, testing, and milestones |
| [MVP contract decisions](Documentation/Architecture/MVP_CONTRACT_DECISIONS.md) | Exact first-slice numerical, graph, budget, trace, and acceptance-fixture rules |
| [DataWarehouse and byte-state storage](Documentation/Architecture/DATAWAREHOUSE_AND_BYTE_STATE_STORAGE.md) | Storage ownership, neural record mapping, bulk tensor regions, and discrete FSM-state encoding |
| [Workshop runtime integration](Documentation/Architecture/WORKSHOP_RUNTIME_INTEGRATION.md) | FSM_COS composition, FSM_Execution lifecycle, provider boundaries, and Hermit authority |

These documents distinguish accepted architectural direction, proposed implementation details, and capabilities that do not yet exist. In particular, the DataWarehouse GPU resource/region contract and the FSM_Execution RuntimeAssembly execution handoff are not claimed to be implemented.

## 5. Workshop boundaries

| System | Owns |
|---|---|
| NeuralNetworks | Neural semantics, validation, bounded evaluation, and computation traces |
| DataWarehouse | Storage allocation, shelves, handles, liveness, dirty tracking, and storage lifetime |
| FSM_COS | MicroBundle composition, validation, loading, and arbitration |
| FSM_Execution | Execution lifecycle, scheduling, dispatch, cancellation, cleanup, and outcomes |
| FSM_API | FSM mechanics where an actual state/context contract is required |
| Hermit | Goals, observations, policy, permissions, planning, and action authority |
| Gestures / Physics | Optional embodiment and environment interfaces for later experiments |
| Visualize | Presentation of actual execution evidence; it must not recompute neural results |

The neural graph remains internal to NeuralNetworks and is evaluated as one bounded capability. It is not automatically expanded into FSM states or FSM_Execution workflow steps.

## 6. Development and guardrails

- Active work belongs on `development`; preserve `master` as the released/NuGet-aligned state.
- Keep exactly the `master` and `development` branches.
- Do not add a vendor-specific GPU dependency to the neural semantic core.
- Do not introduce a parallel general-purpose memory manager.
- Do not claim GPU acceleration without named hardware and end-to-end measurements.
- Do not add training, recurrent execution, a model zoo, or broad framework dependencies before the inference foundation is proven.
- Do not publish a NuGet package without explicit authorization.

**Implementation gate:** satisfied for the scoped CPU evaluator by explicit owner acceptance. See [the accepted MVP contract](Documentation/Architecture/MVP_CONTRACT_DECISIONS.md).

---

![The Singularity Workshop](https://raw.githubusercontent.com/TrentBest/FSM_API/master/Documentation/Branding/TheSingularityWorkshop.png)
