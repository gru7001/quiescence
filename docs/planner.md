## Planner

### Static Ingredients

Let:

* (\Sigma) be the set of planning states.

* For each state (\sigma \in \Sigma), let:
  [
  \operatorname{Choices}(\sigma)
  ]
  be the finite set of admissible choices at (\sigma).

* Let:
  [
  \operatorname{Step}(\sigma,c)
  ]
  be the deterministic transition obtained by issuing choice (c \in \operatorname{Choices}(\sigma)) and then resolving the system forward until the next decision point, termination, or horizon boundary.

* Let:
  [
  H \in \mathbb{R}_{>0}
  ]
  be the planning horizon.

* Let:
  [
  Q : \Sigma \to \mathbb{R}
  ]
  be the instantaneous score function.

* Let:
  [
  W : [0,H] \to \mathbb{R}
  ]
  be a cumulative weight function.

---

## Planning State

A planning state is:

[
\sigma = (x,\tau)
]

where:

* (x) is the simulated world/configuration state
* (\tau \in [0,H]) is elapsed planning time from the root

---

## Decision Point

A state (\sigma) is a decision point iff:

[
\operatorname{Choices}(\sigma) \neq \varnothing
]

The choice set is finite.

---

## Transition Semantics

For each admissible choice (c \in \operatorname{Choices}(\sigma)), define:

[
\operatorname{Step}(\sigma,c) = (\sigma', \mathcal{I})
]

where:

* (\sigma') is the successor decision state, terminal state, or horizon state
* (\mathcal{I}) is the finite sequence of score-constant time segments traversed during that step

That is, during the transition induced by (c), the system evolves through a finite family of intervals:

[
\mathcal{I} = {([a_0,a_1],s_0),\dots,([a_{n-1},a_n],s_{n-1})}
]

with:

* (a_0 = \tau)
* (a_n = \tau')
* (s_i) the constant score on ([a_i,a_{i+1}])

---

## Step Value

The value of one interval ([a,b]) with constant score (s) is:

[
s \cdot (W(b)-W(a))
]

So the value of one step is:

[
\operatorname{ValStep}(\sigma,c)
================================

\sum_{([a_i,a_{i+1}],s_i)\in\mathcal I}
s_i \cdot (W(a_{i+1})-W(a_i))
]

---

## Branch

A branch is a finite choice sequence:

[
\beta = (c_0,\dots,c_{m-1})
]

such that repeated stepping yields:

[
\sigma_0 \xrightarrow{c_0} \sigma_1 \xrightarrow{c_1} \cdots \xrightarrow{c_{m-1}} \sigma_m
]

with elapsed time never exceeding (H).

The value of the branch is:

[
\operatorname{Val}(\beta)
=========================

\sum_{j=0}^{m-1} \operatorname{ValStep}(\sigma_j,c_j)
]

---

## Horizon / Terminal Condition

A branch stops when the first of the following holds:

* elapsed time reaches (H)
* the current state is terminal
* the current state has no admissible choices

---

## Planner

The planner explores a finite subset of admissible branches from initial state (\sigma_0), and selects a maximizing branch:

[
\beta^* \in \arg\max_{\beta} \operatorname{Val}(\beta)
]

The planner’s output is typically the first choice of (\beta^*).
