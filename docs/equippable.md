# Equippable Structure

### Static

* (V): vehicles
* (Q): equippables
* (S): slot names

Each equippable:
[
\operatorname{Occ}(q) \subseteq S
]

---

## Structure on a Vehicle

For a vehicle (v), when equipment structure is defined:

[
\operatorname{Slots}(v) \subseteq S
]

[
E(v) \subseteq Q
]

---

## Validity

[
\forall q \in E(v):; \operatorname{Occ}(q) \subseteq \operatorname{Slots}(v)
]

[
\forall q_1 \neq q_2 \in E(v):;
\operatorname{Occ}(q_1)\cap \operatorname{Occ}(q_2)=\varnothing
]

---

## Derived

[
\operatorname{Occupied}(v)
==========================

\bigcup_{q \in E(v)} \operatorname{Occ}(q)
]

[
\operatorname{Free}(v)
======================

\operatorname{Slots}(v)\setminus \operatorname{Occupied}(v)
]

---

## Admissibility

[
\operatorname{CanEquip}(v,q)
\iff
\operatorname{Occ}(q)\subseteq \operatorname{Free}(v)
]

---

## Contribution

[
\sum_{q \in E(v)} M(q)
]

---

# Compression

Equipment on (v) is a slot set and a set of equippables whose occupied slot sets are disjoint and contained within the slots.
