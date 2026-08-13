using Godot;

namespace DelaunyFabric.Core;

/// <summary>Undirected panel edge (quad topology connection).</summary>
[GlobalClass]
public partial class GarmentEdge : Godot.Resource
{
	[Export] public int A { get; set; }
	[Export] public int B { get; set; }
}
