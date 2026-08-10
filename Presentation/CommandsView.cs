using System;
using System.Collections.Generic;
using Godot;

/// <summary>Command list by three-valued eval; reconciles rows by command identity.</summary>
public partial class CommandsView : VBoxContainer
{
	public readonly record struct Row(CommandDefinition Command, PartialTruth Truth);

	private readonly ExecutionContext _ui;
	private readonly Key<Row[]> _rows = new();
	private readonly Key<CommandDefinition> _chosen = new();
	private readonly Dictionary<CommandDefinition, Node> _nodes = new();

	public event Action<CommandDefinition> CommandPressed;

	public CommandsView(ExecutionContext ui, FloatingPanel panel)
	{
		_ui = ui;
		Name = "Commands";
		MouseFilter = MouseFilterEnum.Ignore;
		panel.Body.AddChild(this);
	}

	public void SetRows(Row[] rows) =>
		_ui.Write(_rows, rows ?? Array.Empty<Row>());

	public void SetChosen(CommandDefinition command) =>
		_ui.Write(_chosen, command);

	public void Render()
	{
		var rows = _ui.Read(_rows) ?? Array.Empty<Row>();
		var chosen = _ui.Read(_chosen);
		var byCmd = new Dictionary<CommandDefinition, Row>();
		var keys = new List<CommandDefinition>();
		foreach (var row in rows)
		{
			if (row.Command == null || row.Truth == PartialTruth.False)
				continue;
			byCmd[row.Command] = row;
			keys.Add(row.Command);
		}

		NodeReconcile.Sync(
			this,
			_nodes,
			keys,
			cmd =>
			{
				var captured = cmd;
				var btn = new Button { Flat = true, Alignment = HorizontalAlignment.Left };
				btn.Pressed += () => CommandPressed?.Invoke(captured);
				return btn;
			},
			(cmd, node) =>
			{
				var row = byCmd[cmd];
				var btn = (Button)node;
				var mark = row.Truth == PartialTruth.True ? "✓" : "?";
				var isChosen = ReferenceEquals(chosen, cmd);
				btn.Text = isChosen ? $"> {cmd.Name} {mark}" : $"{cmd.Name} {mark}";
				btn.Modulate = row.Truth == PartialTruth.Unknown
					? new Color(0.65f, 0.65f, 0.65f)
					: Colors.White;
			});
	}
}
