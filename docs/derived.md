If we allow each substate (x) to keep a version, then cached derivations can be validated by comparing the current versions of their dependencies against the versions observed when they were last computed.

---

## Versioned State

For each substate (x), let:

[
\nu(x) \in \mathbb{N}
]

be its current version.

Whenever (x) is written and its value changes, its version is incremented.

So a state is now understood to include both:

* ordinary values
* per-substate versions

The versions are auxiliary metadata, not additional semantic world content.

---

## Derived Procedure

A **derived procedure** is a deterministic procedure

[
d : S \to O
]

whose purpose is to construct some derived object (O) from state (S).

Examples include:

* sensory state
* visible-object sets
* local tactical summaries
* derived affordance structures
* cached score/evaluation objects

---

## Derived Cache Record

For one cached evaluation of (d), store:

* the cached output:
  [
  o_d
  ]
* the observed read set:
  [
  R_d \subseteq \text{Substates}
  ]
* the observed version snapshot:
  [
  \lambda_d : R_d \to \mathbb{N}
  ]
  where
  [
  \lambda_d(x) = \nu(x)
  ]
  at the time the cached output was computed

So (\lambda_d) records the versions of all substates read during the evaluation.

---

## In-Date Predicate

The cached result of (d) is **in-date** iff:

[
\forall x \in R_d:; \nu(x) = \lambda_d(x)
]

That is, every substate previously read by (d) still has the same version it had during the last evaluation.

---

## Evaluation Rule

When (d) is queried:

1. If a cache record exists and is in-date, return the cached output:
   [
   o_d
   ]

2. Otherwise, re-evaluate (d) on the current state.

   During this evaluation:

   * record the new observed read set (R_d)
   * record the new version snapshot (\lambda_d)
   * store the new output (o_d)

Then return the new cached output.

---

## Re-Evaluation Semantics

If (d) is evaluated on state (S), producing output (o), and during that evaluation observes read set (R), then the cache record becomes:

[
o_d := o
]
[
R_d := R
]
[
\lambda_d(x) := \nu(x) \quad \forall x \in R
]

So the cache is always tied to the exact dependency set and dependency versions observed during the last successful evaluation.

---

## Final Compression

If each substate carries a version, then a derived object can be cached by storing both its observed read set and the versions of those reads at evaluation time. The cached object remains valid exactly while those versions remain unchanged, and otherwise must be recomputed.
