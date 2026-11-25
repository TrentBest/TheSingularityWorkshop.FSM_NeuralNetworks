# NeuralNetworks in the Workshop Runtime

**Status:** architecture integration direction; not a claim of implemented end-to-end integration.

## The important distinction

NeuralNetworks is a computational capability. It is not the Workshop's application runtime, composition kernel, workflow engine, or host.

| System | Owns | NeuralNetworks relationship |
|---|---|---|
| MicroBundleDomain | Neutral identity, dependencies, providers, configuration, load and arbitration contracts | Optional capability packaging seam; neural semantics stay in NeuralNetworks |
| FSM_COS | Resolve, validate, load, arbitrate MicroBundles; return RuntimeAssembly | Composes the requested neural capability with its dependencies |
| FSM_Execution | Execution lifecycle, scheduling orchestration, eligibility, dispatch, cancellation, cleanup, state and outcomes | Executes bounded neural work through a supported provider/adapter |
| FSM_API | Finite-state-machine and context mechanics | Used only where a real integration needs FSM mechanics |
| Host | Environment, permissions, capability registration, platform resources | Supplies and authorizes providers/resources |
| GUI / Visualize | Presentation and visualization | Presents status and actual neural execution evidence; does not execute or recompute the model |
| Hermit | Goals, observations, policy, permissions, and action authority | Requests bounded computation; interprets results without delegating authority to the model |

## Intended flow

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
                |
                v
   registered execution provider
                |
                v
       NeuralNetworks evaluator
                |
                v
    result + optional execution trace
                |
                v
   Host / Hermit / Visualize consumer
~~~

The boundaries are deliberately non-interchangeable:

- A RuntimeManifest describes composition input; it is not a workflow definition.
- A RuntimeAssembly is the result of composition; it is not a running application or a workflow result.
- A FSM_Execution workflow is an ordered workflow contract; it is not a neural computation graph.
- An FSM_API process group is an FSM update mechanism; it is not a general tensor-operation scheduler.
- A neural computation DAG defines mathematical dependencies; it does not own host scheduling, cancellation, or application lifecycle.

## What NeuralNetworks owns

- Model and operation semantics.
- Logical tensor shape, type, indexing, and mathematical meaning.
- Model validation and immutable parameter versions.
- Deterministic evaluation semantics.
- Backend-neutral capability requirements and conformance contracts.
- Neural evaluation results and optional bounded traces from the actual execution.
- Training algorithms later, under separate data, gradient, optimizer, reproducibility, and resource contracts.

The initial neural graph remains internal to NeuralNetworks. It is evaluated in deterministic topological order as one bounded capability. Do not make each graph node a FSM_Execution workflow step or FSM state by default. That would couple mathematical representation to runtime scheduling and add orchestration overhead. If later profiling shows a need for finer-grained scheduling, represent producer/consumer dependencies explicitly and let FSM_Execution interpret declared scheduling intent; priority must never violate data dependencies.

## What FSM_Execution owns

FSM_Execution owns the authoritative lifecycle and scheduling decisions around submitted work: acceptance, start, eligibility, dispatch, observable progress/outcomes, cancellation, and cleanup. Concrete providers perform work on available resources. NeuralNetworks does not create its own competing run coordinator, global scheduler, host loop, or cancellation authority.

The current FSM_Execution WorkflowRunner is a sequential named-handler workflow executor with validation-before-side-effects and structured outcomes. It is not yet the RuntimeAssembly executor. The typed RuntimeAssembly handoff is tracked in [FSM_Execution issue #23](https://github.com/TrentBest/TheSingularityWorkshop.FSM_Execution/issues/23); composed MicroBundle scheduling intent is tracked in [FSM_Execution issue #2](https://github.com/TrentBest/TheSingularityWorkshop.FSM_Execution/issues/2). These are upstream integration dependencies, not capabilities this repository should duplicate.

FSM_Execution's bounded JSON workflow context is for exchanging run data. Its entry/byte/depth limits are not neural tensor, graph-work, intermediate-memory, or trace budgets. A FSM_Execution host can derive an explicit neural evaluation budget from host policy and pass it to NeuralNetworks; NeuralNetworks must enforce its own computation-specific limits.

## What FSM_COS owns

FSM_COS resolves requested MicroBundles, validates dependency closure, loads prerequisites before dependents, performs bounded arbitration, and returns a RuntimeAssembly. It does not execute the neural model or schedule application work. NeuralNetworks must not depend on FSM_COS just to construct or evaluate a model.

A neural capability can participate in composition through MicroBundleDomain when that packaging is useful. Keep the mathematical core independent of the composition host.

## Execution providers and accelerators

The evaluator defines what an operation means. A backend/provider defines how that operation is carried out on a resource.

- The CPU reference implementation establishes semantic correctness and deterministic acceptance fixtures.
- A future GPU backend must declare supported operations, element types, precision, memory/transfer behavior, synchronization, and cancellation granularity.
- The selected backend and device must be observable. An accelerator request must not silently fall back to CPU unless the caller explicitly permits fallback.
- Performance claims require named hardware, warm-up and workload details, transfer/dispatch/synchronization costs, memory measurements, and end-to-end comparison.
- No GPU SDK should be selected merely because it is familiar; first establish actual target hardware, OS, deployment, and maintenance constraints.

A FSM_Execution provider adapter is the likely integration seam, but its exact interface must be based on FSM_Execution's source-backed provider contracts and the RuntimeAssembly handoff work—not invented here.

## Consumers and optional integrations

- **Hermit** may request bounded inference and inspect its result/trace. It retains all goals, permissions, planning, and action authority.
- **Gestures** may provide a controlled movement/embodiment interface for a later experiment.
- **Physics** supplies environmental outcomes for repeatable experiments.
- **Visualize** renders trace evidence and model structure. It must not run a second copy of the neural math.
- **HeadlessAi / ProtocolAi / GrammarAi** may support agent/provider or message-contract workflows outside the neural mathematical core; they are not dependencies of the evaluator.
- **FSM_Serialization** may be used for an explicitly defined model/result representation once a versioned serialization contract is selected. It is not automatically the neural model schema.

These integrations should be added only when a concrete consumer needs them and a source-backed contract exists.

## Evidence and current limits

This document records intended architecture, not completed integration. In particular:

- NeuralNetworks does not yet consume a FSM_Execution execution-provider contract.
- FSM_Execution does not yet accept and execute FSM_COS RuntimeAssembly through the tracked typed handoff.
- No target GPU/runtime has been selected or verified.
- No training implementation or embodied Hermit experiment is claimed.

The integration order is therefore: settle the NeuralNetworks MVP contracts; implement and test the small evaluator; verify the FSM_Execution provider and RuntimeAssembly seams against their upstream work; then add a narrow end-to-end composed execution proof. Keep the neural core independently testable throughout.
