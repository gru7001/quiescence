using Godot;

namespace DelaunyFabric.Core;

/// <summary>3D pose of one UV island. Position is the UV centroid in world; +Z is outward.</summary>
[GlobalClass]
public partial class GarmentIsland : Godot.Resource
{
	[Export] public Vector3 Position { get; set; } = new(0f, 1.1f, 0.05f);
	[Export] public Quaternion Rotation { get; set; } = Quaternion.Identity;
	/// <summary>UV that sits at <see cref="Position"/>. Recentered to the node UV mean so the handle stays at the panel centroid.</summary>
	[Export] public Vector2 UvOrigin { get; set; } = new(0.5f, 0.5f);

	public static GarmentIsland Default() => new();

	public Transform3D Transform => new(new Basis(Rotation), Position);

	public Vector3 Outward => new Basis(Rotation).Z;

	public void SetTransform(Transform3D t)
	{
		Position = t.Origin;
		Rotation = t.Basis.Orthonormalized().GetRotationQuaternion();
	}

	public Vector3 ToWorld(Vector2 uv, float scale) =>
		ToWorld(uv, UvOrigin, scale);

	public Vector2 FromWorld(Vector3 world, float scale) =>
		FromWorld(world, UvOrigin, scale);

	public Vector3 ToWorld(Vector2 uv, Vector2 origin, float scale)
	{
		var d = (uv - origin) * scale;
		return Position + new Basis(Rotation) * new Vector3(d.X, d.Y, 0f);
	}

	public Vector2 FromWorld(Vector3 world, Vector2 origin, float scale)
	{
		var local = new Basis(Rotation).Inverse() * (world - Position);
		if (scale < 1e-8f)
			return origin;
		return origin + new Vector2(local.X, local.Y) / scale;
	}

	public GarmentIsland DuplicatePose() => new()
	{
		Position = Position,
		Rotation = Rotation,
		UvOrigin = UvOrigin,
	};
}
