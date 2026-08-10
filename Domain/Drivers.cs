using System;
using System.Collections.Generic;

/// <summary>Drivers participate in the save graph as <see cref="ISaveable"/> nodes (couplings reference them by <see cref="NodeRef"/>).</summary>
public interface IDriver : ISaveable
{
	/// Outside closure: observe a fixed point and (possibly later) push a choice.
	/// <paramref name="submit"/> is bound to this obligation — call <c>submit(command, assignment)</c>; must not write sim state except through that submission (the engine runs issuance under <see cref="Scheduler.RunScoped(System.Action)"/>).
	/// Bindings use the same <see cref="Var"/> instances as <see cref="CommandDefinition.Variables"/> / constraints (see <see cref="CommandDefinition.TryBindVariables(object, out Assignment)"/>).
	/// Returns true iff the command was successfully issued (<see cref="CommandDefinition.TryIssue"/> returned true).
	void OnDecisionNeeded(Body vehicle, Func<CommandDefinition, Assignment, bool> submit);

	/// <summary>
	/// Independent sim observations for this driver. Each is installed as its own <see cref="Scheduler"/> procedure
	/// (own reactive read set) for the coupled vehicle.
	/// </summary>
	IEnumerable<Action<Body>> SimObservations => Array.Empty<Action<Body>>();
}
