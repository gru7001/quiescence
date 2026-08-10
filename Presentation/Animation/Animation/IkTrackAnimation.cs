using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Baked pose track: one solved pose per keyframe.
/// PlayAt Catmull–Roms those poses between keys (neighbors wrap if cyclic).
/// </summary>
public class IkTrackAnimation
{
	readonly List<float> keyTimes = [];
	readonly List<Transform3D[]> poses = [];
	float duration;
	bool cyclic;

	public bool Cyclic => cyclic;
	public int KeyCount => poses.Count;

	public void Clear(float clipDuration, bool clipCyclic)
	{
		keyTimes.Clear();
		poses.Clear();
		duration = clipDuration;
		cyclic = clipCyclic;
	}

	public void AddKeyPose(float time, Transform3D[] pose)
	{
		keyTimes.Add(time);
		poses.Add(pose);
	}

	/// <summary>
	/// Solve each authored key from rest → baked pose track.
	/// Restores the skeleton to rest when finished.
	/// </summary>
	public static IkTrackAnimation Bake(
		IkAnimation anim,
		Rig rig,
		IkTermStack intrinsics,
		int steps = 400,
		float damping = 1e-1f,
		float maxStep = 0.15f)
	{
		Skeleton3D sk = rig.Skeleton;
		anim.SortByTime();
		float duration = anim.EffectiveDuration();
		float last = anim.LastKeyTime();
		if (anim.Cyclic && duration <= last + 1e-6f)
			throw new InvalidOperationException(
				$"IkTrackAnimation.Bake cyclic: Duration ({duration:0.##}s) must be greater than last key ({last:0.##}s)");

		Transform3D[] rest = new Transform3D[sk.GetBoneCount()];
		for (int i = 0; i < rest.Length; i++)
			rest[i] = sk.GetBoneRest(i);

		var track = new IkTrackAnimation();
		track.Clear(duration, anim.Cyclic);

		for (int k = 0; k < anim.Keys.Count; k++)
		{
			IkAnimKey key = anim.Keys[k];

			for (int i = 0; i < rest.Length; i++)
				sk.SetBonePose(i, rest[i]);

			var stack = new IkTermStack()
				.Add(key.TargetSet.BuildTransformTerm(), key.TargetSet.TargetWeight);
			foreach (var (term, weight) in intrinsics)
				stack.Add(term, weight);
			var solver = new IkSolver(stack, rig);
			for (int s = 0; s < steps; s++)
				solver.SolveStep(damping, maxStep);

			track.AddKeyPose(key.Time, IkMath.Snapshot(sk));
			GD.Print($"IkTrackAnimation.Bake key[{k}] t={key.Time:F2} cost={solver.Cost():F6}");
		}

		for (int i = 0; i < rest.Length; i++)
			sk.SetBonePose(i, rest[i]);

		return track;
	}

	public void ApplyKey(Skeleton3D sk, int keyIndex)
	{
		IkMath.Restore(sk, poses[keyIndex]);
	}

	/// <summary>Catmull–Rom between baked key poses at time.</summary>
	public void PlayAt(Skeleton3D sk, float time)
	{
		float t;
		if (cyclic)
			t = duration > 1e-8f ? Mathf.PosMod(time, duration) : 0f;
		else
			t = Mathf.Clamp(time, 0f, duration);

		if (poses.Count == 1)
		{
			IkMath.Restore(sk, poses[0]);
			return;
		}

		int n = poses.Count;
		int i1;
		int i2;
		float u;

		if (cyclic && t >= keyTimes[^1])
		{
			float span = duration - keyTimes[^1];
			if (span <= 1e-8f)
				throw new InvalidOperationException(
					"IkTrackAnimation cyclic: Duration must be greater than last key time");
			i1 = n - 1;
			i2 = 0;
			u = (t - keyTimes[^1]) / span;
		}
		else
		{
			i1 = 0;
			while (i1 + 1 < n && keyTimes[i1 + 1] <= t)
				i1++;

			if (i1 + 1 >= n)
			{
				IkMath.Restore(sk, poses[i1]);
				return;
			}

			i2 = i1 + 1;
			float t0 = keyTimes[i1];
			float t1 = keyTimes[i2];
			u = t1 > t0 ? (t - t0) / (t1 - t0) : 0f;
		}

		int i0 = Neighbor(i1 - 1, n);
		int i3 = Neighbor(i2 + 1, n);
		ApplyCatmullRomPose(sk, poses[i0], poses[i1], poses[i2], poses[i3], u);
	}

	int Neighbor(int index, int n)
	{
		if (cyclic)
			return ((index % n) + n) % n;
		return Mathf.Clamp(index, 0, n - 1);
	}

	static void ApplyCatmullRomPose(
		Skeleton3D sk,
		Transform3D[] p0,
		Transform3D[] p1,
		Transform3D[] p2,
		Transform3D[] p3,
		float u)
	{
		int bones = sk.GetBoneCount();
		for (int b = 0; b < bones; b++)
			sk.SetBonePose(b, CatmullRomTransform(p0[b], p1[b], p2[b], p3[b], u));
	}

	static Transform3D CatmullRomTransform(
		Transform3D t0, Transform3D t1, Transform3D t2, Transform3D t3, float u)
	{
		Vector3 o = CatmullRomVec(t0.Origin, t1.Origin, t2.Origin, t3.Origin, u);
		Quaternion q = CatmullRomQuat(
			t0.Basis.GetRotationQuaternion(),
			t1.Basis.GetRotationQuaternion(),
			t2.Basis.GetRotationQuaternion(),
			t3.Basis.GetRotationQuaternion(),
			u);
		return new Transform3D(new Basis(q), o);
	}

	static Vector3 CatmullRomVec(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t2 = t * t;
		float t3 = t2 * t;
		return 0.5f * (
			2f * p1 +
			(-p0 + p2) * t +
			(2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
			(-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	static float CatmullRomScalar(float p0, float p1, float p2, float p3, float t)
	{
		float t2 = t * t;
		float t3 = t2 * t;
		return 0.5f * (
			2f * p1 +
			(-p0 + p2) * t +
			(2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
			(-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	static Quaternion CatmullRomQuat(
		Quaternion q0, Quaternion q1, Quaternion q2, Quaternion q3, float t)
	{
		q0 = AlignQuat(q1, q0);
		q2 = AlignQuat(q1, q2);
		q3 = AlignQuat(q2, q3);
		var q = new Quaternion(
			CatmullRomScalar(q0.X, q1.X, q2.X, q3.X, t),
			CatmullRomScalar(q0.Y, q1.Y, q2.Y, q3.Y, t),
			CatmullRomScalar(q0.Z, q1.Z, q2.Z, q3.Z, t),
			CatmullRomScalar(q0.W, q1.W, q2.W, q3.W, t));
		return q.Normalized();
	}

	static Quaternion AlignQuat(Quaternion reference, Quaternion q) =>
		reference.Dot(q) < 0f ? new Quaternion(-q.X, -q.Y, -q.Z, -q.W) : q;
}
