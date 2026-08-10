**Timed Events**

* Let (t) be the current simulation time.
* A timed event is a pair ((\tau, e)) where:

  * (\tau) is a time
  * (e) is a deterministic event procedure

An event ((\tau, e)) is due iff:
[
\tau = t
]

When a due event executes, it produces writes to state.

Those writes are then handled by the ordinary scheduling rule. In particular, if a substate (x) is written by a due event, then all subprocedures whose last observed read set contains (x) are marked awake.

---

**Temporal Composition**

The fixed-point system above is evaluated at fixed (t).

A larger execution process may be defined by composition:

1. hold (t) fixed
2. exhaust ordinary subprocedures to a fixed point
3. advance (t) to the earliest pending event time
4. execute all events due at that time
5. exhaust again to a fixed point