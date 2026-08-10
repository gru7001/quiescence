using System.Collections.Generic;
using Godot;

/// <summary>Readout of the current ordered selection (value + provenance label).</summary>
public partial class SelectionView : VBoxContainer
{
	private readonly ExecutionContext _ui;
	private readonly Key<List<SelectionEntry>> _entries = new();
	private readonly Dictionary<int, Node> _rows = new();

	public SelectionView(ExecutionContext ui, Node parent)
	{
		_ui = ui;
		Name = "Selection";
		MouseFilter = MouseFilterEnum.Ignore;
		parent.AddChild(this);
	}

	public void SetEntries(List<SelectionEntry> entries) =>
		_ui.Write(_entries, entries ?? new List<SelectionEntry>());

	public void Render()
	{
		var entries = _ui.Read(_entries) ?? new List<SelectionEntry>();
		var keys = new List<int>(entries.Count);
		for (var i = 0; i < entries.Count; i++)
			keys.Add(i);

		NodeReconcile.Sync(
			this,
			_rows,
			keys,
			_ => new Label { MouseFilter = MouseFilterEnum.Ignore },
			(i, node) =>
			{
				var e = entries[i];
				((Label)node).Text = $"{i}: {Format(e.Value)}";
			});
	}

	private static string Format(object value) => value switch
	{
		Item it => it.Name,
		Direction d => d.ToString(),
		Tile t => t.ToString(),
		IOccupant o => o.ToString(),
		long n => n.ToString(),
		_ => value?.ToString() ?? "(null)"
	};
}
