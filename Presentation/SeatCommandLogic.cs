using System.Collections.Generic;

/// <summary>Positional selection → partial assignment / three-valued command eval (with provenance).</summary>
public static class SeatCommandLogic
{
	public static readonly Var Slot = ISelectionInput.Slot;

	/// <summary>
	/// Command domain for <paramref name="variable"/> is at least as restrictive as
	/// <paramref name="guaranteeOverSlot"/> (formula over <see cref="Slot"/>).
	/// </summary>
	public static bool ProvenanceOk(CommandDefinition cmd, Var variable, Formula guaranteeOverSlot) =>
		Derivation.Derives(cmd.Constraint, guaranteeOverSlot.Substitute(Slot, variable));

	public static PartialTruth Evaluate(
		CommandDefinition cmd,
		Body body,
		IReadOnlyList<SelectionEntry> selection)
	{
		if (selection.Count > cmd.Variables.Count)
			return PartialTruth.False;

		var a = new Assignment();
		for (var i = 0; i < selection.Count; i++)
		{
			var v = cmd.Variables[i];
			var e = selection[i];
			if (!ProvenanceOk(cmd, v, e.Guarantee))
				return PartialTruth.False;
			var next = v.BindOrCheck(a, e.Value);
			if (next == null)
				return PartialTruth.False;
			a = next;
		}

		return cmd.Constraint.Evaluate(body, a);
	}

	public static Assignment ToAssignment(CommandDefinition cmd, IReadOnlyList<SelectionEntry> selection)
	{
		var a = new Assignment();
		var n = selection.Count < cmd.Variables.Count ? selection.Count : cmd.Variables.Count;
		for (var i = 0; i < n; i++)
		{
			var next = cmd.Variables[i].BindOrCheck(a, selection[i].Value);
			if (next == null)
				return a;
			a = next;
		}
		return a;
	}

	public static Var NextHole(CommandDefinition cmd, IReadOnlyList<SelectionEntry> selection) =>
		selection.Count >= cmd.Variables.Count ? null : cmd.Variables[selection.Count];
}
