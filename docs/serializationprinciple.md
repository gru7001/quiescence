**Save/Load Executable Independence**

We require that save/load be correct without serializing any executable definitions.

In particular, serialized data must not include:

* subprocedures
* event procedures
* any other static code-defined execution logic

Instead, loading restores:

* serialized state
* current simulation time

and all executable logic is recovered from the static system definition already present in the program.

Therefore, any instance-specific information needed for future evolution must be represented in serialized state, while executable logic itself must not be serialized.
