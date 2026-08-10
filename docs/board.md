## Boarded World (Compressed)

### Static Structure

Let:

* (B) be a finite set of boards.
* Each board (b \in B) has a finite set of tiles:
  [
  T_b
  ]

A **world position** is:
[
p = (b, t)
\quad\text{with}\quad b \in B,; t \in T_b
]

---

## Tiles

Each tile (p) carries tile-local state:
[
\theta(p)
]

Tiles represent places that may be occupied.

---

## Edges

Adjacency and blocking are defined by edges.

For each board (b), let:
[
E_b \subseteq {{u,v} \mid u,v \in T_b}
]

Each edge carries state:
[
\varepsilon(b,{u,v})
]

Edges determine movement, visibility, and interaction between tiles.

---

## Connectors

Boards are composed by connectors:

[
C \subseteq {{(b,u),(b',v)} \mid b,b' \in B}
]

Each connector carries state:
[
\kappa(c)
]

Connectors are inter-board edges.

---

## World Graph

Adjacency is given by:

[
A = \Bigl(\bigcup_{b \in B} E_b\Bigr) \cup C
]

So the world is a graph of tiles.

---

## Semantics

* tiles = positions
* edges/connectors = passage (movement, visibility, projectiles)
* all change happens by mutating tile/edge/connector state

Boards are stable regions; composition is explicit via connectors.
