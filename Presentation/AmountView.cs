using System;
using Godot;

/// <summary>Long/amount selector: static spin + select (no row reconcile).</summary>
public partial class AmountView : VBoxContainer, ISelectionInput
{
	private readonly ExecutionContext _ui;
	private readonly Label _status = new();
	private readonly SpinBox _spin = new()
	{
		MinValue = 0,
		MaxValue = long.MaxValue,
		Step = 1,
		Value = 1
	};
	private readonly Key<long?> _highlighted = new();
	private readonly Key<int> _filterEpoch = new();
	private Func<object, bool> _candidateFilter;
	private int _epoch;
	private Button _pick;

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

	public event Action<long> AmountPressed;

	public AmountView(ExecutionContext ui, FloatingPanel panel)
	{
		_ui = ui;
		Panel = panel;
		Guarantee = ParameterPredicates.Long[ISelectionInput.Slot];
		Name = "Amount";
		MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_status);
		AddChild(_spin);
		_pick = new Button { Text = "Select amount", Flat = true };
		_pick.Pressed += () =>
		{
			var v = (long)System.Math.Round(_spin.Value);
			if (CandidateFilter != null && !CandidateFilter(v))
				return;
			AmountPressed?.Invoke(v);
		};
		AddChild(_pick);
		panel.Body.AddChild(this);
	}

	public void Prompt(Var hole) => PromptedHole = hole;

	public void ClearPrompt()
	{
		PromptedHole = null;
		CandidateFilter = null;
	}

	public void SetHighlighted(long? amount) =>
		_ui.Write(_highlighted, amount);

	public void Render()
	{
		_ = _ui.Read(_filterEpoch);
		var highlighted = _ui.Read(_highlighted);
		var v = (long)System.Math.Round(_spin.Value);
		var allowed = CandidateFilter == null || CandidateFilter(v);
		_spin.Editable = allowed;
		_pick.Disabled = !allowed;
		_status.Text = highlighted == null ? "" : $"> {highlighted.Value}";
	}
}
