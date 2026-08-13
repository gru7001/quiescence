using Godot;

/// <summary>One baked key: time + per-bone local poses.</summary>
[GlobalClass]
public partial class IkTrackKey : Godot.Resource
{
	[Export]
	public float Time { get; set; }

	[Export]
	public Godot.Collections.Array<Transform3D> Pose { get; set; } = [];
}
