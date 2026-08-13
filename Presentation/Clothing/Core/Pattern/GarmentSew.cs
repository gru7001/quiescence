using Godot;

namespace DelaunyFabric.Core;

/// <summary>Sew / weld between nodes (shared 3D vertex).</summary>
[GlobalClass]
public partial class GarmentSew : Godot.Resource
{
	[Export] public int A { get; set; }
	[Export] public int B { get; set; }
}
