using System;
using System.Collections.Generic;
using Godot;

/// <summary>Item-counts selector with provenance guarantee; reconciles rows by <see cref="Item"/>.</summary>
public partial class ContainerView : VBoxContainer, ISelectionInput
{
	private readonly ExecutionContext _ui;
	private readonly Key<Dictionary<Item, int>> _counts = new();
	private readonly Key<HashSet<Item>> _highlighted = new();
	private readonly Key<int> _filterEpoch = new();
	private readonly Dictionary<Item, Node> _rows = new();
	private Func<object, bool> _candidateFilter;
	private int _epoch;

	public Formula Guarantee { get; }
	public bool IsBoardLens => false;
	public FloatingPanel Panel { get; }
	public Var PromptedHole { get; private set; }

	public Func<object, bool> CandidateFilter
	{
		get => _candidateFilter;
		set
		{
			_candidateFilter = value;
			_ui.Write(_filterEpoch, ++_epoch);
		}
	}

	public event Action<Item> ItemPressed;

	public ContainerView(ExecutionContext ui, FloatingPanel panel, Formula guarantee)
	{
		_ui = ui;
		Panel = panel;
		Guarantee = guarantee;
		Name = "Container";
		MouseFilter = MouseFilterEnum.Ignore;
		panel.Body.AddChild(this);
	}

	public void Prompt(Var hole) => PromptedHole = hole;

	public void ClearPrompt()
	{
		PromptedHole = null;
		CandidateFilter = null;
	}

	public void SetCounts(Dictionary<Item, int> counts) =>
		_ui.Write(_counts, counts);

	public void SetHighlighted(IEnumerable<Item> items) =>
		_ui.Write(_highlighted, items == null ? new HashSet<Item>() : new HashSet<Item>(items));

	public void Render()
	{
		_ = _ui.Read(_filterEpoch);
		var counts = _ui.Read(_counts) ?? new Dictionary<Item, int>();
		var highlighted = _ui.Read(_highlighted);
		var filtering = CandidateFilter != null;

		var keys = new List<Item>();
		foreach (var kv in counts)
		{
			if (kv.Value > 0)
				keys.Add(kv.Key);
		}

		NodeReconcile.Sync(
			this,
			_rows,
			keys,
			item =>
			{
				var captured = item;
				var btn = new Button { Flat = true, Alignment = HorizontalAlignment.Left };
				btn.Pressed += () => ItemPressed?.Invoke(captured);
				return btn;
			},
			(item, node) =>
			{
				var btn = (Button)node;
				var n = counts[item];
				var on = highlighted != null && highlighted.Contains(item);
				var allowed = !filtering || CandidateFilter(item);
				btn.Text = on ? $"> {item.Name} × {n}" : $"{item.Name} × {n}";
				btn.Disabled = filtering && !allowed;
			});
	}
}
