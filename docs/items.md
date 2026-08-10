# Items

### Static

Let:

* ( U ): carriers
* ( I ): item kinds

Each item kind is stateless.

---

## Structure on a Carrier

For a carrier ( u ), when item structure is defined:

[
N_u : I \to \mathbb{N}
]

Interpretation:

* ( N_u(i) ) is the count of items of kind ( i ) contained in ( u )

---

## Uniqueness (local)

Counts are non-negative integers:

[
\forall i \in I: ; N_u(i) \ge 0
]

---

## Transfer

A transfer of one unit of ( i ) from ( u ) to ( u' ) is admissible iff:

[
N_u(i) > 0
]

and results in:

[
N_u(i) := N_u(i) - 1
]
[
N_{u'}(i) := N_{u'}(i) + 1
]

---

# Quantitative Contribution

Each item kind induces fixed contributions:

[
w : I \to \mathbb{R}*{\ge 0}
\quad\text{(weight)}
]
[
v : I \to \mathbb{R}*{\ge 0}
\quad\text{(volume)}
]

For a carrier ( u ):

[
\operatorname{Weight}(u)
========================

\sum_{i \in I} N_u(i), w(i)
]

[
\operatorname{Volume}(u)
========================

\sum_{i \in I} N_u(i), v(i)
]

(Equivalently: items contribute to quantity axes.)

---

# Form Contribution

Each item kind may contribute forms:

[
F_i \subseteq F
]

For a vehicle ( v ):

[
\operatorname{Forms}(v, S)
==========================

\operatorname{BaseForms}(v, S)
;\cup;
\bigcup_{i \in I:, N_v(i) > 0} F_i
]

---

# Interaction with Equipment

Items and equippables are orthogonal.

However, forms may bridge them:

* an item form may realize an equippable
* an equippable form may release that realization

No structural identification is required.

---

# Compression

An item system is a multiset of stateless item kinds per carrier.
Each item kind contributes fixed quantities (e.g. weight, volume) and may contribute forms.
