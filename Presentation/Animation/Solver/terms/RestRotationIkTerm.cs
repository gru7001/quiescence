using Godot;
using System.Collections.Generic;

/// <summary>
/// Soft “don’t rotate”: local pose basis wants to stay at rest.
/// e = rotvec(Rest⁻¹ Pose) ∈ R³  (0 when pose basis = rest basis).
/// </summary>
public class RestRotationIkTerm : IIkTerm
{
	const int PerJoint = 3;

	readonly List<string> bones = [];

	public RestRotationIkTerm Add(string bone)
	{
		bones.Add(bone);
		return this;
	}

	public RestRotationIkTerm AddMirrored(string bone)
	{
		Add(bone);
		if (IkSide.TryMirrorName(bone, out string other))
			Add(other);
		return this;
	}

	public int Dim(Rig _) => bones.Count * PerJoint;

	public IEnumerable<string> Bones(Rig _) => bones;

	public void WriteResidual(Rig rig, Transform3D[] globalPose, float[] e, int offset)
	{
		Skeleton3D sk = rig.Skeleton;
		for (int i = 0; i < bones.Count; i++)
		{
			int bone = RequireBone(rig, bones[i]);
			Basis r = sk.GetBoneRest(bone).Basis.Inverse() * sk.GetBonePose(bone).Basis;
			Vector3 w = IkMath.RotVec(r);
			int o = offset + i * PerJoint;
			e[o] = w.X;
			e[o + 1] = w.Y;
			e[o + 2] = w.Z;
		}
	}

	static int RequireBone(Rig rig, string name)
	{
		int i = rig.FindBone(name);
		if (i < 0)
			throw new System.ArgumentException($"RestRotationIkTerm: bone '{name}' not found");
		return i;
	}
}
