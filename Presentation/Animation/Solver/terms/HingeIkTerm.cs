using Godot;
using System.Collections.Generic;

/// <summary>
/// Soft hinge as a plane constraint on bone direction (twist-free).
/// Axis authored in global-rest; chart is rest-local at eval.
/// d0 = Rig.RestDir, d = R d0, R = Rest⁻¹ Pose.
/// e = (d·u − d0·u, softplus_β(θmin−θ), softplus_β(θ−θmax))
/// </summary>
public class HingeIkTerm : IIkTerm
{
	const int PerJoint = 3;
	const float LimitBeta = 20f;

	readonly List<Joint> joints = [];

	struct Joint
	{
		public string Name;
		public Vector3 AxisGlobalRest;
		public float ThetaMin, ThetaMax;
	}

	public HingeIkTerm Add(string bone, Vector3 axisGlobalRest, float thetaMin, float thetaMax)
	{
		if (thetaMin >= thetaMax)
			throw new System.ArgumentException($"HingeIkTerm: thetaMin ({thetaMin}) >= thetaMax ({thetaMax})");

		joints.Add(new Joint
		{
			Name = bone,
			AxisGlobalRest = axisGlobalRest.Normalized(),
			ThetaMin = thetaMin,
			ThetaMax = thetaMax,
		});
		return this;
	}

	public HingeIkTerm AddMirrored(string bone, Vector3 axisGlobalRest, float thetaMin, float thetaMax)
	{
		Add(bone, axisGlobalRest, thetaMin, thetaMax);
		if (IkSide.TryMirrorName(bone, out string other))
		{
			IkSide.MirrorLimits(thetaMin, thetaMax, out float minM, out float maxM);
			Add(other, IkSide.MirrorX(axisGlobalRest), minM, maxM);
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
			Vector3 u = (rig.GetBoneGlobalRest(bone).Basis.Inverse() * j.AxisGlobalRest).Normalized();
			Basis r = sk.GetBoneRest(bone).Basis.Inverse() * sk.GetBonePose(bone).Basis;
			Vector3 d = r * d0;
			float theta = SwingAngle(d0, d, u);
			int o = offset + i * PerJoint;
			e[o] = d.Dot(u) - d0.Dot(u);
			e[o + 1] = SoftLimit(j.ThetaMin - theta);
			e[o + 2] = SoftLimit(theta - j.ThetaMax);
		}
	}

	static float SoftLimit(float overshoot) =>
		IkMath.Softplus(LimitBeta * overshoot) / LimitBeta;

	static int RequireBone(Rig rig, string name)
	{
		int i = rig.FindBone(name);
		if (i < 0)
			throw new System.ArgumentException($"HingeIkTerm: bone '{name}' not found");
		return i;
	}

	static float SwingAngle(Vector3 d0, Vector3 d, Vector3 u)
	{
		Vector3 p0 = d0 - d0.Dot(u) * u;
		Vector3 p = d - d.Dot(u) * u;
		float n0 = p0.Length();
		float n = p.Length();
		if (n0 <= 1e-8f || n <= 1e-8f)
			throw new System.InvalidOperationException(
				"HingeIkTerm: bone direction parallel to hinge axis (no swing angle)");
		p0 /= n0;
		p /= n;
		return Mathf.Atan2(p0.Cross(p).Dot(u), p0.Dot(p));
	}
}
