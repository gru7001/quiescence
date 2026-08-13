using Godot;

/// <summary>Ambient sim-clock readout (ticks).</summary>
public partial class ClockView : Label
{
	private readonly ExecutionContext _ui;
	private readonly Key<long> _now = new();

	public ClockView(ExecutionContext ui, Node parent)
	{
		_ui = ui;
		Name = "Clock";
		MouseFilter = MouseFilterEnum.Ignore;
		HorizontalAlignment = HorizontalAlignment.Right;
		SetAnchorsPreset(LayoutPreset.TopRight);
		OffsetLeft = -160;
		OffsetTop = 8;
		OffsetRight = -12;
		OffsetBottom = 28;
		parent.AddChild(this);
	}

	public void SetNow(long now) => _ui.Write(_now, now);

	public void Render()
	{
		Text = $"t={_ui.Read(_now)}";
	}
}
