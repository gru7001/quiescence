using Godot;
using System.Collections.Generic;

/// <summary>
/// One block of residual rows for the IK solver (targets, constraints, …).
/// Solver stacks weighted terms: e = ⊕ wᵢ eᵢ, J = ⊕ wᵢ Jᵢ.
/// Terms are declarative; Rig supplies bone indices / rest edges at eval time.
/// </summary>
public interface IIkTerm
{
	int Dim(Rig rig);

	/// <summary>Bones this term depends on (solver walks ancestors for DOFs).</summary>
	IEnumerable<string> Bones(Rig rig);

	/// <summary>Write residual into e[offset .. offset+Dim).</summary>
	void WriteResidual(Rig rig, Transform3D[] globalPose, float[] e, int offset);
}
