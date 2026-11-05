# DataWarehouse Memory and Byte-Packed FSM State Storage

**Status:** Architecture direction accepted; integration contract and GPU residency remain to be implemented and verified.
**Branch:** `development` only.
**Publication:** No NuGet publication is authorized.
**Related domain:** [TheSingularityWorkshop.DataWarehouse](https://github.com/TrentBest/TheSingularityWorkshop.DataWarehouse)

## 1. Decision

NeuralNetworks must use **DataWarehouse as its memory/storage foundation**. NeuralNetworks must not introduce a parallel general-purpose memory manager for model data, runtime state, or reusable fixed-record storage.

DataWarehouse owns the storage primitives and lifecycle. NeuralNetworks owns the meaning and layout of neural data stored through those primitives. A GPU execution backend owns the device-specific commands needed to create, bind, synchronize, and execute against GPU resources, but those resources must participate in an explicit DataWarehouse-governed ownership/lifetime contract. The current DataWarehouse implementation does not yet provide GPU resource interop; do not claim that it does.

This decision is consistent with DataWarehouse's narrow role as a page-aligned data lake with fixed-size shelves, handles, and external alive/dirty tracking. It remains security-agnostic. FSM_COS composes security MicroBundles and higher-level policy; neither NeuralNetworks nor DataWarehouse should embed authorization logic in storage.

## 2. Ownership boundaries

- **DataWarehouse:** storage allocation, shelves, slots, handles, liveness, dirty tracking, and storage lifetime.
- **NeuralNetworks:** model/tensor semantics, parameter identity/versioning, graph execution semantics, state encoding definitions, and validation.
- **GPU backend adapter:** device-specific texture/buffer allocation and binding, transfer/dispatch/synchronization, and capability reporting. It must expose device-resource lifetime through the agreed DataWarehouse integration seam rather than creating an invisible second owner.
- **Foundry:** execution lifecycle, scheduling, cancellation, and outcomes.
- **FSM_COS:** composition of capabilities and security MicroBundles.
- **Hermit:** goals, permissions, planning, and action authority.

A valid DataWarehouse handle proves storage identity/liveness only. It is not an authorization token.

## 3. Byte-sized FSM state encoding

FSM state can be represented as a compact integer index when a machine's state set is bounded. The intended GPU representation is one unsigned byte per state value, stored in an integer texture channel.

A byte has 256 representable values (0 through 255). The encoding contract must explicitly choose one of these policies:

- **All values valid:** state indexes 0–255, for 256 states; no byte value is available as an in-band sentinel.
- **Sentinel reserved:** state indexes 0–254, for 255 states; 255 represents invalid, uninitialized, or no-state.

The initial implementation should use the sentinel-reserved policy unless a concrete state-machine contract requires all 256 values. State indexes are local encoding values, not durable semantic identities. A stable state-identity-to-byte-index mapping must be versioned with the machine/model definition; reordering states must not silently reinterpret persisted or in-flight bytes.

The byte representation is for discrete FSM state identity only. It must not be generalized to neural weights, activations, gradients, or arbitrary tensor values, whose element type and precision follow the tensor/backend contract.

## 4. Texture representation

For GPU texture storage, use an integer texture format/channel so byte values are preserved exactly. Do not use normalized or sRGB formats for state IDs, and do not use filtering or interpolation on categorical state values.

Examples of possible physical layouts (subject to backend capability and hardware validation):

- One state per texel using a single-channel 8-bit unsigned integer format (for example, an R8 unsigned-integer texture).
- Multiple independent state fields per texel using separate 8-bit unsigned-integer channels (for example, four state bytes in an RGBA 8-bit unsigned-integer texel).

The logical contract is a sequence/grid of byte state indexes. Texture dimensions, channel packing, row pitch, tiling, and resource usage are backend-specific physical layout decisions. A texture is not automatically the right resource for every workload; it is the targeted representation for the GPU-resident state field when texture access semantics and measured hardware behavior justify it.

No conversion through floating-point normalized sampling is permitted for the categorical state path. Tests must prove exact round-trip values, including boundary values 0, 254, and 255 under the selected sentinel policy.

## 5. DataWarehouse relationship and current capability gap

The current DataWarehouse development implementation provides:

- A DataWarehouse that owns a DataLake and registered shelves.
- Fixed-capacity unmanaged DataShelf<T> storage.
- Page-aligned native allocation.
- Slot handles containing shelf identity, slot index, and generation.
- External alive and dirty bitmaps.
- Dirty-slot enumeration and deterministic shelf disposal.

The current implementation is CPU/native-memory storage. It does not currently promise GPU texture allocation, device-memory ownership, resource registration, device fences, or safe concurrent mutation of one shelf. Its current shelf API is not itself a GPU texture API.

Therefore the integration must be designed explicitly. At minimum, investigate a DataWarehouse-owned resource/region abstraction that can represent host shelves and registered device-resident allocations while preserving one authoritative lifetime/ownership model. Do not add GPU-specific concepts to the neural mathematical core, and do not bypass DataWarehouse by silently allocating unrelated long-lived state stores in a backend adapter.

The design must distinguish:
- authoritative logical state and its version;
- host-resident DataWarehouse storage;
- device-resident texture/buffer representation;
- dirty/modified regions and transfer direction;
- resource completion and safe reuse/disposal;
- per-shelf/per-region worker ownership and synchronization.

The current DataShelf contract is not thread-safe for concurrent mutation of a shared shelf. GPU/CPU work partitioning must respect independent shelf/region ownership or provide synchronization above the current DataWarehouse core.

## 6. Required integration sequence

1. **Contract mapping:** map neural model, parameter, tensor, and discrete FSM state records to DataWarehouse shelf/handle semantics. Include byte accounting for metadata, bitmaps, alignment, and reserved capacity.
2. **DataWarehouse collaboration:** review the existing DataWarehouse API and agree whether GPU resource registration belongs in DataWarehouse itself or in a narrowly defined extension/provider contract owned by it.
3. **State encoding:** define stable state identity mapping, byte index assignment, sentinel policy, mapping version, and invalid-state behavior.
4. **GPU resource contract:** define integer texture format requirements, dimensions/strides, ownership, transfer/dirty semantics, synchronization, and disposal.
5. **Conformance tests:** verify DataWarehouse handle/lifetime behavior, exact byte round-trips, mapping-version rejection, and CPU/GPU state agreement.
6. **Measurement:** separately measure host storage access, packing/unpacking, transfer, dispatch, synchronization, and device-side state access on named hardware.
7. **Consumer integration:** expose the bounded capability through Foundry; keep Hermit authority and FSM_COS composition boundaries intact.

## 7. Guardrails

- No parallel general-purpose memory manager in NeuralNetworks.
- No assumption that current DataWarehouse already supports GPU textures.
- No implicit CPU fallback when an accelerator is required.
- No float-normalized texture sampling for discrete state IDs.
- No treating state byte indexes as stable IDs across model versions without an explicit mapping/version contract.
- No reducing neural numeric precision to byte merely because FSM state fits in a byte.
- No shared-shelf concurrent mutation without an explicit synchronization/ownership design.
- No vendor-specific dependency in the neural semantic core.
- No master changes, branch reconciliation, or NuGet publication as part of this work.

## 8. Open questions for the integration issue

- Does the initial byte encoding reserve 255 as an invalid/uninitialized sentinel (255 valid states), or use all 256 byte values (256 valid states)?
- What is the first concrete FSM state field and its per-cell/per-node cardinality?
- Should GPU-resident resources be registered directly with DataWarehouse, or through a DataWarehouse-owned backend resource provider?
- Which target GPU and API will validate integer texture support and exact state access?
- What is the required consistency model between host shelf state and GPU texture state?
