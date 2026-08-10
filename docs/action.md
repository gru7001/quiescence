## Body and Action State

Behaviour is expressed by a body's **action state**: the actor-specific state value the body is currently in.

A body owns a single `ActionState` key. Writes to it drive a per-body change-triggered procedure (the **action dispatch procedure**) that runs the new state's `OnEnter` handler.

There is no separate "current action" identity: a body has only its current state, and the state's runtime type *is* the identity. Stages of a multi-stage activity are distinct state types defined together (e.g. `Punch.WindUpState`, `Punch.StrikeState`).

## Action State

An **action state** carries the instance-specific information needed to drive the body in that state, plus an `OnEnter` handler that executes when the state becomes current.

By convention:

* a stage's **world writes** happen in `OnEnter`
* time-driven progression is implemented by **scheduling an event whose only effect is to write the next state** on the body
* the next state's `OnEnter` performs that stage's world writes

This convention makes late timer firings inert when the body's dispatch procedure has been uninstalled: the scheduled callback writes a new state, but no `OnEnter` runs.

## Save/Load Constraint on Action States

Action states are saved as data; their `OnEnter` behaviour is recovered from the runtime type at load time. State *types* are the unit of stable identity for action behaviour.

After load, the dispatch procedure is reinstalled per body. Its first run observes the loaded state and invokes `OnEnter`, which re-establishes any scheduling that was associated with that state.

## Action Dispatch Procedure

For each body, a static change-triggered procedure observes `Body.ActionState`. On change, it invokes the new state's `OnEnter(scheduler, body)`. The procedure is installed at runtime (e.g. via `Game.SetupRuntime`) and is not serialized.
