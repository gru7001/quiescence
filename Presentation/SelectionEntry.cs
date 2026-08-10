using System.Collections.Generic;

/// <summary>Ordered selection of provenance-carrying values.</summary>
public readonly record struct SelectionEntry(object Value, Formula Guarantee);

/// <summary>Explorer-style replace / append over an ordered selection list.</summary>
public static class SeatSelection
{
	public static List<SelectionEntry> Replace(object value, Formula guarantee) =>
		new() { new SelectionEntry(value, guarantee) };

	public static List<SelectionEntry> Append(IReadOnlyList<SelectionEntry> current, object value, Formula guarantee)
	{
		var next = current == null || current.Count == 0
			? new List<SelectionEntry>()
			: new List<SelectionEntry>(current);
		next.Add(new SelectionEntry(value, guarantee));
		return next;
	}
}
