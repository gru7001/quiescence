using Godot;
using System.Collections.Generic;

/// <summary>
/// Soft twist about the bone axis (rest-local d0 from Rig.RestDir at eval).
/// e = (softplus_β(θmin−θ), softplus_β(θ−θmax)) — flat inside the band.
/// </summary>
public class TwistIkTerm : IIkTerm
{
	const int PerJoint = 2;
	const float LimitBeta = 20f;

	readonly List<Joint> joints = [];

	struct Joint
	{
		public string Name;
		public float ThetaMin, ThetaMax;
	}

	public TwistIkTerm Add(string bone, float thetaMin, float thetaMax)
	{
		if (thetaMin >= thetaMax)
			throw new System.ArgumentException($"TwistIkTerm: thetaMin ({thetaMin}) >= thetaMax ({thetaMax})");

		joints.Add(new Joint { Name = bone, ThetaMin = thetaMin, ThetaMax = thetaMax });
		return this;
	}

	public TwistIkTerm AddMirrored(string bone, float thetaMin, float thetaMax)
	{
		Add(bone, thetaMin, thetaMax);
		if (IkSide.TryMirrorName(bone, out string other))
		{
			IkSide.MirrorLimits(thetaMin, thetaMax, out float minM, out float maxM);
			Add(other, minM, maxM);
		}
		return this;
	}

	public int Dim(Rig _) => joints.Count * PerJoint;

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
			float theta = IkMath.TwistAbout(r, d0);
			int o = offset + i * PerJoint;
			e[o] = SoftLimit(j.ThetaMin - theta);
			e[o + 1] = SoftLimit(theta - j.ThetaMax);
		}
	}

	static float SoftLimit(float overshoot) =>
		IkMath.Softplus(LimitBeta * overshoot) / LimitBeta;

	static int RequireBone(Rig rig, string name)
	{
		int i = rig.FindBone(name);
		if (i < 0)
			throw new System.ArgumentException($"TwistIkTerm: bone '{name}' not found");
		return i;
	}
}
