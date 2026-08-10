using Godot;
using System.Collections.Generic;

/// <summary>
/// Soft constraint: each non-root bone's local length matches rest.
/// eᵢ = ‖Pose.Originᵢ‖ − ‖Rest.Originᵢ‖
/// </summary>
public class RestLengthIkTerm : IIkTerm
{
	public int Dim(Rig rig)
	{
		int n = 0;
		Skeleton3D sk = rig.Skeleton;
		for (int i = 0; i < sk.GetBoneCount(); i++)
			if (sk.GetBoneParent(i) >= 0) n++;
		return n;
	}

	public IEnumerable<string> Bones(Rig rig)
	{
		Skeleton3D sk = rig.Skeleton;
		for (int i = 0; i < sk.GetBoneCount(); i++)
		{
			if (sk.GetBoneParent(i) < 0) continue;
			yield return sk.GetBoneName(i);
		}
	}

	public void WriteResidual(Rig rig, Transform3D[] globalPose, float[] e, int offset)
	{
		Skeleton3D sk = rig.Skeleton;
		int o = offset;
		for (int i = 0; i < sk.GetBoneCount(); i++)
		{
			if (sk.GetBoneParent(i) < 0) continue;
			e[o++] = sk.GetBonePose(i).Origin.Length() - sk.GetBoneRest(i).Origin.Length();
		}
	}
}
