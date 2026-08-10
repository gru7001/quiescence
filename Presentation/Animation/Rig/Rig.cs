using Godot;
using System.Collections.Generic;

/// <summary>
/// Skeleton helpers for IK: bone edges (children mean / leaf dirs) and lookups.
/// </summary>
public class Rig
{
	const float DefaultEdgeLength = 0.1f;
	static readonly Vector3 DefaultBoneAxis = new(0f, 0f, -1f);

	readonly Dictionary<int, Vector3> leafDirections = new();

	public Skeleton3D Skeleton { get; }

	public Rig(Skeleton3D sk) => Skeleton = sk;

	public int FindBone(string boneName) => Skeleton.FindBone(boneName);

	public string GetBoneName(int bone) => Skeleton.GetBoneName(bone);

	public Transform3D GetBoneGlobalRest(int bone) => Skeleton.GetBoneGlobalRest(bone);

	public Vector3 GetRestEdge(int bone) =>
		HasChildren(bone) ? MeanRestChildrenOrigin(bone) : LeafDirection(bone);

	public Vector3 RestDir(int bone)
	{
		Vector3 edge = GetRestEdge(bone);
		float len = edge.Length();
		if (len <= 1e-8f)
			throw new System.InvalidOperationException(
				$"Rig.RestDir: zero rest edge on '{GetBoneName(bone)}'");
		return edge / len;
	}

	public void SetLeafDirection(string boneName, Vector3 globalDirection)
	{
		int bone = Skeleton.FindBone(boneName);
		if (bone < 0)
			throw new System.ArgumentException($"SetLeafDirection: bone '{boneName}' not found");

		SetLeafDirection(bone, globalDirection);

		if (boneName.EndsWith(".R"))
		{
			string leftName = boneName[..^2] + ".L";
			int left = Skeleton.FindBone(leftName);
			if (left >= 0)
				SetLeafDirection(left, MirrorRightToLeft(globalDirection));
		}
	}

	void SetLeafDirection(int bone, Vector3 globalDirection) =>
		leafDirections[bone] = Skeleton.GetBoneGlobalPose(bone).Basis.Inverse() * globalDirection;

	bool HasChildren(int bone) => Skeleton.GetBoneChildren(bone).Length > 0;

	Vector3 MeanRestChildrenOrigin(int bone) =>
		MeanOrigin(bone, i => Skeleton.GetBoneRest(i).Origin);

	Vector3 MeanOrigin(int bone, System.Func<int, Vector3> origin)
	{
		int[] children = Skeleton.GetBoneChildren(bone);
		Vector3 sum = Vector3.Zero;
		foreach (int child in children)
			sum += origin(child);
		return sum / children.Length;
	}

	static Vector3 MirrorRightToLeft(Vector3 v) => new(-v.X, v.Y, v.Z);

	Vector3 LeafDirection(int bone) =>
		leafDirections.TryGetValue(bone, out Vector3 dir) ? dir : DefaultBoneAxis * DefaultEdgeLength;
}
