# Quantities

### Static

Let:

* (U) be a carrier
* (\mathcal{A}) be a set of named quantities

---

## Structure on a Carrier

For (u \in U), when quantitative structure is defined:

[
Q_u : \mathcal{A} \to \mathbb{R}
]

---

## Realization

Each value is obtained by combining contributions:

[
Q_u(a) = B_u(a) + \sum_{m \in \mathcal{M}(u)} \Delta_m(a)
]

where:

* (B_u) is a baseline
* (\mathcal{M}(u)) is a finite set of contributors
* each (\Delta_m) is additive

---

## Interpretation

Effects are obtained by evaluation functions:

[
\Psi(Q_u, \ldots)
]

No restriction is imposed on (\Psi).

---

## Compression

Quantities on a carrier form a real-valued map assembled additively from contributions.

---

# Stores

### Static

Let:

* (U) be a carrier
* (\mathcal{R}) be a set of named stores

---

## Structure on a Carrier

For (u \in U), when store structure is defined:

Each (r \in \mathcal{R}) has:

[
C_u(r) \in \mathbb{R}
\quad\text{and}\quad
K_u(r) \in \mathbb{R}_{\ge 0}
]

---

## Invariant

[
0 \le C_u(r) \le K_u(r)
]

---

## Bounds

Each bound is given by a definition:

[
K_u(r) = \Phi_r(Q_u)
]

where (\Phi_r) may reference quantities when defined.

---

## Compression

A store on a carrier is a bounded scalar with a derived upper limit.

---

# Relation

* quantities aggregate additively
* stores are constrained by bounds
* bounds may be expressed in terms of quantities
* interpretation of both is determined by evaluation functions
