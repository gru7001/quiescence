using Godot;
using System.Collections.Generic;

/// <summary>
/// SE(3) global-transform targets: e = (p−p*) ⊕ rotvec(R*⁻¹ R) per bone.
/// </summary>
public class TransformIkTerm : IIkTerm
{
	readonly List<(string Bone, Transform3D Desired)> targets = [];

	public TransformIkTerm Add(string bone, Transform3D desired)
	{
		targets.Add((bone, desired));
		return this;
	}

	public int Dim(Rig _) => targets.Count * 6;

	public IEnumerable<string> Bones(Rig _)
	{
		foreach (var (bone, _) in targets)
			yield return bone;
	}

	public void WriteResidual(Rig rig, Transform3D[] globalPose, float[] e, int offset)
	{
		for (int t = 0; t < targets.Count; t++)
		{
			int bone = rig.FindBone(targets[t].Bone);
			int o = offset + t * 6;
			if (bone < 0)
			{
				for (int k = 0; k < 6; k++) e[o + k] = 0f;
				continue;
			}

			Transform3D cur = globalPose[bone];
			Transform3D des = targets[t].Desired;
			Vector3 dp = cur.Origin - des.Origin;
			e[o] = dp.X;
			e[o + 1] = dp.Y;
			e[o + 2] = dp.Z;
			Vector3 w = IkMath.RotVec(des.Basis.Inverse() * cur.Basis);
			e[o + 3] = w.X;
			e[o + 4] = w.Y;
			e[o + 5] = w.Z;
		}
	}
}
