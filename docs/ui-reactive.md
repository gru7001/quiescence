## Reactive UI: Direct Render vs UI Engine

### Problem

We want UI (Godot) to display derived views of simulation state (e.g. a board grid), and to update
automatically when the underlying simulation state changes.

The simulation already has a reactive engine (procedures + dependency-tracked reads + quiescence).

The design question is whether the UI should:

* be updated directly as a side effect of simulation procedures, or
* have its own reactive engine and explicit UI state (a middle layer).

---

## Option 1 — Direct Render (no UI state)

### Structure

* A simulation procedure (or decision driver callback) reads sim state.
* It updates Godot nodes directly (grid buttons, labels, etc.).

### Properties

* Minimal moving parts.
* No cross-engine coordination.
* Rendering logic and view-model logic are fused.
* Harder to compose overlays (hover/selection/reachability/path preview) without growing a “god renderer”.

---

## Option 2 — Separate UI Reactive Engine (explicit UI state)

### Structure

Introduce a second reactive engine to drive UI state:

* UI State: separate `State` + `ExecutionContext` + `Scheduler` (the “UI engine”).
* UI Keys: typed keys holding render models (e.g. `BoardGridModel`, `CanMove`, selection, hover).
* UI Render Procedure: reads UI keys and writes to Godot nodes (imperative render).

### Bridge (Sim → UI)

The bridge is a *simulation procedure* that:

* reads simulation state (tracked by the sim engine), then
* writes a derived render-model into the UI engine via a scoped UI run:
  * `uiScheduler.RunScoped(() => uiCtx.Write(BoardGridModelKey, model))`

Because the bridge is a sim procedure, it reruns automatically when the sim keys it read change.
Because the bridge writes UI keys, the UI engine reruns the UI render procedure automatically.

### Properties

* Separates “what to show” (UI state / render model) from “how to draw it” (Godot node updates).
* Enables layering/composition: multiple procedures can contribute to UI state (overlays, selection).
* Enables diffing: renderer can compare old/new render model and update only what changed.
* Adds additional machinery: UI engine + UI keys + cross-engine `RunScoped` writes.

### Constraint (no cycles)

To avoid feedback loops:

* Sim procedures may write UI keys (via UI scoped runs).
* UI procedures should not write sim keys.

This keeps dependencies one-way: `sim -> ui`.

---

## Decision Rule of Thumb

Use **Direct Render** when:

* the UI is simple, and
* you do not need persistent UI-derived state beyond what is already in the sim.

Use a **UI Reactive Engine** when:

* you expect overlays, selection/hover, tooltips, path previews, animations, or multiple independent UI views, and
* you want UI behavior to be explainable as a pure function of explicit UI state.

