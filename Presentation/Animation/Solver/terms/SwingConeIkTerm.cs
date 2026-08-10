using Godot;
using System.Collections.Generic;

/// <summary>
/// Soft ball-joint swing cone: bone direction stays near rest within max angle.
/// d0 = Rig.RestDir at eval; θ = ∠(d0, R d0); e = softplus(θ − θmax) − softplus(−θmax).
/// </summary>
public class SwingConeIkTerm : IIkTerm
{
	readonly List<Joint> joints = [];

	struct Joint
	{
		public string Name;
		public float ThetaMax;
	}

	public SwingConeIkTerm Add(string bone, float thetaMax)
	{
		if (thetaMax <= 0f)
			throw new System.ArgumentException($"SwingConeIkTerm: thetaMax ({thetaMax}) must be > 0");

		joints.Add(new Joint { Name = bone, ThetaMax = thetaMax });
		return this;
	}

	public SwingConeIkTerm AddMirrored(string bone, float thetaMax)
	{
		Add(bone, thetaMax);
		if (IkSide.TryMirrorName(bone, out string other))
			Add(other, thetaMax);
		return this;
	}

	public int Dim(Rig _) => joints.Count;

	public IEnumerable<string> Bones(Rig _)
	{
		foreach (var j in joints)
			yield return j.Name;
	}

	public void WriteResidual(Rig rig, Transform3D[] globalPose, float[] e, int offset)
	{
		Skeleton3D sk = rig.Skeleton;
		for (int i = 0; i < joints.Count; i++)
		{
			Joint j = joints[i];
			int bone = RequireBone(rig, j.Name);
			Vector3 d0 = rig.RestDir(bone);
			Basis r = sk.GetBoneRest(bone).Basis.Inverse() * sk.GetBonePose(bone).Basis;
			Vector3 d = (r * d0).Normalized();
			float cos = Mathf.Clamp(d0.Dot(d), -1f, 1f);
			float theta = Mathf.Acos(cos);
			e[offset + i] = IkMath.Softplus(theta - j.ThetaMax) - IkMath.Softplus(-j.ThetaMax);
		}
	}

	static int RequireBone(Rig rig, string name)
	{
		int i = rig.FindBone(name);
		if (i < 0)
			throw new System.ArgumentException($"SwingConeIkTerm: bone '{name}' not found");
		return i;
	}
}
