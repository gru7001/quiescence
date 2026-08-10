## Narrative Weight and Thawing (Broad Model)

### Narrative Weight

Each driver has a scalar:

[
\omega \in \mathbb{R}_{\ge 0}
]

interpreted as **narrative weight**.

Narrative weight determines how much cognitive resolution is allocated to the driver:

* low (\omega): cheap, routine-driven behavior
* high (\omega): reflective, context-aware behavior

This affects both:

* how the driver reasons
* how other drivers model it

---

### Frozen Driver (Low Weight)

A low-weight driver operates in a **frozen mode**:

* behavior is driven by **schedule**
* deviations are handled by **cheap pressure responses**
* no explicit internal deliberation is performed

The driver still produces:

* observation stream
* action execution via forms

but does not construct rich internal context.

---

### Schedule

A schedule is a cyclic mapping:

[
\text{Schedule} : t \mapsto r
]

where (r) is a **semantic activity** (e.g. patrol, work, eat).

The schedule defines:

* baseline behavior
* semantic self-context (“what I am doing”)

It is:

* stable
* only changed by discrete events

---

### Thawing

**Thawing** is a transition from frozen to reflective mode.

It occurs when:

* narrative weight increases
* a higher-weight agent observes/interacts
* significant disruption occurs
* explicit interaction is required

---

### Thawed Driver (LLM Mode)

A thawed driver becomes an **LLM-guided agent**.

It operates on constructed context:

#### Inputs

1. **Recent observation stream**
   Short factual atoms describing recent events

2. **Retrieved memory**
   Episodic summaries selected by relevance

3. **Current world snapshot**
   What exists and matters now

4. **Affordance interface**
   Structured, parameterizable actions (not raw forms)

5. **Schedule context**
   Current scheduled activity as baseline intent

---

### Goal Formation

Upon thawing, the driver establishes an explicit goal:

* derived from schedule if undisturbed
* derived from disruption if urgent
* preserved from prior reflective state if available

So schedule becomes:

> a default goal seed, not a constraint

---

### Behavior in Thawed Mode

The LLM:

* interprets current situation
* selects actions via the affordance interface
* may generate dialogue or explanation
* may update internal goal/state

It replaces:

* mechanical schedule following
  with
* context-sensitive decision-making

---

### Refreezing

When narrative weight drops or interaction ends:

* driver returns to frozen mode
* schedule resumes as baseline
* observation stream continues

No full reset is required; continuity is preserved through memory and schedule.

---

## Minimal Semantics

A driver operates in one of two modes:

* **frozen**: schedule + pressure
* **thawed**: LLM-guided reasoning over context

Narrative weight determines mode and modeling fidelity.

Schedule provides baseline behavior and semantic identity.

Thawing injects memory, context, and affordances, enabling explicit goal-directed reasoning.

---

## Final Compression

Narrative weight controls cognitive resolution. Low-weight drivers follow schedules with simple reactions. When thawed, a driver becomes an LLM-guided agent, using recent observations, memory, and current affordances to form goals and choose actions. The schedule persists as a semantic baseline, seeding intent but no longer directly controlling behavior.
