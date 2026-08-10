using Godot;

/// <summary>.L ↔ .R name/axis helpers for bilateral IK constraints.</summary>
public static class IkSide
{
	public static bool TryMirrorName(string bone, out string mirrored)
	{
		if (bone.EndsWith(".L"))
		{
			mirrored = bone[..^2] + ".R";
			return true;
		}
		if (bone.EndsWith(".R"))
		{
			mirrored = bone[..^2] + ".L";
			return true;
		}
		mirrored = null;
		return false;
	}

	public static Vector3 MirrorX(Vector3 v) => new(-v.X, v.Y, v.Z);

	/// <summary>Reflect SE(3) through the YZ plane (X → −X), proper rotation (det +1).</summary>
	public static Transform3D MirrorX(Transform3D t)
	{
		// B' = S B S with S = diag(−1, 1, 1)
		Vector3 x = t.Basis.X;
		Vector3 y = t.Basis.Y;
		Vector3 z = t.Basis.Z;
		var b = new Basis(
			new Vector3(x.X, -x.Y, -x.Z),
			new Vector3(-y.X, y.Y, y.Z),
			new Vector3(-z.X, z.Y, z.Z));
		return new Transform3D(b, MirrorX(t.Origin));
	}

	/// <summary>RH angle band about −u is the negation of the band about +u.</summary>
	public static void MirrorLimits(float thetaMin, float thetaMax, out float minM, out float maxM)
	{
		minM = -thetaMax;
		maxM = -thetaMin;
	}
}
