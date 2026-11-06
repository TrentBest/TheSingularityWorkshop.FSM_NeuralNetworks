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

Neither policy is selected yet. Choose only after the first concrete state-machine field defines its cardinality and invalid/uninitialized semantics. State indexes are local encoding values, not durable semantic identities. A stable state-identity-to-byte-index mapping must be versioned with the machine/model definition; reordering states must not silently reinterpret persisted or in-flight bytes.

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


## 9. DataWarehouse shelf ownership and neural record mapping (development addendum)

The DataWarehouse domain has clarified its performance-first parallelism contract: **independent shelf ownership**. A shelf is a page-aligned storage/metadata unit, not a synchronized work queue. Separate shelves may be processed in parallel when each is assigned to one worker/owner for the duration of access. Shared mutation of one shelf is unsupported unless a caller supplies external synchronization. A worker must stop using every ref or raw address obtained from a shelf before that shelf is freed or disposed. This is an ownership contract, not a claim that DataShelf is internally thread-safe.

### Neural storage mapping

Use DataWarehouse shelves for high-volume, fixed-layout unmanaged records whose access patterns benefit from this model. Partition independently writable regions into separately owned shelves where practical; do not have multiple workers mutate the same shelf's allocator, bitmaps, counters, or records concurrently. A read-only sharing phase is valid only while the owner guarantees no mutation, free, or disposal for the phase's full duration.

Keep these identities distinct:

- **Domain identity:** stable neural model, tensor, parameter, node, or FSM-state identity owned by NeuralNetworks.
- **DataWarehouse identity:** a generation-checked handle locating one live fixed-size record in one shelf. It is an in-memory locator, not a durable ID.
- **Device identity:** backend-specific resource/region identity for a GPU allocation. It must not be inferred from a DataHandle or raw host pointer.

A handle can locate metadata describing a tensor or region, but it does not imply that an arbitrarily large tensor payload should be stored as one DataShelf<T> record. Separate compact metadata from bulk tensor storage. Use typed shelves for fixed-size metadata/state records; represent bulk tensor storage through a separately specified DataWarehouse-owned region/resource contract that defines size, alignment, host/device residency, dirty-region direction, completion, and disposal. That region contract is not implemented yet and must not be simulated by leaking BaseAddress or by giving a backend adapter an unrelated long-lived allocator.

### Lifetime and scheduling rules

- Foundry schedules work while preserving exclusive writable ownership of each shelf/region, or delegates coordination to a higher layer that explicitly owns shared mutation.
- DataWarehouse remains responsible for storage lifetime; backend adapters report completion before a device resource/region can be reused or disposed.
- Do not dispose a shelf or invalidate its handles while CPU workers or GPU work can still access the corresponding data.
- A DataHandle is not a lease and does not prevent reclamation. The current API returns refs whose lifetime is not bounded by a method call; callers must enforce worker quiescence before free/dispose.
- BaseAddress is documented by DataWarehouse as an unsafe diagnostic escape hatch. NeuralNetworks must not use it as its normal integration API.
- DataWarehouse's allocation/lifetime fixes and generation retirement improve correctness, but do not establish shared-shelf concurrency or GPU fence ownership.

### Byte-state encoding remains an explicit contract decision

The 0–255 representable range does not itself decide whether 255 is reserved. Keep the two policies open until the first concrete state field and its invalid/uninitialized semantics are specified: 256 valid values with no in-band sentinel, or 255 valid values with 255 reserved. Do not silently adopt the previous suggested default as a user-approved decision. In either policy, the semantic state identity and mapping version remain authoritative; the byte index is a local encoding.


## 10. Proposed neural record layout (design proposal; not an implementation)

This section makes the first DataWarehouse mapping concrete without pretending that the future bulk-region or GPU-resource API already exists. All record shapes below are conceptual contracts. Exact field widths, packing, and public types must be validated against the consuming model/runtime requirements before implementation.

### Fixed-size records in typed shelves

| Record | Suggested contents | Storage role |
|---|---|---|
| Model record | Stable model ID; immutable model-version ID; graph/root descriptor handle; lifecycle/version metadata | One fixed-size record per published model version |
| Node record | Stable node ID; model-version ID; operation kind; input/output descriptor handles; operation-specific metadata handle | Graph topology and execution metadata |
| Tensor descriptor | Stable tensor ID; model-version ID; element type; rank; dimensions/strides descriptor; logical byte length; bulk-region reference; layout/version flags | Describes a tensor without embedding its payload |
| Parameter descriptor | Stable parameter ID; owning model-version ID; tensor descriptor handle; trainability/role flags; parameter version | Parameter identity and association |
| FSM state-map descriptor | Stable FSM ID; mapping version; state count; encoding policy (256-valid or sentinel-reserved); mapping-table reference | Governs interpretation of compact state bytes |
| Work/partition descriptor | Stable work-partition ID; model-version ID; owned shelf/region references; read/write mode; completion generation | Lets Foundry schedule explicit ownership units |

The table deliberately separates stable domain IDs from DataHandle values. Handles are short-lived, generation-checked locators into a particular DataWarehouse instance; they must not be serialized as durable model identity or treated as authorization.

Typed shelves should contain fixed-size unmanaged metadata records. Avoid putting variable-length arrays, arbitrary tensor payloads, managed references, or backend objects inside these records. A descriptor may refer to a bulk region, but that reference is only conceptual until DataWarehouse defines and implements the region/resource contract.

### Bulk tensor payloads

Large weights, activations, gradients, and other tensor values are not one-record DataShelf<T> allocations. Their storage requires a DataWarehouse-governed region contract with, at minimum:

- logical length and element type, plus checked physical byte length;
- alignment, allocation extent, and any page/tiling/row-pitch overhead;
- host residency, device residency, or an explicitly defined dual representation;
- region identity and generation distinct from both neural domain IDs and DataHandle;
- dirty-region tracking with an unambiguous source/destination direction;
- ownership transfer or borrow rules for CPU workers and GPU submissions;
- completion/fence ownership, safe reuse, cancellation, and disposal behavior;
- accounting for payload bytes, alignment waste, metadata, alive/dirty bitmaps, and staging copies.

Until that contract exists, the architecture may describe intended payload placement but must not allocate unrelated long-lived backend buffers and call them DataWarehouse-owned.

### Discrete FSM state pages

A bounded FSM state field is a good candidate for compact fixed-layout storage. The logical representation is one unsigned byte per state element. The physical representation may be a typed byte shelf on the host and, when supported by a selected backend, an integer texture channel on the device.

The state-map descriptor must define:
- stable semantic state IDs and their mapping to byte indexes;
- a mapping version that changes whenever interpretation changes;
- whether 255 is a valid index or a reserved invalid/uninitialized value;
- behavior for unknown IDs, unknown mapping versions, and out-of-range bytes;
- the page/region ownership unit used by Foundry so writable state updates do not race on one shelf/region.

Do not put semantic state IDs directly into the byte field. The byte is a versioned local encoding. A state-map change must either migrate the stored bytes or reject them as belonging to an incompatible mapping; silently reinterpreting old bytes is invalid.

### Capacity and accounting model

For a fixed-size record type of size S, capacity N, and configured alignment/page unit P, the payload request is N × S, while the actual allocation extent is rounded according to DataWarehouse's allocation rule. Account separately for:

1. record payload bytes;
2. rounded allocation/alignment overhead;
3. alive and dirty bitmap storage;
4. generation and free-slot metadata;
5. shelf/region descriptors and stable-ID mapping tables;
6. bulk tensor payloads and any staging/transfer allocations;
7. device allocation granularity, texture row pitch, and backend metadata.

Do not publish a single memory-footprint estimate until actual record layouts, capacities, mapping-table representation, and backend allocation rules are selected. The formulas above are a sizing checklist, not a measured result.

### Implementation gates

1. Review and accept the logical record fields and stable-ID/version rules.
2. Confirm the DataWarehouse region/provider contract in coordination with DataWarehouse issue #7.
3. Select the first concrete FSM state field and decide the 255-versus-256 encoding policy from its semantics.
4. Define exact unmanaged layouts and tests for serialization/round-trip, handle generation, mapping-version rejection, and independent-shelf ownership.
5. Implement host-side metadata mapping only after those contracts are accepted; GPU residency remains blocked on the region/provider and completion contract.
