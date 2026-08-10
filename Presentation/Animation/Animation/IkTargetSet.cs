using Godot;
using System;

/// <summary>
/// Serializable IK target set (bone → transform goals + weight).
/// Save/load with ResourceSaver / ResourceLoader (.tres).
/// </summary>
[GlobalClass]
public partial class IkTargetSet : Godot.Resource
{
	[Export]
	public Godot.Collections.Array<IkTargetEntry> Targets { get; set; } = [];

	[Export]
	public float TargetWeight { get; set; } = 1f;

	public TransformIkTerm BuildTransformTerm()
	{
		var term = new TransformIkTerm();
		foreach (IkTargetEntry entry in Targets)
			term.Add(entry.Bone, entry.Transform);
		return term;
	}

	public void AddTarget(string bone, Transform3D transform)
	{
		Targets.Add(new IkTargetEntry { Bone = bone, Transform = transform });
	}

	public void RemoveAt(int index)
	{
		Targets.RemoveAt(index);
	}

	public static void Save(IkTargetSet data, string path)
	{
		// Fresh typed entries so Godot writes IkTargetEntry, not an empty CSharpScript stub.
		var toSave = Clone(data);
		toSave.TakeOverPath(path);
		Error err = ResourceSaver.Save(toSave, path);
		if (err != Error.Ok)
			throw new InvalidOperationException($"IkTargetSet.Save failed ({err}): {path}");
	}

	public static IkTargetSet Load(string path)
	{
		var loaded = ResourceLoader.Load<IkTargetSet>(
			path, cacheMode: ResourceLoader.CacheMode.Ignore);
		if (loaded == null)
			throw new InvalidOperationException($"IkTargetSet.Load failed: {path}");
		return Clone(loaded);
	}

	/// <summary>
	/// Deep copy with .L↔.R bone names and transforms reflected through YZ (X → −X).
	/// Midline bones (no .L/.R) keep their name; transform still mirrored.
	/// </summary>
	public static IkTargetSet CloneMirrored(IkTargetSet src)
	{
		var copy = new IkTargetSet { TargetWeight = src.TargetWeight };
		if (src.Targets == null) return copy;

		foreach (Variant v in src.Targets)
		{
			string bone;
			Transform3D xf;
			if (v.AsGodotObject() is IkTargetEntry e)
			{
				if (string.IsNullOrEmpty(e.Bone)) continue;
				bone = e.Bone;
				xf = e.Transform;
			}
			else if (v.AsGodotObject() is Godot.Resource r)
			{
				bone = r.Get("Bone").AsString();
				if (string.IsNullOrEmpty(bone)) continue;
				xf = r.Get("Transform").AsTransform3D();
			}
			else
				continue;

			string outBone = IkSide.TryMirrorName(bone, out string mirrored) ? mirrored : bone;
			copy.AddTarget(outBone, IkSide.MirrorX(xf));
		}
		return copy;
	}

	/// <summary>Deep copy into a new IkTargetSet with real IkTargetEntry instances.</summary>
	public static IkTargetSet Clone(IkTargetSet src)
	{
		var copy = new IkTargetSet { TargetWeight = src.TargetWeight };
		if (src.Targets == null) return copy;

		foreach (Variant v in src.Targets)
		{
			if (v.AsGodotObject() is IkTargetEntry e)
			{
				if (string.IsNullOrEmpty(e.Bone)) continue;
				copy.AddTarget(e.Bone, e.Transform);
				continue;
			}
			// Old broken .tres: generic Resource with exported props still present.
			if (v.AsGodotObject() is Godot.Resource r)
			{
				string bone = r.Get("Bone").AsString();
				if (string.IsNullOrEmpty(bone)) continue;
				copy.AddTarget(bone, r.Get("Transform").AsTransform3D());
			}
		}
		return copy;
	}
}
