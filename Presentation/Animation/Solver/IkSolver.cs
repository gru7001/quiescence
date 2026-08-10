using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Jacobi Gauss–Newton IK over a weighted term stack.
/// One FD Jacobian, then one damped step.
///
/// DOFs: per involved bone — 3 world rotations + length;
/// root bones (no parent) also get 3 world translations.
/// FK: G_i = G_parent · Pose_i
/// </summary>
public class IkSolver
{
	readonly IkTermStack terms;
	readonly Rig rig;

	public IkSolver(IkTermStack terms, Rig rig)
	{
		this.terms = terms;
		this.rig = rig;
	}

	public float Cost()
	{
		float[] e = Residual();
		float s = 0f;
		foreach (float v in e) s += v * v;
		return 0.5f * s;
	}

	public float SolveStep(float damping = 1e-2f, float maxStep = 0.15f, float eps = 1e-4f)
	{
		Skeleton3D sk = rig.Skeleton;
		List<IkMath.Dof> q = BuildDofs();
		float[] e = Residual();
		float[,] j = Jacobian(q, eps);
		float[] dq = IkMath.NormalEq(j, e, damping);

		for (int k = 0; k < q.Count; k++)
			q[k].Apply(sk, Mathf.Clamp(dq[k], -maxStep, maxStep));

		return Cost();
	}

	int Dim
	{
		get
		{
			int m = 0;
			foreach (var (term, _) in terms) m += term.Dim(rig);
			return m;
		}
	}

	float[] Residual()
	{
		Skeleton3D sk = rig.Skeleton;
		Transform3D[] g = IkMath.Fk(sk);
		var e = new float[Dim];
		int o = 0;
		foreach (var (term, w) in terms)
		{
			int d = term.Dim(rig);
			term.WriteResidual(rig, g, e, o);
			if (w != 1f)
			{
				for (int i = 0; i < d; i++)
					e[o + i] *= w;
			}
			o += d;
		}
		return e;
	}

	float[,] Jacobian(List<IkMath.Dof> q, float eps)
	{
		Skeleton3D sk = rig.Skeleton;
		int m = Dim, n = q.Count;
		var j = new float[m, n];
		Transform3D[] snap = IkMath.Snapshot(sk);

		for (int k = 0; k < n; k++)
		{
			IkMath.Restore(sk, snap); q[k].Apply(sk, eps);  float[] ep = Residual();
			IkMath.Restore(sk, snap); q[k].Apply(sk, -eps); float[] em = Residual();
			float inv = 0.5f / eps;
			for (int i = 0; i < m; i++)
				j[i, k] = (ep[i] - em[i]) * inv;
		}
		IkMath.Restore(sk, snap);
		return j;
	}

	List<IkMath.Dof> BuildDofs()
	{
		Skeleton3D sk = rig.Skeleton;
		var bones = new SortedSet<int>();
		foreach (var (term, _) in terms)
		{
			foreach (string name in term.Bones(rig))
			{
				for (int b = sk.FindBone(name); b >= 0; b = sk.GetBoneParent(b))
					bones.Add(b);
			}
		}

		var q = new List<IkMath.Dof>();
		foreach (int bone in bones)
		{
			int b = bone;
			q.Add(new((s, dq) => IkMath.Rotate(s, b, Vector3.Right, dq)));
			q.Add(new((s, dq) => IkMath.Rotate(s, b, Vector3.Up, dq)));
			q.Add(new((s, dq) => IkMath.Rotate(s, b, Vector3.Back, dq)));
			q.Add(new((s, dq) => IkMath.Lengthen(s, b, dq)));
			if (sk.GetBoneParent(b) < 0)
			{
				q.Add(new((s, dq) => IkMath.Translate(s, b, Vector3.Right, dq)));
				q.Add(new((s, dq) => IkMath.Translate(s, b, Vector3.Up, dq)));
				q.Add(new((s, dq) => IkMath.Translate(s, b, Vector3.Back, dq)));
			}
		}
		return q;
	}
}

/// <summary>Shared FK / DOF / linear algebra for IkSolver.</summary>
public static class IkMath
{
	public readonly struct Dof
	{
		public readonly Action<Skeleton3D, float> Apply;
		public Dof(Action<Skeleton3D, float> apply) => Apply = apply;
	}

	public static Transform3D[] Fk(Skeleton3D sk)
	{
		int n = sk.GetBoneCount();
		var g = new Transform3D[n];
		var done = new bool[n];
		Transform3D Eval(int i)
		{
			if (done[i]) return g[i];
			int p = sk.GetBoneParent(i);
			g[i] = p >= 0 ? Eval(p) * sk.GetBonePose(i) : sk.GetBonePose(i);
			done[i] = true;
			return g[i];
		}
		for (int i = 0; i < n; i++) Eval(i);
		return g;
	}

	public static void Rotate(Skeleton3D sk, int bone, Vector3 axisWorld, float dq)
	{
		if (Mathf.Abs(dq) <= float.Epsilon) return;
		Transform3D[] g = Fk(sk);
		Transform3D G = g[bone];
		Basis R = new Basis(axisWorld.Normalized(), dq) * G.Basis;
		int p = sk.GetBoneParent(bone);
		Transform3D parent = p >= 0 ? g[p] : Transform3D.Identity;
		Transform3D local = parent.AffineInverse() * new Transform3D(R, G.Origin);
		Transform3D pose = sk.GetBonePose(bone);
		pose.Basis = local.Basis;
		sk.SetBonePose(bone, pose);
	}

	public static void Lengthen(Skeleton3D sk, int bone, float dq)
	{
		if (Mathf.Abs(dq) <= float.Epsilon) return;
		foreach (int child in sk.GetBoneChildren(bone))
		{
			Transform3D pose = sk.GetBonePose(child);
			pose.Origin *= 1f + dq;
			sk.SetBonePose(child, pose);
		}
	}

	/// <summary>Translate a root bone’s pose origin along a world axis (no parent).</summary>
	public static void Translate(Skeleton3D sk, int bone, Vector3 axisWorld, float dq)
	{
		if (Mathf.Abs(dq) <= float.Epsilon) return;
		if (sk.GetBoneParent(bone) >= 0)
			throw new InvalidOperationException(
				$"IkMath.Translate: bone {bone} has a parent (only roots translate)");
		Transform3D pose = sk.GetBonePose(bone);
		pose.Origin += axisWorld.Normalized() * dq;
		sk.SetBonePose(bone, pose);
	}

	public static Vector3 RotVec(Basis b)
	{
		Quaternion q = b.GetRotationQuaternion();
		if (q.W < 0f) q = new(-q.X, -q.Y, -q.Z, -q.W);
		Vector3 v = new(q.X, q.Y, q.Z);
		float s = v.Length();
		if (s <= 1e-8f) return Vector3.Zero;
		return v * (2f * Mathf.Atan2(s, q.W) / s);
	}

	public static float[] NormalEq(float[,] j, float[] e, float lambda)
	{
		int m = j.GetLength(0), n = j.GetLength(1);
		var a = new float[n, n];
		var b = new float[n];
		for (int c = 0; c < n; c++)
		{
			float rhs = 0f;
			for (int i = 0; i < m; i++) rhs -= j[i, c] * e[i];
			b[c] = rhs;
			for (int d = c; d < n; d++)
			{
				float s = c == d ? lambda : 0f;
				for (int i = 0; i < m; i++) s += j[i, c] * j[i, d];
				a[c, d] = a[d, c] = s;
			}
		}
		return Chol(a, b);
	}

	static float[] Chol(float[,] a, float[] b)
	{
		int n = b.Length;
		var L = new float[n, n];
		for (int i = 0; i < n; i++)
		for (int j = 0; j <= i; j++)
		{
			float s = a[i, j];
			for (int k = 0; k < j; k++) s -= L[i, k] * L[j, k];
			L[i, j] = i == j ? Mathf.Sqrt(Mathf.Max(s, 1e-12f)) : s / L[j, j];
		}
		var y = new float[n];
		for (int i = 0; i < n; i++)
		{
			float s = b[i];
			for (int k = 0; k < i; k++) s -= L[i, k] * y[k];
			y[i] = s / L[i, i];
		}
		var x = new float[n];
		for (int i = n - 1; i >= 0; i--)
		{
			float s = y[i];
			for (int k = i + 1; k < n; k++) s -= L[k, i] * x[k];
			x[i] = s / L[i, i];
		}
		return x;
	}

	public static Transform3D[] Snapshot(Skeleton3D sk)
	{
		var p = new Transform3D[sk.GetBoneCount()];
		for (int i = 0; i < p.Length; i++) p[i] = sk.GetBonePose(i);
		return p;
	}

	public static void Restore(Skeleton3D sk, Transform3D[] p)
	{
		for (int i = 0; i < p.Length; i++) sk.SetBonePose(i, p[i]);
	}

	/// <summary>
	/// Twist of R = Rest⁻¹ Pose about rest bone axis d0 (swing removed first).
	/// </summary>
	public static float TwistAbout(Basis r, Vector3 d0)
	{
		d0 = d0.Normalized();
		Vector3 d = (r * d0).Normalized();
		Vector3 axis = d0.Cross(d);
		float s = axis.Length();
		Basis swing;
		if (s <= 1e-8f)
		{
			if (d0.Dot(d) > 0f)
				swing = Basis.Identity;
			else
				throw new InvalidOperationException(
					"IkMath.TwistAbout: bone direction flipped 180° (swing ambiguous)");
		}
		else
		{
			float ang = Mathf.Atan2(s, d0.Dot(d));
			swing = new Basis(axis / s, ang);
		}

		return RotVec(swing.Inverse() * r).Dot(d0);
	}

	public static float Softplus(float x)
	{
		if (x > 20f) return x;
		if (x < -20f) return 0f;
		return Mathf.Log(1f + Mathf.Exp(x));
	}
}
