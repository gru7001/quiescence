using Godot;

/// <summary>One animation key: time + target set.</summary>
[GlobalClass]
public partial class IkAnimKey : Godot.Resource
{
	[Export]
	public float Time { get; set; }

	[Export]
	public IkTargetSet TargetSet { get; set; } = new();
}
