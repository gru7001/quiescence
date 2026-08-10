**State and Procedure**

* Let ( S ) be the global state, composed of substates.
* Let ( P = {p_1, \dots, p_k} ) be deterministic subprocedures.
* Each ( p_i ) induces a partial state update ( S' = p_i(S) ).

---

**Read / write trace**

Each run of ( p_i ) on ( S ) observes a **read set** ( R_i(S) ) and a **write set** ( W_i(S) ). These are state-dependent.

---

**Last reads (dependency)**

After each run, store ( \hat R_i \leftarrow R_i ) — the substates that run **depended** on.

---

**Propagation principle**

Whenever substates are written, **every** procedure that **read** one of those substates on its last run is **not settled** with respect to that write: it **must run again** before the system can be taken as closed under these procedures.

Formally: a write set ( W ) **obligates** another run of every ( p_j ) with ( \hat R_j \cap W \neq \varnothing ).

A run of ( p_i ) produces writes ( W_i ). Those writes participate in the **same** propagation rule — there is no separate “self” mechanism. **Include ( W_i ) in the write batch** that is matched against last read sets (after ( \hat R_i ) has been updated for that run). If ( p_i ) wrote something it just read, it is obligated again by the same dependency rule as everyone else.

---

**Quiescence**

**Quiescence** is reached when no procedure is still obligated under the propagation principle (equivalently: a sweep runs procedures until a full round produces no writes, or the chosen schedule has no remaining obligated runs).

---

**Stability**

If ( W_i(S) = \varnothing ) and ( S' ) agrees with ( S ) on every ( x \in R_i(S) ), then ( W_i(S') = \varnothing ).

---

**Fixed point**

( S^* ) is a fixed point iff ( \forall p_i:; W_i(S^*) = \varnothing ) — no further writes from procedure runs, and ( P(S^*) = S^* ).
