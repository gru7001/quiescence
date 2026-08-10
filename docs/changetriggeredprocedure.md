**Change-Triggered Procedures**

A procedure may detect change in a substate by storing explicit prior observed value in ordinary state.

For a watched substate `(x)`, let `(m_x)` be serialized state.

A change-trigger procedure reads `(x)` and `(m_x)` and behaves as follows:

* if `(x = m_x)`, it is NOP
* otherwise, it performs its ordinary writes and also writes:
  [
  m_x := x
  ]

So an “on `(x)` changed” rule is not primitive. It is a deterministic subprocedure with explicit remembered comparison state. No hidden mutable watcher state is permitted.  
