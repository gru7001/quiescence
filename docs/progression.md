## Skill Tree

**Static Structure**

* Let (V) be the finite set of skills.
* Let
  [
  G = (V,E,w)
  ]
  be a directed acyclic graph, where:

  * (E \subseteq V \times V) is the dependency relation
  * (w : E \to \mathbb{N}_{>0}) assigns each edge a positive integer weight
* An edge
  [
  a \to b
  ]
  means that (a) is constrained by (b).

---

**State**

* The skill state is
  [
  \sigma = {(\ell_s,p_s)}_{s \in V}
  ]
  where for each skill (s):

  * (\ell_s \in \mathbb{N}) is its level
  * (p_s \in [0,1)) is its progress toward the next level

---

**Admissibility Predicate**

For each skill (a), define:

[
\operatorname{Open}(a,\sigma)
\iff
\forall (a,b)\in E:;
\ell_a < \frac{\ell_b}{w(a,b)}
]

Interpretation:

* (a) is **open** iff it may still accept local progress
* otherwise (a) is **saturated**

This is a derived predicate, not stored state.

---

**Canonical Invariant**

* A saturated skill carries no local partial progress:
  [
  \neg \operatorname{Open}(a,\sigma)
  ;\Rightarrow;
  p_a = 0
  ]

This makes “reset on unlock” a consequence rather than a primitive rule.

---

**Routing Policy**

* Let
  [
  R(a,\sigma,\delta)
  ]
  be a deterministic forwarding policy for a training attempt of size (\delta>0) made on a saturated skill (a).

* (R) redistributes that attempted progress to one or more immediate dependents of (a).

The choice of routing policy is separate from the core structure.

---

**Normalization**

Define the normalization operator (N) on a single skill state by:

[
N(\ell,p)
=========

\left(\ell + \lfloor p \rfloor,; p - \lfloor p \rfloor\right)
]

So any full unit of progress is carried into levels, and progress remains in ([0,1)).

---

**Procedure Family**

For each skill (a \in V) and training amount (\delta>0), define a deterministic training attempt:

[
T_{a,\delta}(\sigma)
]

by:

1. If (\operatorname{Open}(a,\sigma)), then
   [
   (\ell_a,p_a) \leftarrow N(\ell_a,; p_a+\delta)
   ]
   and all other skills are unchanged.

2. If (\neg \operatorname{Open}(a,\sigma)), then
   [
   \sigma' = R(a,\sigma,\delta)
   ]
   subject to the invariant above.

---

**Minimal Semantics**

A skill tree is therefore:

* a weighted DAG (G=(V,E,w))
* a per-skill state ((\ell_s,p_s))
* a derived admissibility predicate (\operatorname{Open})
* a forwarding rule (R) for saturated training attempts

Everything else is consequence.

---

**Immediate Consequence**

If (a) is saturated and training is attempted on (a), then (a) does not accumulate local progress.
If some dependent later levels and (a) becomes open again, (a) resumes from (p_a=0) automatically by the invariant.

---

**Example**

If
[
A \xrightarrow{2} B
]
then
[
\operatorname{Open}(A,\sigma)
\iff
\ell_A < \frac{\ell_B}{2}
]

So if (\ell_A=2) and (\ell_B=4), (A) is saturated.
A training attempt on (A) is forwarded by (R), and (p_A=0).

---

## Perk Tree

**Static Structure**

* Let (P) be the finite set of perks.
* Each perk (x \in P) is defined by:
  [
  x = (g_x,\tau_x)
  ]
  where:

  * (g_x) is an availability predicate
  * (\tau_x) is the acquisition transition

---

**State**

Let the perk-relevant character state be

[
\Pi = (\Omega,\rho,\sigma,\eta)
]

where:

* (\Omega \subseteq P) is the set of owned perks
* (\rho) is the spendable resource state
* (\sigma) is the skill state
* (\eta) denotes any other auxiliary state consulted by perk conditions

Only the presence of these substates matters canonically; their internal details are not part of the perk structure itself.

---

**Availability Predicate**

For each perk (x), define:

[
g_x : \Pi \to {\text{true},\text{false}}
]

Interpretation:

* (g_x(\Pi)) is the single semantic question:
  [
  \operatorname{canPick}(x,\Pi)
  ]

So a perk is available exactly when its guard holds.

---

**Acquisition Transition**

For each perk (x), define a partial state transition:

[
\tau_x : \Pi \rightharpoonup \Pi
]

with domain:

[
\operatorname{dom}(\tau_x)
==========================

{\Pi \mid g_x(\Pi)=\text{true}}
]

Interpretation:

* (\tau_x) is the state change produced by picking the perk
* it is defined only when the perk is available

---

**Canonical Semantics**

A perk system therefore consists of guarded acquisitions:

[
x \text{ is pickable in state } \Pi
\iff
g_x(\Pi)
]

[
\text{picking } x \text{ in state } \Pi
=======================================

\tau_x(\Pi)
]

This is the primitive formulation.

---

**Internal Clause Structure**

Although (g_x) is semantically a single predicate, it may be factored into typed clauses, e.g.:

* ownership clauses
* skill-state clauses
* affordability clauses
* auxiliary clauses

This typing is representational, not semantic.
It exists so that authoring and UI can preserve distinctions such as:

* **Cost**
* **Requires**
* **Other conditions**

without changing the canonical meaning.

---

**Induced Perk Graph**

The displayed perk tree is not primitive.

It is induced from positive ownership references inside guards:

[
u \prec x
\quad\text{iff}\quad
g_x \text{ contains a positive requirement on owning } u
]

If this relation is acyclic, the perks admit the usual tree/DAG presentation.

So the perk graph is a view of guard structure, not the underlying mechanic.

---

**Minimal Semantics**

A perk tree is therefore:

* a finite set of perks (P)
* for each perk (x), an availability predicate (g_x)
* for each perk (x), an acquisition transition (\tau_x)

Everything else is presentation or factoring.

---

**Example**

A perk (x) may have guard

[
g_x(\Pi)
========

(x \notin \Omega)
\land
(\ell_{\text{Sword}} \ge 4)
\land
(\text{free skill points in } \rho \ge 2)
\land
(u \in \Omega)
]

Then:

* semantically, this is just one predicate
* representationally, it may be displayed as:

  * Cost: 2 skill points
  * Requires: Sword 4
  * Requires: Perk (u)

---

## Final Compression

### Skill Tree

A skill tree is a weighted DAG plus per-node ((\ell,p)) state, where local training is permitted exactly on open nodes and otherwise forwarded according to a deterministic routing policy.

### Perk Tree

A perk tree is a finite family of guarded acquisition transitions over character state; the visible perk graph is induced from the structure of those guards.
