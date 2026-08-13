using Godot;

/// <summary>Shared Qbody IK defaults (term stack + leaf dirs) for editor bake and board playback.</summary>
public static class QbodyIk
{
	public const string WalkPath = "res://ik/walk.tres";
	public const string WalkTrackPath = "res://ik/walk_track.tres";

	public static IkTermStack DefaultTerms()
	{
		var stretch = new RestLengthIkTerm();
		var hinges = new HingeIkTerm()
			.AddMirrored("shin.L", Vector3.Right, -Mathf.Pi, Mathf.Pi / 8f)
			.AddMirrored("forearm.L", Vector3.Up, -Mathf.Pi, Mathf.Pi / 8f);
		var cones = new SwingConeIkTerm()
			.Add("spine", Mathf.DegToRad(25f))
			.Add("spine.001", Mathf.DegToRad(20f))
			.Add("spine.002", Mathf.DegToRad(20f))
			.Add("spine.003", Mathf.DegToRad(25f))
			.Add("spine.004", Mathf.DegToRad(30f))
			.Add("spine.005", Mathf.DegToRad(35f))
			.Add("spine.006", Mathf.DegToRad(45f))
			.AddMirrored("shoulder.L", Mathf.DegToRad(40f))
			.AddMirrored("upper_arm.L", Mathf.DegToRad(90f))
			.AddMirrored("hand.L", Mathf.DegToRad(50f))
			.AddMirrored("thigh.L", Mathf.DegToRad(90f))
			.AddMirrored("foot.L", Mathf.DegToRad(40f))
			.AddMirrored("toe.L", Mathf.DegToRad(30f));
		var twists = new TwistIkTerm()
			.Add("spine", Mathf.DegToRad(-30f), Mathf.DegToRad(30f))
			.Add("spine.001", Mathf.DegToRad(-25f), Mathf.DegToRad(25f))
			.Add("spine.002", Mathf.DegToRad(-25f), Mathf.DegToRad(25f))
			.Add("spine.003", Mathf.DegToRad(-30f), Mathf.DegToRad(30f))
			.Add("spine.004", Mathf.DegToRad(-35f), Mathf.DegToRad(35f))
			.Add("spine.005", Mathf.DegToRad(-40f), Mathf.DegToRad(40f))
			.Add("spine.006", Mathf.DegToRad(-60f), Mathf.DegToRad(60f))
			.AddMirrored("shoulder.L", Mathf.DegToRad(-40f), Mathf.DegToRad(40f))
			.AddMirrored("upper_arm.L", Mathf.DegToRad(-60f), Mathf.DegToRad(60f))
			.AddMirrored("forearm.L", Mathf.DegToRad(-20f), Mathf.DegToRad(20f))
			.AddMirrored("hand.L", Mathf.DegToRad(-40f), Mathf.DegToRad(40f))
			.AddMirrored("thigh.L", Mathf.DegToRad(-45f), Mathf.DegToRad(45f))
			.AddMirrored("shin.L", Mathf.DegToRad(-20f), Mathf.DegToRad(20f))
			.AddMirrored("foot.L", Mathf.DegToRad(-30f), Mathf.DegToRad(30f))
			.AddMirrored("toe.L", Mathf.DegToRad(-20f), Mathf.DegToRad(20f));
		var locks = new RestRotationIkTerm()
			.AddMirrored("breast.L")
			.AddMirrored("pelvis.L")
			.Add("neutral_bone");

		return new IkTermStack()
			.Add(stretch, 1f)
			.Add(hinges, 1f)
			.Add(cones, 0.1f)
			.Add(twists, 1f)
			.Add(locks, 1f);
	}

	public static Rig Configure(Skeleton3D sk)
	{
		var rig = new Rig(sk);
		rig.SetLeafDirection("toe.R", new Vector3(0, 0, -0.02f));
		rig.SetLeafDirection("hand.R", new Vector3(0.04f, -0.11f, -0.01f));
		rig.SetLeafDirection("pelvis.R", new Vector3(0.07f, 0.04f, -0.06f));
		rig.SetLeafDirection("breast.R", new Vector3(0, 0, -0.1f));
		rig.SetLeafDirection("spine.006", new Vector3(0, 0.15f, 0));
		return rig;
	}

	public static Skeleton3D FindSkeleton(Node root) =>
		root.GetNodeOrNull<Skeleton3D>("Rig/Skeleton3D")
		?? FindSkeletonRecursive(root);

	static Skeleton3D FindSkeletonRecursive(Node n)
	{
		if (n is Skeleton3D sk)
			return sk;
		foreach (var child in n.GetChildren())
		{
			if (child is Node c)
			{
				var found = FindSkeletonRecursive(c);
				if (found != null)
					return found;
			}
		}
		return null;
	}

	public static void ApplyRest(Skeleton3D sk)
	{
		for (int i = 0; i < sk.GetBoneCount(); i++)
			sk.SetBonePose(i, sk.GetBoneRest(i));
	}
}
