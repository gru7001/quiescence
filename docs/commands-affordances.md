## Addendum: Slot Domains as Contextual Affordances (High Level)

This note is a response/extension to `docs/commands.md`. It does **not** propose a concrete API.
It only sharpens the problem statement around practical progressive filling for UI/AI drivers.

### Reminder: what `commands.md` postulates

`commands.md` models a command schema `c` as:

- an ordered list of argument slots \(A_c = (a_1,\dots,a_n)\)
- each slot has a type \(T_{a_i}\) and a domain \(\Theta_{a_i}\)
- interaction is progressive filling via partial fillings \(\phi\), extendability, and next-step choices.

This already implies an “affordance interface”: the system must be able to answer questions of the form

- is a partial filling \(\phi\) extendable?
- what next bindings preserve extendability?

### Problem: \(\Theta_{a_i}\) is often not a static set

In implementation, many slot “domains” are not naturally described as a fixed, context-free set selected
by a simple tag (e.g. `ArgDomain.Item`). Instead, the admissible values depend on:

- the issuing vehicle/body (inventory membership, reachability, permissions),
- the current simulation state (occupancy, cooldowns, locks, quantities),
- other already-bound slots (joint dependence / conditional domains),
- and sometimes additional UI context (what object was pointed at), if not represented explicitly in values.

Examples of *contextual* slot domains (informal):

- **InventoryItem(body)**: items available in a specific body’s inventory.
- **NearbyContainer(body)**: containers reachable/adjacent to a specific body.
- **ContainerItem(container)**: items available in a specific container.

These are better thought of as *parameterized* domains, not a single global \(\Theta\).

### Problem: domains can be large or non-enumerable

Some commands admit domains that are impractical to enumerate:

- “teleport target tile” with unbounded range,
- “choose a number” without a small finite bound,
- “choose a tile” in a very large world.

In such cases, “next-step filling” cannot mean “list all candidates”. Progressive filling needs a way
to validate and guide choices without requiring full enumeration.

### Problem: UI gestures often bind multiple slots at once

Real interaction is frequently *argument-first* and *projection-driven*:

- Clicking an item row in a container UI naturally identifies **(container, item)** together.
- Clicking an item in inventory naturally identifies an **inventory item** without requiring a container slot.

So a single gesture may propose a partial filling that assigns multiple slots simultaneously.

This highlights two related concerns:

1) **Joint dependence** is not only “slot B depends on slot A”; a UI primitive may correspond to a tuple
   over several slots.
2) Any reliance on UI context to resolve underspecified values (“the item you clicked” without a stable
   domain identity) risks collapsing the semantic boundary between UI and simulation.

### Problem: “sensible presentation” is not derivable from coarse domain tags alone

A coarse domain tag (e.g. slot value type `Tile`, `Direction`, `Item`, `long`) can help select generic input controls, but it
cannot, by itself, determine:

- which composite pickers are valid (e.g. “ItemInContainer” vs “Item independent of Container”),
- which previews/overlays should be shown while hovering/filling (e.g. a direction-based cone/range preview),
- or which partial-fill gestures should be interpreted as which slot bindings.

These are not purely properties of the value type; they are properties of the command schema together with
state and partial filling.

### Restated goal

We want progressive filling to support both UI and AI drivers such that:

- partial fillings can be proposed from concrete world objects/gestures,
- extendability/admissibility can be checked without issuing the command,
- next-step guidance can be produced without requiring full enumeration when domains are huge,
- and UI-specific presentation policy (rendering, ordering, hover effects) remains separable from simulation
  semantics, even though the semantics must be queryable by agents.

This addendum only identifies pressures on the model; it does not choose where the corresponding mechanisms
should live (command definitions, separate domain services, UI matchers, etc.).

