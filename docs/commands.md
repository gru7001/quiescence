# Commands (Progressive Filling Model)

This replaces the prior “single-shot form + opaque filling” view with an explicit **command schema**
whose arguments can be filled progressively (supporting both command-first and argument-first UI).

## Static

Let:

* ( V ) = set of vehicles
* ( C ) = set of **command schemas**

For each ( c \in C ), let:

* ( A_c = (a_1,\dots,a_n) ) be the ordered list of **argument slots**
* each slot ( a_i ) has:

  * a type ( T_{a_i} )
  * a domain ( \Theta_{a_i} )

Define the **filling space**:

[
\Theta_c = \Theta_{a_1} \times \cdots \times \Theta_{a_n}
]

---

## Partial Filling

A **partial filling** of ( c ) is:

[
\phi : {1,\dots,n} \rightharpoonup \bigcup_i \Theta_{a_i}
]

such that:

[
\phi(i) \in \Theta_{a_i} \quad \text{whenever defined}
]

A **complete filling** is:

[
\theta \in \Theta_c
]

---

## Capability (coarse)

Each command has a predicate:

[
\operatorname{Capable}_c : V \times S \to {\text{true}, \text{false}}
]

Interpretation:

* ( \operatorname{Capable}_c(v,S) ) means vehicle ( v ) may issue command ( c ) in state ( S )

This encodes **coarse command availability**.

---

## Admissibility (fine)

Each command has a predicate:

[
\operatorname{Accept}_c : V \times \Theta_c \times S \to {\text{true}, \text{false}}
]

Interpretation:

* ( \operatorname{Accept}_c(v,\theta,S) ) means the fully filled command is valid in state ( S ) for issuer ( v )

This encodes **fine admissibility**.

---

## Command Exposure

For vehicle ( v ), define:

[
\operatorname{Commands}(v,S)
============================

{c \in C \mid
\operatorname{Capable}_c(v,S)
\land
\exists \theta \in \Theta_c:
\operatorname{Accept}_c(v,\theta,S)
}
]

Interpretation:

* commands that are both:

  * applicable to the vehicle, and
  * have at least one admissible filling

---

## Partial Admissibility (Extendability)

Define:

[
\operatorname{Extendable}_c(v,\phi,S)
\iff
\operatorname{Capable}_c(v,S)
\land
\exists \theta \in \Theta_c:
\theta \supseteq \phi
\land
\operatorname{Accept}_c(v,\theta,S)
]

Interpretation:

* a partial filling is **extendable** iff it can be completed into a valid command

---

## Argument-Centric Query

For a value ( x ), define:

[
\operatorname{CommandsAccepting}(v,x,S)
=======================================

{c \in \operatorname{Commands}(v,S)
\mid
\exists \phi:
\phi \text{ assigns } x \text{ to some slot}
\land
\operatorname{Extendable}_c(v,\phi,S)
}
]

Interpretation:

* commands that can be (partially) filled using ( x )

---

## Next-Step Filling

Given a partial filling ( \phi ), define:

[
\operatorname{Next}(c,v,\phi,S)
===============================

{(i,x)\mid
i \notin \operatorname{dom}(\phi),
x \in \Theta_{a_i},
\operatorname{Extendable}_c(v,\phi \cup {i \mapsto x}, S)
}
]

Interpretation:

* valid next argument choices that preserve extendability

---

## Issuance

A **command issuance** is:

[
(c,\theta)
\quad \text{with } \theta \in \Theta_c
]

Acceptance condition:

[
c \in \operatorname{Commands}(v,S)
\land
\operatorname{Accept}_c(v,\theta,S)
]

If accepted, it produces writes to state, typically:

* setting action identity
* initializing action state

This aligns with the action installation model.

---

## Interpretation

### Commands vs Actions

* **command**: what the driver issues
* **action**: what is installed on the actor

[
\text{issue}(c,\theta)
\Rightarrow
\text{install action}
]

### Objects as Arguments

Objects (items, tiles, actors, etc.) are not command owners.

They are elements of argument domains ( \Theta_{a_i} ), and serve as:

* candidate fillings
* entry points for partial filling

### Interaction Modes

Two canonical modes:

1. **Command-first**

   * choose ( c \in \operatorname{Commands}(v,S) )
   * fill arguments via ( \operatorname{Next} )

2. **Argument-first**

   * choose value ( x )
   * query ( \operatorname{CommandsAccepting}(v,x,S) )

Both are equivalent views over the same structure.

---

# Compression

A **command** is a schema with typed argument slots and two predicates:

* **Capable** determines whether the command applies to a vehicle
* **Accept** determines whether a fully filled command is valid

Commands are exposed exactly when they are both:

* applicable to the vehicle, and
* completable in the current state

Interaction is modeled as progressive filling:

* partial fillings restrict the space of valid completions
* a command is issuable exactly when fully filled and admissible

Objects do not own commands; they participate as argument values.

