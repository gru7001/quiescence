using Godot;

/// <summary>One SE(3) target row for a serializable IK solver.</summary>
[GlobalClass]
public partial class IkTargetEntry : Godot.Resource
{
	[Export]
	public string Bone { get; set; } = "";

	[Export]
	public Transform3D Transform { get; set; } = Transform3D.Identity;
}
