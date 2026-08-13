using Godot;

namespace DelaunyFabric.Core;

/// <summary>One authored pattern node. World = island UV rest + local Offset.</summary>
[GlobalClass]
public partial class GarmentNode : Godot.Resource
{
	[Export] public Vector2 Uv { get; set; }
	[Export] public int Island { get; set; }
	/// <summary>Island-local translation from the UV rest point. Zero sits on the panel.</summary>
	[Export] public Vector3 Offset { get; set; }
}
