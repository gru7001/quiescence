using System;

/// <summary>
/// Selection element: values obtained through it carry <see cref="Guarantee"/> as provenance.
/// </summary>
public interface ISelectionInput
{
	public static readonly Var Slot = new("$");

	/// <summary>Domain guarantee over <see cref="Slot"/>.</summary>
	Formula Guarantee { get; }

	/// <summary>True when this selector is a board lens (tile / occupant); chrome pass-through applies while it is focused.</summary>
	bool IsBoardLens { get; }

	/// <summary>Panel to raise/open while this selector is the completion focus; null for board lenses.</summary>
	FloatingPanel Panel { get; }

	/// <summary>
	/// Completion candidate predicate written by the seat derive step.
	/// Null = free mode (all candidates). Non-null = only values for which it returns true.
	/// </summary>
	Func<object, bool> CandidateFilter { get; set; }

	Var PromptedHole { get; }

	void Prompt(Var hole);

	void ClearPrompt();
}
