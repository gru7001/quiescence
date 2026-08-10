using Godot;

public partial class Main2 : Node3D
{
	Rig rig;
	IkAnimationEditor editor;
	IkTermStack terms;
	Transform3D[] restPoses;

	public override void _Ready()
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

		terms = new IkTermStack()
			.Add(stretch, 1f)
			.Add(hinges, 1f)
			.Add(cones, 0.1f)
			.Add(twists, 1f)
			.Add(locks, 1f);


		var sk = GetNode<Skeleton3D>("Qbody/Rig/Skeleton3D");
		rig = new Rig(sk);
		rig.SetLeafDirection("toe.R", new Vector3(0, 0, -0.02f));
		rig.SetLeafDirection("hand.R", new Vector3(0.04f, -0.11f, -0.01f));
		rig.SetLeafDirection("pelvis.R", new Vector3(0.07f, 0.04f, -0.06f));
		rig.SetLeafDirection("breast.R", new Vector3(0, 0, -0.1f));
		rig.SetLeafDirection("spine.006", new Vector3(0, 0.15f, 0));

		restPoses = new Transform3D[sk.GetBoneCount()];
		for (int i = 0; i < restPoses.Length; i++)
			restPoses[i] = sk.GetBoneRest(i);

		IkAnimation initial = null;
		if (ResourceLoader.Exists("res://ik/animation.tres"))
			initial = IkAnimation.Load("res://ik/animation.tres");
		else if (ResourceLoader.Exists("res://ik/solver.tres"))
		{
			initial = new IkAnimation();
			initial.AddKey(0f, IkTargetSet.Load("res://ik/solver.tres"));
		}

		editor = new IkAnimationEditor { Name = "IkAnimationEditor" };
		AddChild(editor);
		editor.Setup(rig, terms, initial);
		editor.ResetToRestPressed += ResetSkeletonToRest;
	}

	void ResetSkeletonToRest()
	{
		Skeleton3D sk = rig.Skeleton;
		for (int i = 0; i < restPoses.Length; i++)
			sk.SetBonePose(i, restPoses[i]);
	}

	public override void _Process(double delta)
	{
		Skeleton3D sk = rig.Skeleton;
		if (editor.Playing)
		{
			editor.TickPlay(sk, (float)delta);
			return;
		}

		if (!editor.HasActiveKey)
			return;

		TransformIkTerm targets = editor.BuildActiveTransformTerm();
		var stack = new IkTermStack().Add(targets, editor.ActiveTargetWeight);
		foreach (var (term, weight) in terms)
			stack.Add(term, weight);
		var solver = new IkSolver(stack, rig);
		solver.SolveStep(damping: 1e-1f, maxStep: 0.15f);
	}
}
