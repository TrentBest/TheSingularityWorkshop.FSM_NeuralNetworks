# Neural Networks — Theory, Models, and Experiments

**Status:** Learning and design guide for the accepted NeuralNetworks MVP. The browser lesson remains an educational calculator; a first .NET 10 CPU reference evaluator now exists under `src/FsmNeuralNetworks`. Its build/test verification is pending the dedicated GitHub Actions workflow.

## 1. The central idea: a parameterized function

A neural network is a function whose structure and numerical parameters define how inputs become outputs:

\[
f_{\theta}: \mathbb{R}^{n} \rightarrow \mathbb{R}^{m}
\]

Here, \(x\) is an input vector and \(\theta\) is the collection of parameters—usually weights and biases. Inference evaluates the function with fixed parameters. Training is a different process: it changes parameters in response to an objective and data.

This distinction matters. A network can perform useful inference without training code, a dataset pipeline, or an optimizer. The first implementation should prove inference before adding learning.

## 2. A neuron is a weighted sum plus a bias

For input features \(x_i\), weights \(w_i\), and bias \(b\), one unit computes:

\[
z = b + \sum_{i=0}^{n-1} x_i w_i
\]

Weights express how strongly each input contributes. The bias shifts the result. An activation function then transforms the affine result.

For ReLU:

\[
\operatorname{ReLU}(z)=\max(0,z)
\]

ReLU leaves positive values unchanged and maps negative values to zero. It is simple, inexpensive, and useful for introducing nonlinear behavior when composed across layers.

## 3. The shared two-input / two-output experiment

The repository's acceptance fixture uses:

\[
x = [2,1],\quad
W = \begin{bmatrix}2&1\\-1&3\end{bmatrix},\quad
b = [-1,2]
\]

Rows of \(W\) correspond to output units; columns correspond to input features. Therefore:

\[
z_0 = (2\cdot2)+(1\cdot1)-1=4
\]
\[
z_1 = (2\cdot-1)+(1\cdot3)+2=3
\]

Then:

\[
y=\operatorname{ReLU}(z)=[4,3]
\]

The same fixture should be used by the browser lesson, CPU reference evaluator, trace tests, and later backend-conformance tests. A shared fixture makes disagreements visible rather than hiding them behind different examples.

## 4. Why a graph, not merely a list of layers?

A layered feed-forward network is a useful first model, but neural computation is more naturally described as a directed graph:

- Nodes are operations such as Input, Dense, and ReLU.
- Ports define named inputs and outputs, including shape and element type.
- Edges connect one node's output port to another node's input port.
- A valid first-slice graph is acyclic and can be evaluated in topological order.

A layer list is one convenient way to author a subset of graphs. A graph representation leaves room for branching, merging, normalization, and other operations without redefining the whole model format. Cycles and recurrent state are deliberately outside the first DAG evaluator; they require explicit temporal semantics.

    flowchart LR
      X[Input tensor x] --> D[Dense: xWᵀ + b]
      W[Weights W] --> D
      B[Bias b] --> D
      D --> Z[Pre-activation z]
      Z --> R[ReLU]
      R --> Y[Output tensor y]

## 5. Tensor meaning is not storage location

A tensor has logical meaning: shape, element type, dimension ordering, and values. Physical storage is a separate concern: allocation, layout, strides, memory location, alignment, and transfer handles.

For the MVP, values use IEEE 754 binary64 (double) so calculations are easy to inspect and compare. This is a correctness and teaching choice, not a claim that binary64 is the fastest GPU representation. A later backend can use another precision only under an explicit conformance profile with measured numerical tolerances.

The DataWarehouse is the intended storage foundation. NeuralNetworks owns tensor semantics; it should not quietly create a competing general-purpose memory manager.

## 6. Where FSM_API 2.0 fits—and where it does not

FSM_API's string and integer representations describe FSM identity and runtime state storage. Integer backing can make state identity and indexed lookup compact and direct. It does **not** mean neural weights, activations, gradients, or tensor values should be converted to integers.

Keep the domains distinct:

| Concern | Representation |
|---|---|
| FSM definition/state identity | String-backed for readable authoring; integer-backed for the appropriate runtime path |
| Neural node and port identity | Stable semantic identifiers, with local indexes available internally |
| Weights, biases, inputs, activations | Numerical tensor values; binary64 in the first reference contract |
| Graph execution order | Validated topological plan |
| Evaluator lifecycle | May be coordinated by an FSM when a real lifecycle/state contract benefits from it |

Do not create one FSM state per neuron or per multiply-add. That would turn a numerical dataflow engine into a callback scheduler. The neural graph evaluator performs the math; FSM_API may coordinate a bounded evaluator lifecycle when that integration is justified and benchmarked.

The integration must use the actual public 2.0 API and preserve the distinction between the two independent backing registries. It must not assume a string identifier is implicitly converted to an integer identifier.

## 7. Inference versus training

Inference evaluates fixed parameters:

\[
y=f_{\theta}(x)
\]

Training seeks parameters that reduce a loss over examples:

\[
\theta^{*}\approx\arg\min_{\theta}\;\frac{1}{N}\sum_{k=1}^{N}
\mathcal{L}(f_{\theta}(x_k),t_k)
\]

That introduces new concepts: target values, loss functions, gradients, differentiation, optimizers, batches, datasets, random seeds, checkpointing, and reproducibility. These should be introduced as a separate theory and implementation milestone after deterministic inference is proven.

A useful learning progression is:
1. Forward pass: calculate outputs from known inputs and parameters.
2. Loss: quantify the difference between prediction and target.
3. Gradient: determine how a parameter affects the loss.
4. Backpropagation: efficiently apply the chain rule through the graph.
5. Optimization: update parameters under an explicit learning-rate and stability policy.
6. Evaluation: measure generalization on data not used for parameter updates.

## 8. Numerical honesty and reproducibility

The reference evaluator should reject non-finite inputs and parameters, detect non-finite intermediate results, and report failure rather than returning a misleading successful output. Dense accumulation order is fixed for the reference path. A model version is immutable for the duration of an inference so one run cannot observe a mixture of old and new parameters.

A trace should come from the same execution that produced the output. It may record node identity, output port, shape, values or summaries, and model version. Trace capture is optional and bounded: observability must not become an unbounded memory allocation.

## 9. Experiments to build toward

| Experiment | What it teaches | Evidence to expose |
|---|---|---|
| Change inputs | Forward propagation | Each weighted term, bias, pre-activation, output |
| Change one weight | Parameter sensitivity | Before/after output and delta |
| Add a hidden layer | Composition and nonlinearity | Graph topology and per-node tensors |
| Compare activations | Nonlinear function behavior | Curves, output values, numerical edge cases |
| Train a tiny model | Loss and gradient descent | Loss curve, gradients, parameter history |
| Scale the workload | Compute vs. orchestration | Operations, allocations, throughput, latency |
| Compare backends | Numerical and hardware trade-offs | Device, precision, transfer time, kernel time, tolerances |

Performance claims must separate model math from orchestration, validation, trace capture, storage, allocation, and device transfer. Benchmark the same workload and verify correctness before comparing speed.

## 10. Status and implementation boundary

The browser lesson is an educational calculator for one fixed Dense + ReLU fixture. The first .NET 10 CPU reference evaluator has now been added for the accepted bounded stateless DAG profile, with initial tests and a dedicated CI workflow. Treat it as an initial implementation slice until exact-commit CI evidence is green. GPU acceleration, training, recurrent execution, and live Hermit integration are not implemented claims.

- [Architecture](ARCHITECTURE.md)
- [MVP contract decisions](MVP_CONTRACT_DECISIONS.md)
- [FSM_API 2.0 integer-backing integration](FSM_API_2.0_INTEGER_BACKING_INTEGRATION.md)
- [DataWarehouse and byte-state storage](DATAWAREHOUSE_AND_BYTE_STATE_STORAGE.md)
- [Workshop runtime integration](WORKSHOP_RUNTIME_INTEGRATION.md)


## 15. Embodied control: intention, coordination, and joint-level actuation

Hermit's embodied controller is a hierarchical control problem, not a requirement to put an FSM around every neuron or to make one neural network directly animate every joint.

The intended hierarchy is:

1. **Intent / goal:** Hermit wants to reach a location, touch an object, grasp it, walk, or maintain balance.
2. **Coordination:** a higher-level controller turns that intent into coordinated movement goals—such as a reach trajectory, posture, gait phase, or balance correction.
3. **Joint supervision:** each supported joint may have a small, bounded FSM that manages its local control mode, legal range, command limits, and transitions.
4. **Actuation:** the learned controller proposes muscle-like activation or target commands. Where a joint has opposing actuators, the two activation channels can represent the opposing sides of the joint.
5. **Physical outcome:** Physics computes movement, contact, load, collision, and failure. The next observation closes the loop.

The FSMs are not a substitute for numerical computation. They supervise local control state and enforce discrete constraints; the neural evaluator performs its declared numerical operations. Do not create one FSM state per neuron or per multiply-add. Do not assume that every joint must immediately have an independently learned policy.

### Observation and action boundary

A future embodied-control adapter should make the following evidence explicit, where the owning subsystem actually supplies it:

- stable rig, joint, and actuator identities;
- joint position, legal range, and velocity;
- current controller mode and accepted command;
- actuator activation/effort and configured limits;
- end-effector target versus achieved pose;
- contact, load, balance, and collision evidence from authoritative providers;
- task intent, coordination target, and timestep/episode identity.

The learned policy may propose bounded activations or target trajectories. Joint supervisors validate and constrain those proposals. Physics remains authoritative for the result. Requested, accepted, and achieved values must remain separately inspectable.

Muscles contract to generate force; tendons transmit that force to the skeleton; ligaments stabilize and constrain joints. A software actuator model may simplify these mechanics, but documentation and telemetry should not conflate their biological roles.

### Rig strategy and first experiment

The intended reference embodiment is the fully articulated Unity Input Sample armature, including finger joints and excluding toe joints. Treat this as a rig-source and compatibility target to audit—not as evidence that the rig has already been imported or that every joint is currently supported.

Gestures owns reusable armature, joint, kinematic-procedure, and controller observation/action contracts. Its current teachable-armature issue (#32) is deliberately a two-link planar reference slice. Keep that fixture small and deterministic; track full-rig mapping as a separate expansion so the initial contract is not silently overgeneralized.

A sensible first learned task is a bounded reach-and-touch experiment on a small subset of the rig. Walking, balance, and whole-body coordination should follow only after the observation/action loop, joint limits, actuator semantics, reset behavior, and physical feedback are measurable.

### Workload implications for the evaluator

The embodied workload introduces repeated, time-stepped inference and potentially many joint/actuator outputs. It does not justify changing the logical tensor model or weakening the current CPU reference contract. It does mean the evaluator and later backends must be measured against both the tiny educational fixture and representative batched control workloads.

Keep these costs separate in benchmarks: graph validation, inference arithmetic, allocations, trace capture, controller/FSM orchestration, sensor adaptation, and any future device transfer. Recurrent or stateful neural execution remains a separate contract; joint FSM state must not be smuggled into the current acyclic graph evaluator.

**Status:** architecture direction only. No live Hermit controller, imported Unity rig, per-joint FSM array, training loop, or GPU motor-control execution is claimed by this document.
