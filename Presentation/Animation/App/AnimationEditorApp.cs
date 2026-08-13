using Godot;

/// <summary>Scene root for the IK animation editor (qbody + orbit camera + editor UI).</summary>
public partial class AnimationEditorApp : Node3D
{
	Rig rig;
	IkAnimationEditor editor;
	IkTermStack terms;
	Transform3D[] restPoses;

	public override void _Ready()
	{
		terms = QbodyIk.DefaultTerms();

		var sk = GetNode<Skeleton3D>("Qbody/Rig/Skeleton3D");
		rig = QbodyIk.Configure(sk);

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
